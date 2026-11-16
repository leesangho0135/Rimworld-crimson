using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CrimsonSOS2
{
    public enum CrimsonRobotRole { Omni, Rescue, Militor }

    public sealed class CompProperties_CrimsonShipRobot : CompProperties
    {
        public CrimsonRobotRole role;
        public CompProperties_CrimsonShipRobot() { compClass = typeof(CrimsonShipRobot); }
    }

    public sealed class CrimsonShipRobot : ThingComp
    {
        private Thing controller;
        private int nextControllerCheck;
        private bool workPrepared;
        private float batteryLevel = 1f;
        private bool dropLegacyWeapons;
        public float BatteryLevel { get { return batteryLevel; } }
        public bool HasEnergy { get { return batteryLevel > 0f; } }
        internal void SetBattery(float value)
        {
            batteryLevel = Mathf.Clamp01(value);
            Pawn pawn = parent as Pawn;
            Need_MechEnergy need = pawn == null || pawn.needs == null ? null : pawn.needs.TryGetNeed<Need_MechEnergy>();
            if (need != null) need.CurLevelPercentage = batteryLevel;
        }
        public CrimsonRobotRole Role { get { return ((CompProperties_CrimsonShipRobot)props).role; } }
        internal static bool IsRobot(Pawn pawn)
        { return pawn != null && pawn.def.GetCompProperties<CompProperties_CrimsonShipRobot>() != null; }
        internal static bool ControllerReady(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Destroyed || thing.Faction != Faction.OfPlayer) return false;
            CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
            return power == null || power.PowerOn;
        }
        internal static Thing FindController(Map map)
        {
            if (map == null) return null;
            foreach (string name in new[] { "Crimson_Observer", "Ship_DroneCore" })
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                foreach (Building building in map.listerBuildings.AllBuildingsColonistOfDef(def))
                    if (ControllerReady(building)) return building;
            }
            return null;
        }
        public bool Connected
        {
            get
            {
                if (!parent.Spawned) return false;
                if (controller != null && controller.Map == parent.Map && ControllerReady(controller)) return true;
                if (Find.TickManager.TicksGame < nextControllerCheck) return false;
                nextControllerCheck = Find.TickManager.TicksGame + 250;
                controller = FindController(parent.Map);
                return controller != null;
            }
        }
        internal void Bind(Thing ai) { controller = ai; nextControllerCheck = 0; }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            NormalizeLegacy(parent as Pawn);
            Pawn spawned = parent as Pawn;
            if (dropLegacyWeapons && spawned != null && spawned.Spawned && spawned.equipment != null)
            { spawned.equipment.DropAllEquipment(spawned.Position, false, false); dropLegacyWeapons = false; }
            CrimsonShipRobotControl.EnsureControls(parent as Pawn);
            SetBattery(batteryLevel);
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            Pawn pawn = parent as Pawn;
            if (CrimsonShipRobotControl.OwnRobot(pawn) && pawn.Spawned)
                yield return CrimsonShipRobotControl.AreaCommand(pawn);
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref controller, "crimsonShipAI");
            Scribe_Values.Look(ref batteryLevel, "crimsonRobotBattery", 1f);
            Scribe_Values.Look(ref dropLegacyWeapons, "crimsonDropLegacyWeapons", false);
        }
        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned) return;
            // Keep facing in sync with native movement and job targets even
            // when visual post-tick processing is deferred for this pawn.
            if (pawn.rotationTracker != null) pawn.rotationTracker.UpdateRotation();
            if (!parent.IsHashIntervalTick(250)) return;
            if (Connected) SetBattery(batteryLevel - 250f / CrimsonRestorationSettings.BatteryTicks);
            if ((!Connected || !HasEnergy) && pawn.Drafted) pawn.drafter.Drafted = false;
            if ((!Connected || !HasEnergy) && pawn.CurJob != null && pawn.CurJob.def != JobDefOf.Wait)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
        }
        public override string CompInspectStringExtra()
        { return "배터리 " + batteryLevel.ToStringPercent() + " · " + (Connected ? "함선 통제망 연결" : "목격자 연결 없음")
                + (HasEnergy ? "\n특이점 복원소에서 충전·자가 수리" : "\n방전: 특이점 복원소로 운반 필요"); }

        internal void NormalizeLegacy(Pawn pawn)
        {
            if (pawn == null || pawn.Faction != Faction.OfPlayer || Role == CrimsonRobotRole.Omni) return;
            // All three old models share Mech_Light. Preserve the actual pawn,
            // body-part records, name, player settings and ship AI reference.
            Map map = pawn.Spawned ? pawn.Map : null;
            if (map != null) map.listerThings.Remove(pawn);
            pawn.def = DefDatabase<ThingDef>.GetNamed("Crimson_ShipRobotOmni");
            pawn.kindDef = DefDatabase<PawnKindDef>.GetNamed("Crimson_ShipRobotOmni");
            props = pawn.def.GetCompProperties<CompProperties_CrimsonShipRobot>();
            if (map != null) map.listerThings.Add(pawn);
            workPrepared = false;
            pawn.Notify_DisabledWorkTypesChanged();
            dropLegacyWeapons = true;
            if (pawn.Spawned && pawn.equipment != null)
            { pawn.equipment.DropAllEquipment(pawn.Position, false, false); dropLegacyWeapons = false; }
            if (pawn.Drawer != null && pawn.Drawer.renderer != null) pawn.Drawer.renderer.SetAllGraphicsDirty();
        }

        internal void PrepareWork(Pawn pawn)
        {
            if (Role == CrimsonRobotRole.Militor || (workPrepared && pawn.workSettings != null && pawn.skills != null)) return;
            if (pawn.workSettings == null) pawn.workSettings = new Pawn_WorkSettings(pawn);
            if (pawn.skills == null) pawn.skills = new Pawn_SkillTracker(pawn);
            foreach (SkillRecord skill in pawn.skills.skills) skill.Level = pawn.RaceProps.mechFixedSkillLevel;
            pawn.workSettings.EnableAndInitialize();
            foreach (WorkTypeDef work in pawn.RaceProps.mechEnabledWorkTypes)
            {
                int priority = work.defName == "Firefighter" ? 1 : work.defName == "Doctor" ? 2
                    : work.defName == "Hauling" || work.defName == "Cleaning" ? 4 : 3;
                pawn.workSettings.SetPriority(work, priority);
            }
            workPrepared = true;
        }
    }

    public sealed class ThinkNode_ConditionalCrimsonShipAI : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            CrimsonShipRobot robot = pawn.TryGetComp<CrimsonShipRobot>();
            return robot != null && robot.Connected && robot.HasEnergy && !pawn.Drafted;
        }
    }

    public sealed class JobGiver_CrimsonRobotWork : JobGiver_Work
    {
        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            CrimsonShipRobot robot = pawn.TryGetComp<CrimsonShipRobot>();
            if (robot == null || !robot.Connected || !robot.HasEnergy || pawn.Drafted || robot.Role == CrimsonRobotRole.Militor) return ThinkResult.NoJob;
            robot.PrepareWork(pawn);
            return base.TryIssueJobPackage(pawn, jobParams);
        }
    }

    public sealed class JobGiver_CrimsonRobotRescue : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            CrimsonShipRobot robot = pawn.TryGetComp<CrimsonShipRobot>();
            if (robot == null || robot.Role != CrimsonRobotRole.Rescue || !robot.Connected || pawn.Drafted) return null;
            robot.PrepareWork(pawn);
            WorkGiverDef def = DefDatabase<WorkGiverDef>.GetNamed("DoctorRescue");
            WorkGiver_Scanner rescue = (WorkGiver_Scanner)def.Worker;
            foreach (Pawn target in pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != pawn && p.Downed && p.Faction == pawn.Faction && CrimsonShipRobotControl.Allowed(pawn, p.Position))
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position)))
                if (rescue.HasJobOnThing(pawn, target, false))
                {
                    Job job = rescue.JobOnThing(pawn, target, false);
                    if (job != null && (!job.targetB.IsValid || CrimsonShipRobotControl.Allowed(pawn, job.targetB.Cell))) return job;
                }
            return null;
        }
    }

    public sealed class JobGiver_CrimsonRobotWait : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) { return JobMaker.MakeJob(JobDefOf.Wait, 250); }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonShipRobotRegistration
    {
        static CrimsonShipRobotRegistration()
        {
            // Ship control remains independent from a human mechanitor. Battery
            // storage is owned by this component and mirrored to the native need.
            // Original mechanoids and their charging/control systems are untouched.
            Harmony harmony = new Harmony("rimod.crimson.sos2.ship-robots");
            harmony.Patch(
                AccessTools.Method(typeof(Pawn_NeedsTracker), "ShouldHaveNeed"),
                prefix: new HarmonyMethod(typeof(CrimsonShipRobotRegistration), "WirelessEnergy"));
            harmony.Patch(AccessTools.Method(typeof(Need_MechEnergy), "NeedInterval"),
                prefix: new HarmonyMethod(typeof(CrimsonShipRobotRegistration), "BatteryInterval"));
            // Programmed robots have fixed skills and no human story/traits.
            // Native skill decay and learning assume that a story tracker exists.
            harmony.Patch(AccessTools.Method(typeof(SkillRecord), "Interval"),
                prefix: new HarmonyMethod(typeof(CrimsonShipRobotRegistration), "ProgrammedSkill"));
            harmony.Patch(AccessTools.Method(typeof(SkillRecord), "Learn", new[] { typeof(float), typeof(bool), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(CrimsonShipRobotRegistration), "ProgrammedSkill"));
            foreach (string role in new[] { "Omni", "Rescue", "Militor" })
            {
                ThingDef crate = DefDatabase<ThingDef>.GetNamedSilentFail("Crimson_ShipRobot" + role + "Crate");
                if (crate != null) { crate.uiIcon = MakeIcon("Omni"); crate.uiIconMaterial = null; }
            }
        }
        private static Texture2D MakeIcon(string role)
        {
            Texture2D source = ContentFinder<Texture2D>.Get("Crimson/Robots/" + role + "Atlas");
            RenderTexture previous = RenderTexture.active;
            RenderTexture inner = RenderTexture.GetTemporary(240, 160, 0, RenderTextureFormat.ARGB32);
            RenderTexture canvas = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(source, inner, new Vector2(.5f, .5f), Vector2.zero);
                RenderTexture.active = canvas;
                GL.Clear(true, true, Color.clear);
                Graphics.CopyTexture(inner, 0, 0, 0, 0, 240, 160, canvas, 0, 0, 8, 48);
                Texture2D icon = new Texture2D(256, 256, TextureFormat.ARGB32, false);
                icon.name = "CrimsonShipRobot_" + role;
                icon.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                icon.Apply(false, true);
                return icon;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(inner);
                RenderTexture.ReleaseTemporary(canvas);
            }
        }
        private static bool WirelessEnergy(Pawn ___pawn, NeedDef __0, ref bool __result)
        {
            if (!CrimsonShipRobot.IsRobot(___pawn) || __0.defName != "MechEnergy") return true;
            __result = true;
            return false;
        }
        private static bool BatteryInterval(Need_MechEnergy __instance, Pawn ___pawn)
        {
            CrimsonShipRobot robot = ___pawn == null ? null : ___pawn.TryGetComp<CrimsonShipRobot>();
            if (robot == null) return true;
            __instance.CurLevelPercentage = robot.BatteryLevel;
            return false;
        }
        private static bool ProgrammedSkill(Pawn ___pawn) { return !CrimsonShipRobot.IsRobot(___pawn); }
    }

    public sealed class CrimsonRobotCrateExtension : DefModExtension
    { public PawnKindDef pawnKind; }

    public sealed class CrimsonRobotCrate : ThingWithComps
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!Spawned) yield break;
            Command_Action command = new Command_Action {
                defaultLabel = "목격자에 연결하여 가동",
                defaultDesc = "목격자 또는 기존 함선 드론 코어에 작업로봇을 연결합니다. 특이점 복원소에서 충전·수리·기체 복원을 수행합니다.",
                icon = def.uiIcon, action = Activate };
            if (CrimsonShipRobot.FindController(Map) == null) command.Disable("가동 중인 목격자 또는 함선 드론 코어가 필요합니다.");
            yield return command;
        }
        private void Activate()
        {
            if (!Spawned) return;
            Thing ai = CrimsonShipRobot.FindController(Map);
            if (ai == null) return;
            IntVec3 location;
            if (!CellFinder.TryFindRandomCellNear(Position, Map, 3,
                c => c.Standable(Map) && c.GetFirstPawn(Map) == null, out location)) return;
            Pawn pawn = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Crimson_ShipRobotOmni"), Faction.OfPlayer);
            GenSpawn.Spawn(pawn, location, Map);
            pawn.TryGetComp<CrimsonShipRobot>().Bind(ai);
            Destroy(DestroyMode.Vanish);
        }
    }

    public class CrimsonRobotGraphic : Graphic_Multi
    {
        private readonly Material[] views = new Material[4];
        private static readonly Dictionary<string, Texture2D[]> frameTextures = new Dictionary<string, Texture2D[]>();
        public override Material MatSingle { get { return MatSouth; } }
        public override Material MatNorth { get { return views[0]; } }
        public override Material MatEast { get { return views[1]; } }
        public override Material MatSouth { get { return views[2]; } }
        public override Material MatWest { get { return views[3]; } }
        public override bool WestFlipped { get { return false; } }
        public override bool EastFlipped { get { return false; } }
        public override bool ShouldDrawRotated { get { return false; } }
        public override Material MatAt(Rot4 rot, Thing thing = null) { return views[rot.AsInt]; }
        public override void TryInsertIntoAtlas(TextureAtlasGroup group) { }
        public override void Init(GraphicRequest request)
        {
            path = request.path; data = request.graphicData; drawSize = request.drawSize;
            color = request.color; colorTwo = request.colorTwo;
            Texture2D[] frames;
            if (!frameTextures.TryGetValue(path, out frames))
            {
                Texture2D atlas = ContentFinder<Texture2D>.Get(path);
                Rect[] windows = Windows(path);
                frames = new Texture2D[4];
                for (int i = 0; i < frames.Length; i++) frames[i] = BakeFrame(atlas, windows[i], path + "_" + i);
                frameTextures.Add(path, frames);
            }
            for (int i = 0; i < 4; i++)
                views[i] = MaterialPool.MatFrom(frames[i], request.shader, color);
        }
        public override Graphic GetColoredVersion(Shader shader, Color first, Color second)
        { return GraphicDatabase.Get<CrimsonRobotGraphic>(path, shader, drawSize, first, second, data); }

        // Pawn rendering can replace the material's texture transform. Give it
        // one complete directional texture instead of an atlas plus UV offsets.
        private static Texture2D BakeFrame(Texture2D atlas, Rect window, string name)
        {
            int width = Mathf.RoundToInt(window.width), height = Mathf.RoundToInt(window.height);
            Vector2 scale = new Vector2(window.width / atlas.width, window.height / atlas.height);
            Vector2 offset = new Vector2(window.x / atlas.width, 1f - window.yMax / atlas.height);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(atlas, target, scale, offset);
                RenderTexture.active = target;
                Texture2D result = new Texture2D(width, height, TextureFormat.ARGB32, false);
                result.name = name; result.wrapMode = TextureWrapMode.Clamp; result.filterMode = FilterMode.Bilinear;
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0); result.Apply(false, true);
                return result;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        // Fixed atlas cells and a 3:2 draw rectangle preserve source aspect and
        // consistent character scale across independently constructed views.
        private static Rect[] Windows(string path)
        {
            return new[] { new Rect(0,0,768,512), new Rect(768,0,768,512),
                new Rect(0,512,768,512), new Rect(768,512,768,512) };
        }
    }

    public sealed class CrimsonRobotCrateGraphic : Graphic_Single
    {
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            Graphic graphic = GraphicDatabase.Get<CrimsonRobotGraphic>(request.path, request.shader, request.drawSize, request.color);
            mat = graphic.MatSouth;
        }
    }
}
