using System.Collections.Generic;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonLargeShelfGraphic : Graphic_SingleOnOff
    {
        internal const float FrontCenterZ = 1f;
        private Graphic sides;
        public override bool ShouldDrawRotated { get { return false; } }
        public override Material MatNorth { get { return mat; } }
        public override Material MatSouth { get { return mat; } }
        public override Material MatEast { get { return sides.MatEast; } }
        public override Material MatWest { get { return sides.MatWest; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            // Both native file names contain the unchanged two-state atlas.
            // Reuse SOS2's power selection, sampling one square state at a time.
            mat = FrontFrame(mat, 0f);
            matOff = FrontFrame(matOff, .5f);
            sides = GraphicDatabase.Get<CrimsonStorageHygieneGraphic>(
                "Crimson/Decor/Storage/LargeShelfStates", request.shader,
                new Vector2(3f, 1f), request.color, request.colorTwo, request.graphicData);
        }

        private static Material FrontFrame(Material original, float offset)
        {
            Material frame = new Material(original);
            frame.mainTextureScale = new Vector2(.5f, 1f);
            frame.mainTextureOffset = new Vector2(offset, 0f);
            return frame;
        }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        { return rot.IsHorizontal ? sides.MatAt(rot, thing) : base.MatAt(rot, thing); }
        public override Material MatSingleFor(Thing thing)
        { return thing == null ? MatSingle : MatAt(thing.Rotation, thing); }
        public override Mesh MeshAt(Rot4 rot)
        { return MeshPool.GridPlane(rot.IsHorizontal ? new Vector2(1f, 3f) : drawSize); }
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        {
            if (!rot.IsHorizontal) loc.z += FrontCenterZ;
            CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation);
        }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation)),
                thing.Rotation.IsHorizontal ? 0f : FrontCenterZ);
        }
        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        { return GraphicDatabase.Get<CrimsonLargeShelfGraphic>(path, shader, drawSize, color, colorTwo, data); }
    }

    public sealed class CompProperties_CrimsonLargeShelfDisplay : CompProperties
    {
        public CompProperties_CrimsonLargeShelfDisplay() { compClass = typeof(CompCrimsonLargeShelfDisplay); }
    }

    // Each of the three native storage cells holds three real stacks. Display
    // them in three levels without changing hauling or owning those items.
    public sealed class CompCrimsonLargeShelfDisplay : ThingComp
    {
        internal const int Columns = 3, Rows = 3, SlotCount = Columns * Rows;
        private List<Thing> displaySlots = new List<Thing>();

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
                && item.Position == parent.Position + new IntVec3(column - 1, 0, 0);
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
            if (parent.Rotation.IsHorizontal) return;
            for (int column = 0; column < Columns; column++)
            {
                IntVec3 cell = parent.Position + new IntVec3(column - 1, 0, 0);
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
            Vector3 location = parent.DrawPos + new Vector3(0f, 0f, CrimsonLargeShelfGraphic.FrontCenterZ);
            Matrix4x4 body = CrimsonFurnitureDepth.Transform(parent.Graphic, location, parent.Rotation, parent, 0f);
            int column = slot % Columns, row = slot / Columns;
            return body * Matrix4x4.TRS(new Vector3((column - 1) * .9f, .015f, .025f + (row - 1) * .84f),
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
