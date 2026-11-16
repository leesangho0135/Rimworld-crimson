using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CrimsonSOS2
{
    public sealed class CrimsonRestorationSettings : DefModExtension
    {
        public int batteryDurationTicks = 60000;
        public int chargeTicks = 5000;
        public int repairTicks = 10000;
        public int resurrectionTicks = 30000;
        public float idleWatts = 100f;
        public float wattsPerRobot = 3000f;
        internal static int BatteryTicks
        {
            get
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Crimson_SingularityRestoration");
                CrimsonRestorationSettings settings = def == null ? null : def.GetModExtension<CrimsonRestorationSettings>();
                return Math.Max(1, settings == null ? 60000 : settings.batteryDurationTicks);
            }
        }
    }

    // A single deep owner preserves the actual pawn through docking, death and
    // resurrection. Three stable bay IDs share capacity for live/dead machines.
    public sealed class CrimsonRobotRestoration : Building, IThingHolder
    {
        public const int Capacity = 3;
        private ThingOwner<Thing> contents;
        private List<string> bayIDs = new List<string> { null, null, null };
        private List<float> resurrection = new List<float> { 0f, 0f, 0f };
        private List<float> repairCredit = new List<float> { 0f, 0f, 0f };
        public CrimsonRobotRestoration() { contents = new ThingOwner<Thing>(this, false, LookMode.Deep); }
        public int Occupied { get { return contents.Count; } }
        public CrimsonRestorationSettings Settings { get { return def.GetModExtension<CrimsonRestorationSettings>(); } }
        public bool Powered
        {
            get
            {
                CompPowerTrader power = this.TryGetComp<CompPowerTrader>();
                CompFlickable flick = this.TryGetComp<CompFlickable>();
                return Spawned && power != null && power.PowerOn && (flick == null || flick.SwitchIsOn);
            }
        }
        public ThingOwner GetDirectlyHeldThings() { return contents; }
        public void GetChildHolders(List<IThingHolder> holders)
        { ThingOwnerUtility.AppendThingHoldersFromThings(holders, contents); }
        private static Pawn RobotPawn(Thing thing) { return thing is Corpse ? ((Corpse)thing).InnerPawn : thing as Pawn; }
        public static bool Eligible(Thing thing)
        {
            Pawn pawn = RobotPawn(thing);
            return thing != null && !thing.Destroyed && pawn != null && !pawn.Discarded
                && pawn.Faction == Faction.OfPlayer && CrimsonShipRobot.IsRobot(pawn)
                && ((thing is Corpse && pawn.Dead) || (thing is Pawn && !pawn.Dead));
        }
        public static bool NeedsHauling(Thing thing)
        {
            if (!Eligible(thing)) return false;
            Pawn pawn = RobotPawn(thing);
            return thing is Corpse || pawn.Downed || !pawn.TryGetComp<CrimsonShipRobot>().HasEnergy;
        }
        internal static bool NeedsService(Pawn pawn)
        { return pawn.TryGetComp<CrimsonShipRobot>().BatteryLevel <= .2f || MechRepairUtility.CanRepair(pawn); }

        internal static CrimsonRobotRestoration FindStation(Pawn pawn, Thing item)
        {
            if (pawn == null || !pawn.Spawned) return null;
            ThingDef definition = DefDatabase<ThingDef>.GetNamed("Crimson_SingularityRestoration");
            return pawn.Map.listerThings.ThingsOfDef(definition).OfType<CrimsonRobotRestoration>()
                .Where(s => s.Faction == Faction.OfPlayer && s.Powered && s.Occupied < Capacity && !s.IsForbidden(pawn)
                    && CrimsonShipRobotControl.Allowed(pawn, s.InteractionCell)
                    && pawn.CanReserveAndReach(s, PathEndMode.InteractionCell, Danger.Some, 3, 1))
                .OrderBy(s => s.Position.DistanceToSquared(pawn.Position)).FirstOrDefault();
        }

        public Thing InBay(int bay)
        {
            string id = bayIDs[bay];
            return id == null ? null : contents.FirstOrDefault(t => t.GetUniqueLoadID() == id);
        }
        public bool TryDock(Thing thing)
        {
            if (!Powered || contents.Count >= Capacity || !Eligible(thing) || contents.Contains(thing)) return false;
            Pawn pawn = RobotPawn(thing);
            pawn.TryGetComp<CrimsonShipRobot>().NormalizeLegacy(pawn);
            int bay = bayIDs.FindIndex(id => id == null);
            if (bay < 0) return false;
            bool wasSpawned = thing.Spawned;
            IntVec3 oldPosition = thing.Position;
            Map oldMap = thing.Map;
            if (wasSpawned) thing.DeSpawn();
            if (!contents.TryAddOrTransfer(thing, false))
            {
                if (wasSpawned && !thing.Spawned) GenSpawn.Spawn(thing, oldPosition, oldMap);
                return false;
            }
            bayIDs[bay] = thing.GetUniqueLoadID();
            resurrection[bay] = 0f;
            repairCredit[bay] = 0f;
            if (pawn.drafter != null) pawn.drafter.Drafted = false;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref contents, "crimsonRestorationContents", this);
            Scribe_Collections.Look(ref bayIDs, "crimsonRestorationBayIDs", LookMode.Value);
            Scribe_Collections.Look(ref resurrection, "crimsonResurrectionProgress", LookMode.Value);
            Scribe_Collections.Look(ref repairCredit, "crimsonRepairCredit", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (contents == null) contents = new ThingOwner<Thing>(this, false, LookMode.Deep);
                if (bayIDs == null) bayIDs = new List<string>();
                if (resurrection == null) resurrection = new List<float>();
                if (repairCredit == null) repairCredit = new List<float>();
                while (bayIDs.Count < Capacity) bayIDs.Add(null);
                while (resurrection.Count < Capacity) resurrection.Add(0f);
                while (repairCredit.Count < Capacity) repairCredit.Add(0f);
                for (int i = 0; i < Capacity; i++) if (InBay(i) == null) bayIDs[i] = null;
                foreach (Thing item in contents)
                    if (!bayIDs.Contains(item.GetUniqueLoadID()))
                    {
                        int free = bayIDs.FindIndex(id => id == null);
                        if (free >= 0) bayIDs[free] = item.GetUniqueLoadID();
                    }
            }
        }

        public override void TickRare()
        {
            base.TickRare();
            CompPowerTrader power = this.TryGetComp<CompPowerTrader>();
            int processing = contents.Count(t => t is Corpse || NeedsServiceFully(RobotPawn(t)));
            if (power != null) power.PowerOutput = -(Settings.idleWatts + Settings.wattsPerRobot * processing);
            if (!Powered) return;
            for (int bay = 0; bay < Capacity; bay++)
            {
                Thing thing = InBay(bay);
                if (thing == null) { bayIDs[bay] = null; continue; }
                Corpse corpse = thing as Corpse;
                if (corpse != null)
                {
                    resurrection[bay] += 250f / Math.Max(1, Settings.resurrectionTicks);
                    if (resurrection[bay] < 1f) continue;
                    Pawn dead = corpse.InnerPawn;
                    dead.TryGetComp<CrimsonShipRobot>().NormalizeLegacy(dead);
                    // Native resurrection uses the same pawn and destroys its
                    // old corpse. dontSpawn keeps it inside its assigned bay.
                    if (!ResurrectionUtility.TryResurrect(dead, new ResurrectionParams {
                        restoreMissingParts = true, dontSpawn = true, noLord = true, gettingScarsChance = 0f })) continue;
                    if (!contents.TryAddOrTransfer(dead, false))
                    {
                        GenSpawn.Spawn(dead, InteractionCell, Map);
                        bayIDs[bay] = null;
                        continue;
                    }
                    bayIDs[bay] = dead.GetUniqueLoadID();
                    dead.TryGetComp<CrimsonShipRobot>().SetBattery(0f);
                    resurrection[bay] = 0f;
                    continue;
                }
                Pawn robot = thing as Pawn;
                if (robot == null) continue;
                CrimsonShipRobot comp = robot.TryGetComp<CrimsonShipRobot>();
                comp.SetBattery(comp.BatteryLevel + 250f / Math.Max(1, Settings.chargeTicks));
                if (MechRepairUtility.CanRepair(robot))
                {
                    float maximumHealth = robot.RaceProps.body.AllParts.Sum(part => part.def.GetMaxHealth(robot));
                    repairCredit[bay] += maximumHealth * 250f / Math.Max(1, Settings.repairTicks);
                    int points = Mathf.FloorToInt(repairCredit[bay]);
                    while (points-- > 0 && MechRepairUtility.CanRepair(robot))
                    {
                        MechRepairUtility.RepairTick(robot);
                        // Held pawns do not run the ordinary health tick which
                        // removes zero-severity injuries after native Heal().
                        // Complete that same cleanup here, so a repaired pawn
                        // is no longer selected as damaged forever.
                        foreach (Hediff_Injury healed in robot.health.hediffSet.hediffs
                            .OfType<Hediff_Injury>().Where(injury => injury.ShouldRemove).ToList())
                            robot.health.RemoveHediff(healed);
                        repairCredit[bay] -= 1f;
                    }
                }
                else repairCredit[bay] = 0f;
                if (!NeedsServiceFully(robot)) TryRelease(bay);
            }
        }
        private static bool NeedsServiceFully(Pawn robot)
        { return robot == null || robot.TryGetComp<CrimsonShipRobot>().BatteryLevel < .99999f || MechRepairUtility.CanRepair(robot); }

        public bool TryRelease(int bay)
        {
            Thing item = InBay(bay);
            if (!Spawned || item == null) return false;
            Thing dropped;
            if (!contents.TryDrop(item, InteractionCell, Map, ThingPlaceMode.Near, 1, out dropped)) return false;
            bayIDs[bay] = null; resurrection[bay] = 0f; repairCredit[bay] = 0f;
            Pawn pawn = dropped as Pawn;
            if (pawn != null) CrimsonShipRobotControl.EnsureControls(pawn);
            return true;
        }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Spawned) contents.TryDropAll(Position, Map, ThingPlaceMode.Near);
            base.DeSpawn(mode);
        }
        public override string GetInspectString()
        {
            string text = base.GetInspectString() + "\n기체 " + Occupied + "/3 · " + (Powered ? "충전·정비·복원 가동" : "전원 없음: 처리 중지");
            for (int bay = 0; bay < Capacity; bay++)
            {
                Thing thing = InBay(bay);
                if (thing == null) continue;
                Pawn pawn = RobotPawn(thing);
                text += "\n" + (bay + 1) + ". " + pawn.LabelShortCap + " · " + (thing is Corpse
                    ? "기체 복원 " + Mathf.Clamp01(resurrection[bay]).ToStringPercent()
                    : "충전 " + pawn.TryGetComp<CrimsonShipRobot>().BatteryLevel.ToStringPercent()
                        + (MechRepairUtility.CanRepair(pawn) ? " · 손상 정비 중" : " · 정비 완료"));
            }
            return text.Trim();
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (Faction != Faction.OfPlayer) yield break;
            for (int bay = 0; bay < Capacity; bay++)
            {
                Thing thing = InBay(bay);
                if (thing == null) continue;
                int selected = bay;
                yield return new Command_Action {
                    defaultLabel = (bay + 1) + ". " + RobotPawn(thing).LabelShortCap + " 내보내기",
                    defaultDesc = "선택한 기체를 내보냅니다. 보존된 진행도는 이 도킹을 끝내면 초기화됩니다.",
                    icon = thing.def.uiIcon, action = delegate { TryRelease(selected); }
                };
            }
        }
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn)) yield return option;
            if (!CrimsonShipRobotControl.OwnRobot(selPawn)) yield break;
            if (!Powered || Occupied >= Capacity)
                yield return new FloatMenuOption("특이점 복원소 이용 불가: 전원 또는 빈 자리 필요", null);
            else if (!selPawn.TryGetComp<CrimsonShipRobot>().HasEnergy)
                yield return new FloatMenuOption("방전된 기체는 운반해야 합니다", null);
            else yield return new FloatMenuOption("특이점 복원소에서 충전·정비", delegate {
                selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Crimson_RobotDock"), this));
            });
        }
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            Graphic graphic = DefDatabase<PawnKindDef>.GetNamed("Crimson_ShipRobotOmni").lifeStages[0].bodyGraphicData.Graphic;
            for (int bay = 0; bay < Capacity; bay++)
            {
                if (InBay(bay) == null) continue;
                Vector3 offset = new Vector3((bay - 1) * .92f, 0f, -.27f).RotatedBy(Rotation.AsAngle);
                Vector3 location = drawLoc + offset;
                location.y = AltitudeLayer.BuildingOnTop.AltitudeFor() + .02f;
                Graphics.DrawMesh(MeshPool.GridPlane(new Vector2(.96f,.64f)), location,
                    Quaternion.identity, graphic.MatAt(Rotation.Opposite), 0);
            }
        }
    }

    public sealed class JobGiver_CrimsonRobotService : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            CrimsonShipRobot robot = pawn.TryGetComp<CrimsonShipRobot>();
            if (robot == null || !robot.Connected || !robot.HasEnergy || pawn.Drafted || !CrimsonRobotRestoration.NeedsService(pawn)) return null;
            CrimsonRobotRestoration station = CrimsonRobotRestoration.FindStation(pawn, pawn);
            return station == null ? null : JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Crimson_RobotDock"), station);
        }
    }
    public sealed class JobDriver_CrimsonRobotDock : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        { return pawn.Reserve(job.targetA, job, 3, 1, null, errorOnFailed); }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(delegate { CrimsonRobotRestoration s = job.targetA.Thing as CrimsonRobotRestoration; return s == null || !s.Powered || s.Occupied >= 3; });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil dock = ToilMaker.MakeToil("CrimsonRobotDock");
            dock.initAction = delegate { if (!((CrimsonRobotRestoration)job.targetA.Thing).TryDock(pawn)) EndJobWith(JobCondition.Incompletable); };
            dock.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return dock;
        }
    }
    public sealed class WorkGiver_CrimsonRobotRecovery : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode { get { return PathEndMode.ClosestTouch; } }
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Thing thing in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                if (CrimsonRobotRestoration.NeedsHauling(thing)) yield return thing;
            foreach (Pawn robot in pawn.Map.mapPawns.AllPawnsSpawned)
                if (robot != pawn && CrimsonRobotRestoration.NeedsHauling(robot)) yield return robot;
        }
        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            return thing != pawn && CrimsonRobotRestoration.NeedsHauling(thing) && !thing.IsForbidden(pawn)
                && CrimsonShipRobotControl.Allowed(pawn, thing.Position)
                && pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Some)
                && CrimsonRobotRestoration.FindStation(pawn, thing) != null;
        }
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CrimsonRobotRestoration station = CrimsonRobotRestoration.FindStation(pawn, thing);
            if (station == null) return null;
            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Crimson_RobotRecover"), station, thing);
            job.count = 1; return job;
        }
    }
    public sealed class JobDriver_CrimsonRobotRecover : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        { return pawn.Reserve(job.targetA, job, 3, 1, null, errorOnFailed) && pawn.Reserve(job.targetB, job, 1, 1, null, errorOnFailed); }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil deposit = ToilMaker.MakeToil("CrimsonRecoverRobot");
            deposit.initAction = delegate {
                CrimsonRobotRestoration station = job.targetA.Thing as CrimsonRobotRestoration;
                if (station == null || !station.TryDock(pawn.carryTracker.CarriedThing))
                {
                    pawn.carryTracker.innerContainer.TryDropAll(pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            deposit.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return deposit;
        }
    }

    public sealed class CrimsonRestorationGraphic : Graphic_Single
    {
        private readonly Material[] frames = new Material[4];
        private static readonly Rect[] windows = { new Rect(113,73,546,362), new Rect(979,19,346,465),
            new Rect(116,590,539,349), new Rect(979,532,348,467) };
        public override bool ShouldDrawRotated { get { return false; } }
        public override Material MatSingle { get { return frames[2] ?? mat; } }
        public override Material MatNorth { get { return frames[0] ?? mat; } }
        public override Material MatEast { get { return frames[1] ?? mat; } }
        public override Material MatSouth { get { return frames[2] ?? mat; } }
        public override Material MatWest { get { return frames[3] ?? mat; } }
        public override Material MatAt(Rot4 rot, Thing thing = null) { return frames[rot.AsInt] ?? mat; }
        public override Material MatSingleFor(Thing thing) { return thing == null ? MatSingle : MatAt(thing.Rotation,thing); }
        public override Mesh MeshAt(Rot4 rot) { return MeshPool.GridPlane(rot.IsHorizontal ? new Vector2(2,3) : new Vector2(3,2)); }
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            Texture2D source = ContentFinder<Texture2D>.Get(path);
            for (int i = 0; i < 4; i++)
            {
                Rect r = windows[i];
                Texture2D frame = CrimsonV51FurnitureGraphic.NativeFrame(source, new Vector2(r.width/source.width,r.height/source.height),
                    new Vector2(r.x/source.width,1f-r.yMax/source.height), i);
                frames[i] = MaterialPool.MatFrom(frame,request.shader,request.color);
            }
        }
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this,loc,rot,thingDef,thing,0f); }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Print(layer,thing,this,thing.Rotation.IsHorizontal ? new Vector2(2,3) : new Vector2(3,2),0f); }
        public override Graphic GetColoredVersion(Shader shader, Color first, Color second)
        { return GraphicDatabase.Get<CrimsonRestorationGraphic>(path,shader,drawSize,first,second,data); }
    }
}
