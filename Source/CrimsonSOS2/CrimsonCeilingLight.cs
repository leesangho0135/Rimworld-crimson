using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonCeilingLight : Building
    {
        internal const string DefName = "Crimson_CeilingLight";

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // The fixture lives above the camera-facing room. Keep selection and
            // native power/status overlays, but never draw a physical floor object.
            Comps_PostDraw();
        }
    }

    public sealed class CompProperties_CrimsonCeilingAccent : CompProperties_Glower
    {
        public CompProperties_CrimsonCeilingAccent()
        {
            compClass = typeof(CrimsonCeilingAccentGlower);
        }
    }

    public class CrimsonCeilingGlower : CompGlower
    {
        protected override bool ShouldBeLitNow
        {
            get { return parent.Spawned && parent.Position.Roofed(parent.Map) && base.ShouldBeLitNow; }
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            // Only roof state needs polling. Native power/flick signals remain
            // immediate, and UpdateLit does no grid rebuild if the state is unchanged.
            if (parent.Spawned) UpdateLit(parent.Map);
        }
    }

    public sealed class CrimsonCeilingAccentGlower : CrimsonCeilingGlower
    {
        // The fixed accent is recomputed on spawn. Only the main glower writes
        // native glowOn/glowColorOverride keys into the parent save node.
        public override void PostExposeData() { }
    }

    public sealed class CrimsonCeilingLightPlaceWorker : PlaceWorker
    {
        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
            ThingDef def = other as ThingDef;
            if (def != null) def = def.entityDefToBuild as ThingDef ?? def;
            return def != null && def.category == ThingCategory.Building
                && def.defName != CrimsonCeilingLight.DefName;
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (!loc.InBounds(map) || !loc.Roofed(map)) return "천장이 있는 칸에 설치해야 합니다.";
            foreach (Thing existing in map.thingGrid.ThingsListAtFast(loc))
            {
                if (existing == thingToIgnore || existing == thing) continue;
                ThingDef def = existing.def.entityDefToBuild as ThingDef ?? existing.def;
                if (def.defName == CrimsonCeilingLight.DefName) return "이 칸에는 이미 천장등이 있습니다.";
            }
            return true;
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonCeilingLightPlacement
    {
        static CrimsonCeilingLightPlacement()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.ceiling-light-placement");
            harmony.Patch(AccessTools.Method(typeof(GenConstruct), "CanPlaceBlueprintOver"),
                prefix: new HarmonyMethod(typeof(CrimsonCeilingLightPlacement), "AllowCeiling"));
            harmony.Patch(AccessTools.Method(typeof(GenSpawn), "SpawningWipes"),
                prefix: new HarmonyMethod(typeof(CrimsonCeilingLightPlacement), "PreserveCeiling"));
        }

        private static ThingDef Built(BuildableDef def)
        {
            ThingDef thing = def as ThingDef;
            return thing == null ? null : thing.entityDefToBuild as ThingDef ?? thing;
        }

        private static bool Shares(BuildableDef first, BuildableDef second)
        {
            ThingDef a = Built(first), b = Built(second);
            if (a == null || b == null || a.category != ThingCategory.Building || b.category != ThingCategory.Building) return false;
            bool ceilingA = a.defName == CrimsonCeilingLight.DefName;
            bool ceilingB = b.defName == CrimsonCeilingLight.DefName;
            return ceilingA != ceilingB;
        }

        private static bool AllowCeiling(BuildableDef __0, ThingDef __1, ref bool __result)
        {
            if (!Shares(__0, __1)) return true;
            __result = true; return false;
        }

        private static bool PreserveCeiling(BuildableDef __0, BuildableDef __1, ref bool __result)
        {
            if (!Shares(__0, __1)) return true;
            __result = false; return false;
        }
    }
}
