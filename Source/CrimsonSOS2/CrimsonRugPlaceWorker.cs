using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonRugPlaceWorker : PlaceWorker
    {
        private static bool IsRug(ThingDef def)
        {
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
            if (def == null || IsRug(def) || def.IsDoor) return false;
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
                    if (IsRug(def) || def.IsDoor || (def.category == ThingCategory.Building
                        && def.passability == Traversability.Impassable))
                        return "SpaceAlreadyOccupied".Translate();
                }
            }
            return true;
        }
    }
}
