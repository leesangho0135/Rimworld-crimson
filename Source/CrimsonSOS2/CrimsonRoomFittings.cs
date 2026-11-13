using HarmonyLib;
using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    // Keep placement symmetric without treating every effect drawn on the
    // MoteLow altitude as a conflicting physical floor light.
    [StaticConstructorOnStartup]
    internal static class CrimsonPathGuidePlacement
    {
        static CrimsonPathGuidePlacement()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.path-guide-placement");
            harmony.Patch(AccessTools.Method(typeof(GenConstruct), "CanPlaceBlueprintOver"),
                prefix: new HarmonyMethod(typeof(CrimsonPathGuidePlacement), "PreventGuideOverlap"));
        }

        private static bool PreventGuideOverlap(BuildableDef __0, ThingDef __1,
            ref bool __result)
        {
            ThingDef placing = BuiltDef(__0);
            ThingDef existing = BuiltDef(__1);
            if ((IsPathGuide(placing) && IsEstablishedLight(existing))
                || (IsPathGuide(existing) && IsEstablishedLight(placing)))
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static ThingDef BuiltDef(BuildableDef def)
        {
            ThingDef thingDef = def as ThingDef;
            return thingDef == null ? null : thingDef.entityDefToBuild as ThingDef ?? thingDef;
        }

        private static bool IsPathGuide(ThingDef def)
        {
            return def != null && def.defName == "Crimson_PathGuideLight";
        }

        private static bool IsEstablishedLight(ThingDef def)
        {
            return def != null && (def.defName == "Crimson_FloorBeacon"
                || def.defName == "Crimson_ThresholdGuide"
                || def.defName == "Crimson_DeckLight"
                || def.defName == "Crimson_DeckLightParallel");
        }
    }

    // Keep the established deck and obstruction rules of the public guide
    // place worker; this additional check belongs only to the new guide.
    public sealed class CrimsonPathGuideLightPlaceWorker : CrimsonFloorGuidePlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            AcceptanceReport established = base.AllowsPlacing(checkingDef, loc, rot,
                map, thingToIgnore, thing);
            if (!established.Accepted) return established;
            foreach (Thing existing in map.thingGrid.ThingsListAtFast(loc))
            {
                if (existing == thingToIgnore || existing == thing) continue;
                ThingDef built = existing.def.entityDefToBuild as ThingDef ?? existing.def;
                if (built.defName == "Crimson_PathGuideLight")
                    return "SpaceAlreadyOccupied".Translate();
            }
            return true;
        }
    }

}
