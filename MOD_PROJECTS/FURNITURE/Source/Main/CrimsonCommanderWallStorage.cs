using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Keep native slot groups, filters, hauling and save data. Only the two
    // closed wall cabinets opt into this building type and its wall refresh.
    public sealed class CrimsonCommanderWallStorage : Building_Storage
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            RefreshWallLinks(map);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map map = Map;
            base.DeSpawn(mode);
            RefreshWallLinks(map);
        }

        private void RefreshWallLinks(Map map)
        {
            if (map == null) return;
            map.linkGrid.Notify_LinkerCreatedOrDestroyed(this);
            foreach (IntVec3 cell in this.OccupiedRect().ExpandedBy(1))
                if (cell.InBounds(map))
                    map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Buildings, true, false);
        }
    }

    // Print a native linked hull tile for EACH cell, rather than stretching a
    // single tile over the cabinet. A slightly wider shell makes a shallow
    // step on each side without changing the native storage footprint.
    public sealed class CrimsonCommanderWallStorageGraphic : Graphic_Single
    {
        private WallCellGraphic cells;
        private static Material floor;
        public override bool ShouldDrawRotated { get { return false; } }

        private WallCellGraphic Cells
        {
            get { return cells ?? (cells = new WallCellGraphic(this)); }
        }

        public override Graphic GetColoredVersion(Shader shader, Color first, Color second)
        {
            return GraphicDatabase.Get<CrimsonCommanderWallStorageGraphic>(path, shader,
                drawSize, first, second, data);
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            if (floor == null)
                floor = GraphicDatabase.Get<Graphic_Single>("Crimson/Hull/DeckSeamless",
                    ShaderDatabase.Cutout, Vector2.one, Color.white).MatSingle;
            bool built = thing.def.entityDefToBuild == null;
            foreach (IntVec3 cell in thing.OccupiedRect())
            {
                Vector3 location = cell.ToVector3Shifted();
                if (built)
                {
                    location.y = AltitudeLayer.FloorEmplacement.AltitudeFor();
                    Printer_Plane.PrintPlane(layer, location, Vector2.one, floor);
                }
                location.y = thing.def.altitudeLayer.AltitudeFor();
                Printer_Plane.PrintPlane(layer, location, new Vector2(drawSize.x, 1f), Cells.At(thing, cell));
            }
            if (!built) return;
            foreach (PanelLine line in PanelLines(IsMedicine(thing.def)))
            {
                Vector3 location = thing.TrueCenter() + new Vector3(line.x * drawSize.x, .006f, line.z);
                Printer_Plane.PrintPlane(layer, location, new Vector2(line.width * drawSize.x, line.height),
                    SolidColorMaterials.SimpleSolidColorMaterial(line.color));
            }
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        {
            // Placement previews share the shell width; installation stays 1x2.
            for (int index = 0; index < 2; index++)
            {
                LinkDirections links = index == 0 ? LinkDirections.Up : LinkDirections.Down;
                Material material = MaterialAtlasPool.SubMaterialFromAtlas(mat, links);
                Vector3 location = loc + new Vector3(0f, 0f, index - .5f);
                Matrix4x4 transform = Matrix4x4.TRS(location, Quaternion.identity,
                    new Vector3(drawSize.x, 1f, 1f));
                Graphics.DrawMesh(MeshPool.plane10, transform, material, 0);
            }
        }

        private static bool IsMedicine(ThingDef def)
        {
            return def != null && def.defName == "Crimson_CommanderWallMedicineCabinet";
        }

        internal struct PanelLine
        {
            internal float x, z, width, height;
            internal Color color;
            internal PanelLine(float x, float z, float width, float height, Color color)
            { this.x = x; this.z = z; this.width = width; this.height = height; this.color = color; }
        }

        internal static IEnumerable<PanelLine> PanelLines(bool medicine)
        {
            Color shadow = new Color(.045f, .045f, .047f);
            Color lip = new Color(.16f, .16f, .17f);
            Color recess = new Color(.065f, .065f, .07f);
            Color marker = new Color(.25f, .055f, .045f);
            yield return new PanelLine(-.288f, 0f, .012f, 1.53f, shadow);
            yield return new PanelLine(.288f, 0f, .010f, 1.53f, lip);
            yield return new PanelLine(0f, .765f, .576f, .010f, lip);
            yield return new PanelLine(0f, -.765f, .576f, .016f, shadow);
            yield return new PanelLine(0f, 0f, .565f, .012f, shadow);
            yield return new PanelLine(0f, -.016f, .565f, .006f, lip);
            yield return new PanelLine(.175f, .105f, .095f, .022f, recess);
            yield return new PanelLine(.175f, .091f, .095f, .006f, lip);
            yield return new PanelLine(.175f, -.655f, .040f, .014f, marker);
            if (medicine)
            {
                yield return new PanelLine(0f, .385f, .565f, .010f, shadow);
                yield return new PanelLine(0f, -.385f, .565f, .010f, shadow);
                yield return new PanelLine(.105f, -.655f, .040f, .014f, marker);
            }
        }

        private sealed class WallCellGraphic : Graphic_Linked
        {
            internal WallCellGraphic(Graphic subGraphic) : base(subGraphic) { }
            internal Material At(Thing thing, IntVec3 cell) { return LinkedDrawMatFrom(thing, cell); }
            public override bool ShouldLinkWith(IntVec3 cell, Thing parent)
            {
                // Internal cells must join even for an unspawned blueprint.
                return parent.OccupiedRect().Contains(cell) || base.ShouldLinkWith(cell, parent);
            }
        }
    }
}
