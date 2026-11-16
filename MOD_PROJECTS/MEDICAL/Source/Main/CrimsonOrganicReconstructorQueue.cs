using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonOrganicQueueBills
    {
        static CrimsonOrganicQueueBills()
        {
            new Harmony("rimod.crimson.sos2.organic-queue").Patch(
                AccessTools.Method(typeof(Bill_Production), "ShouldDoNow"),
                postfix: new HarmonyMethod(typeof(CrimsonOrganicQueueBills), "ShouldLoad"));
        }

        private static void ShouldLoad(Bill_Production __instance, ref bool __result)
        {
            if (!__result || __instance.billStack == null) return;
            CrimsonOrganicReconstructorQueue machine = __instance.billStack.billGiver as CrimsonOrganicReconstructorQueue;
            if (machine != null) __result = machine.CanQueueMore(__instance);
        }
    }

    public sealed class CrimsonOrganicReconstructorQueue : Building_WorkTable_HeatPush, IThingHolder
    {
        public const int WaitingCapacity = 10;
        public const int TotalCapacity = WaitingCapacity + 1;
        private ThingOwner<Thing> input;
        private ThingOwner<Thing> output;
        // IDs keep a deleted bill from leaving an unresolved Scribe reference.
        // The input owner and this list have the same FIFO order.
        private List<string> inputBills = new List<string>();
        private float workLeft;

        public CrimsonOrganicReconstructorQueue()
        {
            input = new ThingOwner<Thing>(this, false, LookMode.Deep);
            output = new ThingOwner<Thing>(this, false, LookMode.Deep);
        }

        public int InputCount { get { return input.Count; } }
        public int WaitingCount { get { return Math.Max(0, InputCount - 1); } }
        public float WorkLeft { get { return workLeft; } }
        private RecipeDef Recipe { get { return DefDatabase<RecipeDef>.GetNamed(CrimsonOrganicReconstructor.RecipeName); } }

        public ThingOwner GetDirectlyHeldThings() { return input; }
        public void GetChildHolders(List<IThingHolder> holders)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(holders, input);
            ThingOwnerUtility.AppendThingHoldersFromThings(holders, output);
        }

        internal bool CanQueueMore(Bill_Production bill)
        {
            if (InputCount >= TotalCapacity || bill == null || bill.recipe != Recipe) return false;
            string id = bill.GetUniqueLoadID();
            int reserved = 0;
            foreach (string queuedBill in inputBills) if (queuedBill == id) reserved++;
            if (bill.repeatMode == BillRepeatModeDefOf.RepeatCount && reserved >= bill.repeatCount) return false;
            if (bill.repeatMode == BillRepeatModeDefOf.TargetCount)
            {
                int expectedFuel = 0;
                foreach (Thing thing in input)
                {
                    Corpse corpse = thing as Corpse;
                    if (corpse != null && corpse.InnerPawn != null)
                        expectedFuel += CrimsonOrganicReconstructor.FuelYield(corpse.InnerPawn.BodySize);
                }
                foreach (Thing thing in output) if (thing.def == ThingDefOf.Chemfuel) expectedFuel += thing.stackCount;
                if (bill.recipe.WorkerCounter.CountProducts(bill) + expectedFuel >= bill.targetCount) return false;
            }
            return true;
        }

        internal bool TryLoad(Corpse corpse, Bill_Production bill)
        {
            if (corpse == null || corpse.Destroyed || corpse.InnerPawn == null || !corpse.InnerPawn.RaceProps.IsFlesh
                || bill == null || bill.billStack != billStack || !billStack.Bills.Contains(bill)
                || !bill.ShouldDoNow() || !CanQueueMore(bill)
                || !Recipe.fixedIngredientFilter.Allows(corpse) || !bill.ingredientFilter.Allows(corpse)) return false;
            if (!input.TryAddOrTransfer(corpse, false)) return false;
            inputBills.Add(bill.GetUniqueLoadID());
            if (InputCount == 1) workLeft = Recipe.workAmount;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref input, "crimsonOrganicInput", this);
            Scribe_Deep.Look(ref output, "crimsonOrganicOutput", this);
            Scribe_Collections.Look(ref inputBills, "crimsonOrganicInputBills", LookMode.Value);
            Scribe_Values.Look(ref workLeft, "crimsonOrganicWorkLeft", 0f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (input == null) input = new ThingOwner<Thing>(this, false, LookMode.Deep);
                if (output == null) output = new ThingOwner<Thing>(this, false, LookMode.Deep);
                if (inputBills == null) inputBills = new List<string>();
                while (inputBills.Count < input.Count) inputBills.Add(null);
                if (inputBills.Count > input.Count) inputBills.RemoveRange(input.Count, inputBills.Count - input.Count);
                if (input.Count == 0) workLeft = 0f;
            }
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Spawned) return;
            if (output.Count != 0) output.TryDropAll(Position, Map, ThingPlaceMode.Near);
            CompPowerTrader power = this.TryGetComp<CompPowerTrader>();
            CompFlickable flick = this.TryGetComp<CompFlickable>();
            if (input.Count == 0 || output.Count != 0 || power == null || !power.PowerOn || (flick != null && !flick.SwitchIsOn)) return;
            UsedThisTick();
            // Processing time is the recipe's nominal work amount; no pawn must remain.
            float budget = delta;
            while (budget > 0f && input.Count > 0 && output.Count == 0)
            {
                float used = Math.Min(Math.Max(0f, workLeft), budget);
                workLeft -= used;
                budget -= used;
                if (workLeft > 0f) break;
                if (!CompleteFirst()) break;
            }
        }

        private bool CompleteFirst()
        {
            Corpse corpse = input[0] as Corpse;
            if (corpse == null || corpse.InnerPawn == null) return false;
            int fuel = CrimsonOrganicReconstructor.FuelYield(corpse.InnerPawn.BodySize);
            AddOutput(ThingDefOf.Chemfuel, fuel);
            AddOutput(ThingDefOf.Steel, fuel * 2);
            string billID = inputBills[0];
            input.Remove(corpse);
            inputBills.RemoveAt(0);
            workLeft = input.Count == 0 ? 0f : Recipe.workAmount;
            foreach (Bill bill in billStack.Bills)
                if (bill.GetUniqueLoadID() == billID)
                {
                    bill.Notify_IterationCompleted(null, new List<Thing> { corpse });
                    break;
                }
            corpse.Destroy(DestroyMode.Vanish);
            output.TryDropAll(Position, Map, ThingPlaceMode.Near);
            return true;
        }

        private void AddOutput(ThingDef resource, int count)
        {
            while (count > 0)
            {
                Thing product = ThingMaker.MakeThing(resource);
                product.stackCount = Math.Min(count, resource.stackLimit);
                output.TryAdd(product, false);
                count -= product.stackCount;
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode != DestroyMode.WillReplace)
            {
                input.TryDropAll(Position, Map, ThingPlaceMode.Near);
                output.TryDropAll(Position, Map, ThingPlaceMode.Near);
                if (input.Count == 0) { inputBills.Clear(); workLeft = 0f; }
            }
            base.DeSpawn(mode);
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString() + "\n자동 처리: " + (InputCount > 0 ? "1/1" : "0/1") + " · 대기: " + WaitingCount + "/10";
            if (InputCount > 0)
                text += "\n" + input[0].LabelCap + " · 진행도 " + Mathf.Clamp01(1f - workLeft / Math.Max(1f, Recipe.workAmount)).ToStringPercent();
            if (output.Count > 0) text += "\n산출물 배출 대기";
            return text;
        }
    }

    public sealed class WorkGiver_CrimsonOrganicLoad : WorkGiver_DoBill
    {
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CrimsonOrganicReconstructorQueue machine = thing as CrimsonOrganicReconstructorQueue;
            if (machine == null || machine.InputCount >= CrimsonOrganicReconstructorQueue.TotalCapacity) return null;
            // Native bill selection owns filters, search radius, pawn restrictions and ingredient reservations.
            Job native = base.JobOnThing(pawn, thing, forced);
            if (native == null || native.def != JobDefOf.DoBill || native.bill == null
                || native.targetQueueB == null || native.targetQueueB.Count != 1
                || !(native.targetQueueB[0].Thing is Corpse)) return null;
            native.def = DefDatabase<JobDef>.GetNamed("Crimson_LoadOrganicReconstructor");
            native.targetB = native.targetQueueB[0];
            native.count = 1;
            native.targetQueueB = null;
            native.countQueue = null;
            return native;
        }
    }

    public sealed class JobDriver_CrimsonOrganicLoad : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(job.targetB, job, 1, 1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil deposit = ToilMaker.MakeToil("CrimsonDepositOrganicMatter");
            deposit.initAction = delegate {
                CrimsonOrganicReconstructorQueue machine = job.targetA.Thing as CrimsonOrganicReconstructorQueue;
                Corpse corpse = pawn.carryTracker.CarriedThing as Corpse;
                if (machine == null || !machine.TryLoad(corpse, job.bill as Bill_Production))
                {
                    pawn.carryTracker.innerContainer.TryDropAll(pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            deposit.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return deposit;
        }
    }
}
