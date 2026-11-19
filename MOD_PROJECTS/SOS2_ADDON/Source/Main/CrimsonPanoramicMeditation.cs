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
    public sealed class Building_CrimsonPanoramicMeditationDais : Building
    {
        public const string DefName = "Crimson_PanoramicMeditationDaisWide";
        public const string JobDefName = "Crimson_PanoramicMeditation";

        public Pawn AssignedPawn
        {
            get
            {
                CompAssignableToPawn owners = this.TryGetComp<CompAssignableToPawn>();
                return owners == null ? null : owners.AssignedPawnsForReading.FirstOrDefault();
            }
        }

        public IntVec3 WindowCell
        {
            get
            {
                CellRect rect = this.OccupiedRect();
                switch (Rotation.AsInt)
                {
                    case 0: return new IntVec3(rect.minX + rect.Width / 2, 0, rect.maxZ + 1);
                    case 1: return new IntVec3(rect.maxX + 1, 0, rect.minZ + rect.Height / 2);
                    case 2: return new IntVec3(rect.minX + rect.Width / 2, 0, rect.minZ - 1);
                    default: return new IntVec3(rect.minX - 1, 0, rect.minZ + rect.Height / 2);
                }
            }
        }

        public IntVec3 MeditationCell { get { return WindowCell - Rotation.FacingCell; } }

        public Building_CrimsonPanoramicWindow Window
        {
            get
            {
                if (!Spawned || !WindowCell.InBounds(Map)) return null;
                return WindowCell.GetThingList(Map).OfType<Building_CrimsonPanoramicWindow>()
                    .FirstOrDefault(window => window.Position == WindowCell
                        && window.Rotation == Rotation && window.def.size.x == def.size.x
                        && window.HasBackingWall);
            }
        }

        public bool Allows(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && pawn.Faction == Faction.OfPlayer
                && AssignedPawn == pawn && Spawned && !Destroyed && Window != null
                && MeditationCell.Standable(Map);
        }

        public Job MakeMeditationJob(Pawn pawn, bool forJoy)
        {
            if (!Allows(pawn) || !MeditationUtility.CanMeditateNow(pawn)
                || !pawn.CanReserveAndReach(this, PathEndMode.OnCell, pawn.NormalMaxDanger())
                || !pawn.CanReach(MeditationCell, PathEndMode.OnCell, pawn.NormalMaxDanger())) return null;
            // Native meditation reserves A for the standing spot, B exclusively
            // for a bed, and C for the focus. Keep B invalid for standing jobs.
            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed(JobDefName), MeditationCell,
                LocalTargetInfo.Invalid, this);
            job.ignoreJoyTimeAssignment = !forJoy;
            return job;
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(pawn)) yield return option;
            if (AssignedPawn != pawn)
            {
                yield return new FloatMenuOption("전망 명상: 지정된 소유자만 사용할 수 있습니다.", null);
                yield break;
            }
            if (Window == null)
            {
                yield return new FloatMenuOption("전망 명상: 같은 폭·방향의 전망창과 기존 벽 " + def.size.x + "칸이 필요합니다.", null);
                yield break;
            }
            Job ordered = MakeMeditationJob(pawn, true);
            yield return new FloatMenuOption(ordered == null ? "전망 명상: 지금은 사용할 수 없습니다." : "전망 명상",
                ordered == null ? (Action)null : delegate { pawn.jobs.TryTakeOrderedJob(ordered, JobTag.Misc); });
        }

        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n명상 위치: 연단 중앙 · 시선: 전망창 방향\n"
                + (Window == null ? "전망창 연결 안 됨: 바라보는 쪽 벽에 같은 방향으로 설치하세요." : "전망창 연결됨 · 지정 소유자 전용")
                + "\n기본 명상 집중 회복 보너스: +50%/일";
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            if (!Spawned) return;
            GenDraw.DrawFieldEdges(new List<IntVec3> { MeditationCell }, new Color(.8f, .12f, .12f));
            if (Window != null) GenDraw.DrawLineBetween(MeditationCell.ToVector3Shifted(), Window.TrueCenter());
        }
    }

    public sealed class CrimsonPanoramicDaisPlaceWorker : PlaceWorker
    {
        public override bool ForceAllowPlaceOver(BuildableDef other)
        { return other != null && other.defName == "Crimson_Deck"; }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            ThingDef definition = checkingDef as ThingDef;
            if (definition == null) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(loc, rot, definition.size))
            {
                if (!cell.InBounds(map) || !cell.Standable(map)) return "연단은 실내의 걸을 수 있는 바닥 두 줄에 설치해야 합니다.";
                foreach (Thing existing in cell.GetThingList(map))
                {
                    if (existing == thing || existing == thingToIgnore) continue;
                    ThingDef built = existing.def.entityDefToBuild as ThingDef ?? existing.def;
                    if (built.defName == "Crimson_Deck" || built.building != null && built.building.isPowerConduit) continue;
                    if (built.category == ThingCategory.Building) return "다른 가구나 벽과 연단을 겹쳐 설치할 수 없습니다.";
                }
            }
            return true;
        }
    }

    public sealed class JobDriver_CrimsonPanoramicMeditation : JobDriver_Meditate
    {
        private Building_CrimsonPanoramicMeditationDais Dais
        { get { return job.GetTarget(TargetIndex.C).Thing as Building_CrimsonPanoramicMeditationDais; } }

        private void StandFacingWindow()
        {
            Building_CrimsonPanoramicMeditationDais dais = Dais;
            if (dais == null || !dais.Allows(pawn))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            pawn.jobs.posture = PawnPosture.Standing;
            pawn.rotationTracker.FaceCell(dais.WindowCell);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.C);
            this.FailOn(delegate { return Dais == null || !Dais.Allows(pawn); });
            // Retain the complete native movement and meditation sequence,
            // including joy, psyfocus and interruption rules. Only constrain
            // this furniture's owner, standing posture and viewing direction.
            foreach (Toil toil in base.MakeNewToils())
            {
                toil.AddPreInitAction(StandFacingWindow);
                toil.AddPreTickAction(StandFacingWindow);
                yield return toil;
            }
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonPanoramicMeditationIntegration
    {
        static CrimsonPanoramicMeditationIntegration()
        {
            Harmony harmony = new Harmony("rimod.crimson.panorama-meditation");
            harmony.Patch(AccessTools.Method(typeof(MeditationUtility), "GetMeditationJob"),
                prefix: new HarmonyMethod(typeof(CrimsonPanoramicMeditationIntegration), "OwnedJob"));
            harmony.Patch(AccessTools.Method(typeof(MeditationUtility), "IsValidMeditationBuildingForPawn"),
                postfix: new HarmonyMethod(typeof(CrimsonPanoramicMeditationIntegration), "ValidSpot"));
            harmony.Patch(AccessTools.Method(typeof(CompMeditationFocus), "CanPawnUse"),
                postfix: new HarmonyMethod(typeof(CrimsonPanoramicMeditationIntegration), "OwnedFocus"));
        }

        private static bool OwnedJob(Pawn __0, bool __1, ref Job __result)
        {
            Building_CrimsonPanoramicMeditationDais dais = __0.ownership == null ? null
                : __0.ownership.AssignedMeditationSpot as Building_CrimsonPanoramicMeditationDais;
            if (dais == null) return true;
            Job owned = dais.MakeMeditationJob(__0, __1);
            if (owned == null) return true;
            __result = owned;
            return false;
        }

        private static void ValidSpot(Building __0, Pawn __1, ref bool __result)
        {
            Building_CrimsonPanoramicMeditationDais dais = __0 as Building_CrimsonPanoramicMeditationDais;
            if (dais != null) __result = __result && dais.Allows(__1);
        }

        private static void OwnedFocus(CompMeditationFocus __instance, Pawn __0, ref bool __result)
        {
            Building_CrimsonPanoramicMeditationDais dais = __instance.parent as Building_CrimsonPanoramicMeditationDais;
            if (dais != null) __result = __result && dais.Allows(__0);
        }
    }
}
