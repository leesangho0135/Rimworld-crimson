using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    // Uses the native linked-wall grid without wrapping the W04 frame in an atlas.
    public sealed class CrimsonHullWindow : Building
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            RefreshLinks(map);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map map = Map;
            base.DeSpawn(mode);
            if (map != null) RefreshLinks(map);
        }

        private void RefreshLinks(Map map)
        {
            map.linkGrid.Notify_LinkerCreatedOrDestroyed(this);
            foreach (IntVec3 cell in this.OccupiedRect().ExpandedBy(1))
                if (cell.InBounds(map))
                    map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Buildings, true, false);
        }
    }
}
