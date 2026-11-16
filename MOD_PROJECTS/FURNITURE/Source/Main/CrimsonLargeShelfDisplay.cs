using System.Collections.Generic;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CompProperties_CrimsonLargeShelfDisplay : CompProperties
    {
        public int columns = 3;
        public int rows = 3;
        public CompProperties_CrimsonLargeShelfDisplay() { compClass = typeof(CompCrimsonLargeShelfDisplay); }
    }

    // Parameterized native storage display: small 1x2, medium 2x2, large 3x3.
    // Every compartment tracks one real stack without replacing hauling.
    public sealed class CompCrimsonLargeShelfDisplay : ThingComp
    {
        private CompProperties_CrimsonLargeShelfDisplay Settings
        { get { return (CompProperties_CrimsonLargeShelfDisplay)props; } }
        internal int Columns { get { return Settings.columns; } }
        internal int Rows { get { return Settings.rows; } }
        internal int SlotCount { get { return Columns * Rows; } }
        private List<Thing> displaySlots = new List<Thing>();

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!respawningAfterLoad) return;
            // Old small/medium shelves stored three stacks per floor cell.
            // Move only the surplus outside the resized shelf, never delete it.
            LongEventHandler.ExecuteWhenFinished(MoveLegacyOverflow);
        }

        private void MoveLegacyOverflow()
        {
            if (!parent.Spawned || parent.Destroyed) return;
            Map map = parent.Map;
            CellRect footprint = parent.OccupiedRect();
            foreach (IntVec3 cell in footprint)
            {
                List<Thing> items = new List<Thing>();
                foreach (Thing thing in cell.GetThingList(map))
                    if (thing.def.category == ThingCategory.Item) items.Add(thing);
                items.Sort(delegate(Thing a, Thing b) { return a.thingIDNumber.CompareTo(b.thingIDNumber); });
                for (int i = Rows; i < items.Count; i++)
                {
                    Thing item = items[i];
                    item.DeSpawn(DestroyMode.Vanish);
                    if (!GenPlace.TryPlaceThing(item, cell, map, ThingPlaceMode.Near, null,
                        delegate(IntVec3 candidate) { return !footprint.Contains(candidate); }))
                        GenSpawn.Spawn(item, cell, map, WipeMode.Vanish);
                }
            }
            RefreshSlots();
        }

        private void EnsureSlots()
        {
            if (displaySlots == null) displaySlots = new List<Thing>();
            while (displaySlots.Count < SlotCount) displaySlots.Add(null);
            if (displaySlots.Count > SlotCount) displaySlots.RemoveRange(SlotCount, displaySlots.Count - SlotCount);
        }

        public override void PostExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) RefreshSlots();
            Scribe_Collections.Look(ref displaySlots, "crimsonShelfDisplaySlots", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) EnsureSlots();
        }

        private bool InColumn(Thing item, int column)
        {
            return item != null && !item.Destroyed && item.Spawned && item.Map == parent.Map
                && item.def.category == ThingCategory.Item
                && item.Position == ColumnCell(column);
        }

        private IntVec3 ColumnCell(int column)
        {
            CellRect footprint = parent.OccupiedRect();
            return parent.Rotation.IsHorizontal
                ? new IntVec3(footprint.minX, 0, footprint.minZ + column)
                : new IntVec3(footprint.minX + column, 0, footprint.minZ);
        }

        private void RefreshSlots()
        {
            EnsureSlots();
            if (!parent.Spawned || parent.Destroyed) return;
            // Retain surviving assignments, including holes left by removal.
            // New arrivals fill empty compartments; existing stacks do not jump.
            for (int slot = 0; slot < SlotCount; slot++)
            {
                Thing item = displaySlots[slot];
                if (item == null) continue;
                bool duplicate = false;
                for (int earlier = 0; earlier < slot; earlier++)
                    if (displaySlots[earlier] == item) duplicate = true;
                if (!InColumn(item, slot % Columns) || duplicate) displaySlots[slot] = null;
            }
            for (int column = 0; column < Columns; column++)
            {
                IntVec3 cell = ColumnCell(column);
                if (!cell.InBounds(parent.Map)) continue;
                List<Thing> things = parent.Map.thingGrid.ThingsListAtFast(cell);
                for (int row = 0; row < Rows; row++)
                {
                    int slot = column + row * Columns;
                    if (displaySlots[slot] != null) continue;
                    Thing next = null;
                    for (int i = 0; i < things.Count; i++)
                    {
                        Thing candidate = things[i];
                        if (InColumn(candidate, column) && !displaySlots.Contains(candidate)
                            && (next == null || candidate.thingIDNumber < next.thingIDNumber)) next = candidate;
                    }
                    if (next != null) displaySlots[slot] = next;
                }
            }
        }

        public Thing DisplayItem(int slot)
        {
            if (!parent.Spawned || parent.Destroyed || parent.Rotation.IsHorizontal
                || slot < 0 || slot >= SlotCount) return null;
            RefreshSlots();
            return displaySlots[slot];
        }

        internal static Vector2 ItemSize(Thing item)
        {
            Vector2 source = item.Graphic.drawSize;
            Vector2 fitted = CrimsonWallEquipmentStorage.FitContent(source, new Vector2(.66f, .48f), 0f);
            float scale = Mathf.Min(1f, fitted.x / Mathf.Max(.001f, source.x));
            return source * scale;
        }

        internal Matrix4x4 ItemMatrix(int slot, Thing item)
        {
            Vector2 size = ItemSize(item);
            CrimsonCardinalGraphic graphic = parent.Graphic as CrimsonCardinalGraphic;
            float offset = graphic == null ? (parent.def.graphicData.drawSize.y - 1f) * .5f : graphic.FrameOffset(parent.Rotation);
            Vector3 location = parent.DrawPos + new Vector3(0f, 0f, offset);
            Matrix4x4 body = CrimsonFurnitureDepth.Transform(parent.Graphic, location, parent.Rotation, parent, 0f);
            int column = slot % Columns, row = slot / Columns;
            float centerColumn = (Columns - 1) * .5f, centerRow = (Rows - 1) * .5f;
            float rowSpacing = Rows == 3 ? .84f : .78f;
            return body * Matrix4x4.TRS(new Vector3((column - centerColumn) * .9f, .015f, .025f + (row - centerRow) * rowSpacing),
                Quaternion.identity, new Vector3(size.x, 1f, size.y));
        }

        public override void PostDraw()
        {
            if (!parent.Spawned || parent.Destroyed || parent.Rotation.IsHorizontal) return;
            RefreshSlots();
            for (int slot = 0; slot < SlotCount; slot++)
            {
                Thing item = displaySlots[slot];
                if (item != null) Graphics.DrawMesh(MeshPool.plane10, ItemMatrix(slot, item), item.Graphic.MatSingleFor(item), 0);
            }
        }
    }
}
