using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CrimsonSOS2
{
    public sealed class CompProperties_CrimsonBridgeDuty : CompProperties
    {
        public CompProperties_CrimsonBridgeDuty() { compClass = typeof(CompCrimsonBridgeDuty); }
    }

    public sealed class CompCrimsonBridgeDuty : ThingComp
    {
        public const string JobName = "Crimson_BridgeDuty";
        private Pawn first;
        private Pawn second;

        private Building_CrimsonPanoramicMeditationDais Dais
        { get { return parent as Building_CrimsonPanoramicMeditationDais; } }

        public int SeatCount { get { return parent.def.size.x == 6 && Dais == null ? 2 : 1; } }

        public bool InCombat
        {
            get
            {
                ShipMapComp ship = parent.Spawned ? parent.Map.GetComponent<ShipMapComp>() : null;
                return ship != null && ship.ShipMapState == ShipMapState.inCombat;
            }
        }

        public IntVec3 SeatCell(int index)
        {
            if (Dais != null) return Dais.MeditationCell;
            return parent.Position + new IntVec3(SeatCount == 1 ? 0 : index == 0 ? -1 : 2, 0, 0)
                .RotatedBy(parent.Rotation);
        }

        public Pawn Assigned(int index)
        { return Dais != null ? Dais.AssignedPawn : index == 0 ? first : second; }

        public bool Assign(int index, Pawn pawn)
        {
            if (Dais != null || index < 0 || index >= SeatCount
                || pawn != null && (pawn.Dead || !pawn.IsFreeColonist)) return false;
            if (pawn != null && SeatCount == 2 && Assigned(1 - index) == pawn) return false;
            if (index == 0) first = pawn;
            else second = pawn;
            return true;
        }

        public bool CanUse(Pawn pawn, int index)
        {
            return index >= 0 && index < SeatCount && parent.Spawned && parent.Faction == Faction.OfPlayer
                && pawn != null && pawn.IsFreeColonist && pawn.Spawned && pawn.Map == parent.Map
                && !pawn.Dead && !pawn.Downed && !pawn.Drafted && !pawn.InMentalState
                && SeatCell(index).Standable(parent.Map) && (Dais == null || Dais.Allows(pawn));
        }

        public Job MakeJob(Pawn pawn, int index, bool automatic)
        {
            if (!CanUse(pawn, index) || automatic && (!InCombat || Assigned(index) != pawn)
                || !pawn.CanReserveAndReach(SeatCell(index), PathEndMode.OnCell, pawn.NormalMaxDanger())) return null;
            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed(JobName), parent, SeatCell(index));
            job.count = automatic ? 1 : 0;
            return job;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.IsHashIntervalTick(30) || !InCombat) return;
            for (int index = 0; index < SeatCount; index++)
            {
                Pawn pawn = Assigned(index);
                if (pawn == null || pawn.CurJobDef != null && pawn.CurJobDef.defName == JobName)
                    continue;
                Job job = MakeJob(pawn, index, true);
                if (job != null) pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            if (pawn == null || !pawn.IsFreeColonist || parent.Faction != Faction.OfPlayer) yield break;
            if (Dais != null && Dais.AssignedPawn != pawn) yield break;
            for (int index = 0; index < SeatCount; index++)
            {
                int seat = index;
                string label = Dais != null ? "연단 전투 위치에서 대기"
                    : SeatCount == 1 ? "함교 좌석에 앉기" : "함교 좌석 " + (seat + 1) + "에 앉기";
                Job available = MakeJob(pawn, seat, false);
                yield return new FloatMenuOption(available == null ? label + " (사용 불가)" : label,
                    available == null ? (Action)null : delegate
                    {
                        Job job = MakeJob(pawn, seat, false);
                        if (job != null) pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    });
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Dais != null || parent.Faction != Faction.OfPlayer || !parent.Spawned) yield break;
            for (int index = 0; index < SeatCount; index++)
            {
                int seat = index;
                Pawn owner = Assigned(seat);
                yield return new Command_Action
                {
                    defaultLabel = "좌석 " + (seat + 1) + " 지정" + (owner == null ? "" : " · " + owner.LabelShort),
                    defaultDesc = "전투가 시작되면 이 좌석에 먼저 배치할 승무원을 지정합니다.",
                    icon = parent.def.uiIcon,
                    action = delegate { OpenAssignmentMenu(seat); }
                };
            }
        }

        private void OpenAssignmentMenu(int seat)
        {
            List<FloatMenuOption> choices = new List<FloatMenuOption>();
            choices.Add(new FloatMenuOption("지정 해제", delegate { Assign(seat, null); }));
            foreach (Pawn pawn in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                Pawn candidate = pawn;
                if (SeatCount == 2 && Assigned(1 - seat) == candidate) continue;
                choices.Add(new FloatMenuOption(candidate.LabelShort, delegate { Assign(seat, candidate); }));
            }
            Find.WindowStack.Add(new FloatMenu(choices));
        }

        public bool IsAtDuty(Pawn pawn, int index)
        {
            return pawn != null && CanUse(pawn, index) && pawn.Position == SeatCell(index)
                && pawn.CurJobDef != null && pawn.CurJobDef.defName == JobName && pawn.CurJob.targetA.Thing == parent
                && pawn.CurJob.targetB.Cell == pawn.Position;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref first, "crimsonFirstBridgeOperator");
            Scribe_References.Look(ref second, "crimsonSecondBridgeOperator");
        }
    }

    // The native bridge retains one manning slot. Each custom job instead
    // reserves its own cell, with the left occupied seat taking control first.
    public sealed class CrimsonBridgeMannable : CompMannable
    {
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn) { yield break; }
    }

    public sealed class JobDriver_CrimsonBridgeDuty : JobDriver
    {
        private bool servedInCombat;

        private CompCrimsonBridgeDuty Station
        {
            get
            {
                Thing target = job.targetA.Thing;
                return target == null ? null : target.TryGetComp<CompCrimsonBridgeDuty>();
            }
        }

        private int SeatIndex(CompCrimsonBridgeDuty station)
        { return station.SeatCell(0) == job.targetB.Cell ? 0 : 1; }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        { return pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed); }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref servedInCombat, "crimsonServedInCombat");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddEndCondition(delegate
            {
                CompCrimsonBridgeDuty station = Station;
                if (station == null) return JobCondition.Incompletable;
                int index = SeatIndex(station);
                if (!station.CanUse(pawn, index) || station.SeatCell(index) != job.targetB.Cell
                    || job.count == 1 && station.Assigned(index) != pawn)
                    return JobCondition.Incompletable;
                if (station.InCombat) servedInCombat = true;
                return (servedInCombat || job.count == 1) && !station.InCombat
                    ? JobCondition.Succeeded : JobCondition.Ongoing;
            });

            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            Toil hold = ToilMaker.MakeToil("CrimsonBridgeDuty");
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            hold.tickAction = delegate
            {
                CompCrimsonBridgeDuty station = Station;
                if (station == null || pawn.Position != job.targetB.Cell)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                Thing fixture = job.targetA.Thing;
                pawn.rotationTracker.FaceCell(pawn.Position + fixture.Rotation.FacingCell);
                PawnUtility.GainComfortFromThingIfPossible(pawn, fixture, 1);
                Building_CrimsonBridgeConsole console = fixture as Building_CrimsonBridgeConsole;
                if (console != null && (SeatIndex(station) == 0 || !PrimarySeatOccupied(station)))
                    console.TryGetComp<CompMannable>().ManForATick(pawn);
            };
            yield return hold;
        }

        private static bool PrimarySeatOccupied(CompCrimsonBridgeDuty station)
        {
            List<Thing> things = station.SeatCell(0).GetThingList(station.parent.Map);
            for (int i = 0; i < things.Count; i++)
                if (station.IsAtDuty(things[i] as Pawn, 0)) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_DrawTracker), "DrawPos", MethodType.Getter)]
    internal static class CrimsonBridgeSeatDrawPosition
    {
        internal static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            Job job = ___pawn.CurJob;
            if (job == null || job.def.defName != CompCrimsonBridgeDuty.JobName
                || ___pawn.Position != job.targetB.Cell || ___pawn.Drafted) return;
            Building_CrimsonBridgeConsole console = job.targetA.Thing as Building_CrimsonBridgeConsole;
            if (console != null)
                __result += console.SeatDrawShift + console.Rotation.FacingCell.ToVector3() * .4f;
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonBridgeDutyIntegration
    {
        static CrimsonBridgeDutyIntegration()
        {
            new Harmony("rimod.crimson.bridge-duty").CreateClassProcessor(typeof(CrimsonBridgeSeatDrawPosition)).Patch();
        }
    }
}
