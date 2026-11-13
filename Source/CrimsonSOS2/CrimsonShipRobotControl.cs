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
    // Ship robots use the public mech order/area interfaces while their existing
    // ship-controller component remains the authority. No overseer pawn is made.
    [StaticConstructorOnStartup]
    internal static class CrimsonShipRobotControl
    {
        static CrimsonShipRobotControl()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.ship-robot-control");
            Postfix(harmony, AccessTools.PropertyGetter(typeof(Pawn), "IsColonyMechPlayerControlled"), "PlayerControlled");
            Prefix(harmony, AccessTools.Method(typeof(MechanitorUtility), "CanDraftMech"), "CanDraft");
            Prefix(harmony, AccessTools.Method(typeof(MechanitorUtility), "InMechanitorCommandRange"), "CommandRange");
            Prefix(harmony, AccessTools.Method(typeof(MechanitorUtility), "GetMechGizmos"), "NativeMechGizmos");
            Prefix(harmony, AccessTools.Method(typeof(MechanitorUtility), "GetMechWorkMode"), "WorkMode");
            Prefix(harmony, AccessTools.Method(typeof(MechanitorUtility), "IsColonyMechRequiringMechanitor"), "RequiresMechanitor");
            Postfix(harmony, AccessTools.PropertyGetter(typeof(Pawn_DraftController), "ShowDraftGizmo"), "ShowDraft");
            Postfix(harmony, AccessTools.Method(typeof(PawnComponentsUtility), "AddAndRemoveDynamicComponents"), "ComponentsReady");
            Postfix(harmony, AccessTools.PropertyGetter(typeof(MainTabWindow_Mechs), "Pawns"), "MechTablePawns");
            Prefix(harmony, AccessTools.Method(typeof(PawnColumnWorker_AllowedArea), "DoCell"), "AreaCell");
            Prefix(harmony, AccessTools.Method(typeof(PawnColumnWorker_Overseer), "DoCell"), "OverseerCell");
            Prefix(harmony, AccessTools.Method(typeof(PawnColumnWorker_ControlGroup), "DoCell"), "GroupCell");
            Prefix(harmony, AccessTools.Method(typeof(PawnColumnWorker_WorkMode), "DoCell"), "ModeCell");
            Prefix(harmony, AccessTools.Method(typeof(PawnColumnWorker_Energy), "DoCell"), "EnergyCell");
            Postfix(harmony, AccessTools.PropertySetter(typeof(Pawn_PlayerSettings), "AreaRestrictionInPawnCurrentMap"), "AreaChanged");
        }

        private static void Prefix(Harmony harmony, System.Reflection.MethodBase method, string name)
        { harmony.Patch(method, prefix: new HarmonyMethod(typeof(CrimsonShipRobotControl), name)); }
        private static void Postfix(Harmony harmony, System.Reflection.MethodBase method, string name)
        { harmony.Patch(method, postfix: new HarmonyMethod(typeof(CrimsonShipRobotControl), name)); }

        internal static bool OwnRobot(Pawn pawn)
        { return pawn != null && CrimsonShipRobot.IsRobot(pawn) && pawn.Faction == Faction.OfPlayer; }

        internal static void EnsureControls(Pawn pawn)
        {
            if (!OwnRobot(pawn)) return;
            if (pawn.playerSettings == null) pawn.playerSettings = new Pawn_PlayerSettings(pawn);
            if (pawn.drafter == null) pawn.drafter = new Pawn_DraftController(pawn);
        }

        internal static bool Allowed(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || !pawn.Spawned || !cell.InBounds(pawn.Map)) return false;
            Area area = pawn.playerSettings == null ? null : pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap;
            return area == null || area[cell];
        }

        internal static Command_Action AreaCommand(Pawn pawn)
        {
            EnsureControls(pawn);
            Area area = pawn.playerSettings.AreaRestrictionInPawnCurrentMap;
            return new Command_Action
            {
                defaultLabel = "허용 구역: " + (area == null ? "무제한" : area.Label),
                defaultDesc = "함선 로봇이 자율 작업과 배회를 수행할 구역을 선택합니다. 징집 중 직접 내린 명령은 우선합니다.",
                icon = ContentFinder<Texture2D>.Get("UI/Designators/AreaAllowed", false) ?? TexCommand.ForbidOff,
                action = delegate
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    options.Add(new FloatMenuOption("무제한", delegate { SetArea(pawn, null); }));
                    foreach (Area candidate in pawn.Map.areaManager.AllAreas)
                    {
                        if (!candidate.AssignableAsAllowed()) continue;
                        Area selected = candidate;
                        options.Add(new FloatMenuOption(selected.Label, delegate { SetArea(pawn, selected); }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }

        private static void SetArea(Pawn pawn, Area area)
        {
            if (!OwnRobot(pawn) || !pawn.Spawned) return;
            EnsureControls(pawn);
            pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
        }

        private static void PlayerControlled(Pawn __instance, ref bool __result)
        {
            if (!OwnRobot(__instance)) return;
            __result = __instance.Spawned && __instance.IsColonyMech
                && __instance.TryGetComp<CrimsonShipRobot>().Connected;
        }

        private static bool CanDraft(Pawn __0, ref AcceptanceReport __result)
        {
            if (!OwnRobot(__0)) return true;
            if (!__0.Spawned || __0.Dead || __0.Downed || __0.InMentalState)
                __result = "현재 징집할 수 없는 상태입니다.";
            else if (!__0.TryGetComp<CrimsonShipRobot>().Connected)
                __result = "가동 중인 목격자 또는 함선 드론 코어가 필요합니다.";
            else __result = true;
            return false;
        }

        private static bool CommandRange(Pawn __0, LocalTargetInfo __1, ref bool __result)
        {
            if (!OwnRobot(__0)) return true;
            __result = __0.Spawned && __0.TryGetComp<CrimsonShipRobot>().Connected
                && __1.IsValid && __1.Cell.InBounds(__0.Map)
                && (!__1.HasThing || __1.Thing.MapHeld == __0.Map);
            return false;
        }

        private static bool NativeMechGizmos(Pawn __0, ref IEnumerable<Gizmo> __result)
        {
            if (!OwnRobot(__0)) return true;
            // Native draft/fire-at-will commands are supplied by the drafter.
            // Human-overseer group and bandwidth commands do not apply here.
            __result = Enumerable.Empty<Gizmo>();
            return false;
        }

        private static bool WorkMode(Pawn __0, ref MechWorkModeDef __result)
        {
            if (!OwnRobot(__0)) return true;
            __result = DefDatabase<MechWorkModeDef>.GetNamed("Work");
            return false;
        }

        private static bool RequiresMechanitor(Pawn __0, ref bool __result)
        { if (!OwnRobot(__0)) return true; __result = false; return false; }

        private static void ShowDraft(Pawn ___pawn, ref bool __result)
        { if (OwnRobot(___pawn)) __result = true; }

        private static void ComponentsReady(Pawn __0) { EnsureControls(__0); }

        private static void MechTablePawns(ref IEnumerable<Pawn> __result)
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            List<Pawn> robots = map.mapPawns.PawnsInFaction(Faction.OfPlayer).Where(OwnRobot).ToList();
            if (robots.Count != 0) __result = __result.Concat(robots).Distinct();
        }

        private static bool AreaCell(Rect __0, Pawn __1)
        {
            if (!OwnRobot(__1)) return true;
            EnsureControls(__1);
            AreaAllowedGUI.DoAllowedAreaSelectors(__0, __1);
            return false;
        }

        private static bool OverseerCell(Rect __0, Pawn __1)
        {
            if (!OwnRobot(__1)) return true;
            Widgets.Label(__0, "목격자");
            TooltipHandler.TipRegion(__0, __1.TryGetComp<CrimsonShipRobot>().CompInspectStringExtra());
            return false;
        }

        private static bool GroupCell(Rect __0, Pawn __1)
        {
            if (!OwnRobot(__1)) return true;
            CrimsonRobotRole role = __1.TryGetComp<CrimsonShipRobot>().Role;
            Widgets.Label(__0, role == CrimsonRobotRole.Omni ? "노동" : role == CrimsonRobotRole.Rescue ? "구조" : "전투");
            return false;
        }

        private static bool ModeCell(Rect __0, Pawn __1)
        {
            if (!OwnRobot(__1)) return true;
            Widgets.Label(__0, __1.Drafted ? "징집" : __1.TryGetComp<CrimsonShipRobot>().Connected ? "자율 작업" : "연결 대기");
            return false;
        }

        private static bool EnergyCell(Rect __0, Pawn __1)
        {
            if (!OwnRobot(__1)) return true;
            Widgets.Label(__0, __1.TryGetComp<CrimsonShipRobot>().Connected ? "무선 전력" : "연결 대기");
            return false;
        }

        private static void AreaChanged(Pawn ___pawn)
        {
            if (!OwnRobot(___pawn) || !___pawn.Spawned || ___pawn.Drafted || ___pawn.CurJob == null
                || ___pawn.CurJob.playerForced) return;
            ___pawn.mindState.enemyTarget = null;
            ___pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
        }
    }

    public sealed class ThinkNode_ConditionalCrimsonRobotDrafted : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        { return CrimsonShipRobotControl.OwnRobot(pawn) && pawn.Drafted && pawn.TryGetComp<CrimsonShipRobot>().Connected; }
    }

    public sealed class JobGiver_CrimsonRobotFight : JobGiver_AIFightEnemies
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Job job = base.TryGiveJob(pawn);
            if (job != null && (job.def == JobDefOf.AttackMelee || job.def == JobDefOf.Goto)
                && !CrimsonShipRobotControl.Allowed(pawn, job.targetA.Cell)) return null;
            return job;
        }

        protected override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verb)
        {
            return base.TryFindShootingPosition(pawn, out dest, verb)
                && CrimsonShipRobotControl.Allowed(pawn, dest);
        }
    }
}
