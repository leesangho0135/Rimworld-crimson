using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonRugPlaceWorker : PlaceWorker
    {
        internal static bool IsRug(BuildableDef definition)
        {
            ThingDef def = definition as ThingDef;
            if (def != null) def = def.entityDefToBuild as ThingDef ?? def;
            return def != null && (def.defName == "Crimson_RugA1Small"
                || def.defName == "Crimson_RugA1Medium" || def.defName == "Crimson_RugA1Large"
                || def.defName == "Crimson_RugA1ExtraLarge"
                || def.defName == "Crimson_RugA1SemiLarge"
                || def.defName == "Crimson_RugA1MediumWide");
        }

        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
            ThingDef def = other as ThingDef;
            if (def != null) def = def.entityDefToBuild as ThingDef ?? def;
            if (def == null || def.IsDoor) return false;
            if (IsRug(def)) return true;
            if (def.defName == "Crimson_Deck") return true;
            return def.category == ThingCategory.Building && def.building != null
                && !def.building.isNaturalRock && def.passability != Traversability.Impassable
                && def.designationCategory != null
                && (def.designationCategory.defName == "Furniture"
                    || def.designationCategory.defName == "Crimson_CS_Furniture"
                    // Menu placement must not change physical rug compatibility.
                    || (def.defName.StartsWith("Crimson_", System.StringComparison.Ordinal)
                        && def.GetCompProperties<CompProperties_CrimsonFixedFurniture>() != null));
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            foreach (IntVec3 cell in GenAdj.OccupiedRect(loc, rot, checkingDef.Size))
            {
                if (!cell.InBounds(map)) return false;
                foreach (Thing existing in map.thingGrid.ThingsListAtFast(cell))
                {
                    if (existing == thingToIgnore || existing == thing) continue;
                    ThingDef def = existing.def.entityDefToBuild as ThingDef ?? existing.def;
                    if (IsRug(def)) continue;
                    if (def.IsDoor || (def.category == ThingCategory.Building
                        && def.passability == Traversability.Impassable))
                        return "SpaceAlreadyOccupied".Translate();
                }
            }
            return true;
        }
    }

    // Only Crimson rug pairs share their occupied cells. Resolve construction
    // stages too, so placing or completing one rug never removes another.
    [StaticConstructorOnStartup]
    internal static class CrimsonRugOverlap
    {
        private static readonly Predicate<Thing> SkipRugs = delegate(Thing thing)
        { return thing != null && CrimsonRugPlaceWorker.IsRug(thing.def); };

        static CrimsonRugOverlap()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.rug-overlap");
            harmony.Patch(AccessTools.Method(typeof(GenConstruct), "CanPlaceBlueprintAt_NewTemp"),
                prefix: new HarmonyMethod(typeof(CrimsonRugOverlap), "IgnoreExistingRugs"));
            harmony.Patch(AccessTools.Method(typeof(GenConstruct), "CanPlaceBlueprintOver"),
                prefix: new HarmonyMethod(typeof(CrimsonRugOverlap), "AllowRugs"));
            harmony.Patch(AccessTools.Method(typeof(GenSpawn), "SpawningWipes"),
                prefix: new HarmonyMethod(typeof(CrimsonRugOverlap), "PreserveRugs"));
        }

        private static bool BothRugs(BuildableDef first, BuildableDef second)
        {
            return CrimsonRugPlaceWorker.IsRug(first) && CrimsonRugPlaceWorker.IsRug(second);
        }

        private static void IgnoreExistingRugs(BuildableDef __0, ref Predicate<Thing> __11)
        {
            if (!CrimsonRugPlaceWorker.IsRug(__0)) return;
            // Use the native exclusion hook instead of bypassing placement:
            // terrain, bounds, doors and all non-rug obstacles are still checked.
            Predicate<Thing> established = __11;
            __11 = established == null ? SkipRugs : delegate(Thing thing)
            { return SkipRugs(thing) || established(thing); };
        }

        private static bool AllowRugs(BuildableDef __0, ThingDef __1, ref bool __result)
        {
            if (!BothRugs(__0, __1)) return true;
            __result = true;
            return false;
        }

        private static bool PreserveRugs(BuildableDef __0, BuildableDef __1, ref bool __result)
        {
            if (!BothRugs(__0, __1)) return true;
            __result = false;
            return false;
        }
    }
}
