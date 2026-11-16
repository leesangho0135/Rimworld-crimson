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

    // The hull tiles stay exactly 1x1. Separate narrow jambs extend beyond
    // their native silhouettes; the wall artwork itself is never stretched.
    public sealed class CrimsonCommanderWallStorageGraphic : Graphic_Single
    {
        private WallCellGraphic cells;
        private static Material floor;
        public override bool ShouldDrawRotated { get { return false; } }
        internal static Vector2 BodyCellSize { get { return Vector2.one; } }

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
                Printer_Plane.PrintPlane(layer, location, BodyCellSize, Cells.At(thing, cell));
                if (!built) continue;
                float cellZ = cell.z + .5f - thing.TrueCenter().z;
                foreach (PanelLine line in SideLines(Cells.Mask(thing, cell), cellZ, IsMedicine(thing.def)))
                {
                    Vector3 side = thing.TrueCenter() + new Vector3(line.x, .006f, line.z);
                    Printer_Plane.PrintPlane(layer, side, new Vector2(line.width, line.height),
                        SolidColorMaterials.SimpleSolidColorMaterial(line.color));
                }
            }
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        {
            // A fixed 1x2 preview uses the original tile sizes too.
            for (int index = 0; index < 2; index++)
            {
                LinkDirections links = index == 0 ? LinkDirections.Up : LinkDirections.Down;
                Material material = MaterialAtlasPool.SubMaterialFromAtlas(mat, links);
                Vector3 location = loc + new Vector3(0f, 0f, index - .5f);
                Graphics.DrawMesh(MeshPool.plane10, location, Quaternion.identity, material, 0);
                foreach (PanelLine line in SideLines((int)links, index - .5f, IsMedicine(def)))
                {
                    Vector3 side = loc + new Vector3(line.x, .006f, line.z);
                    Matrix4x4 matrix = Matrix4x4.TRS(side, Quaternion.identity,
                        new Vector3(line.width, 1f, line.height));
                    Graphics.DrawMesh(MeshPool.plane10, matrix,
                        SolidColorMaterials.SimpleSolidColorMaterial(line.color), 0);
                }
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

        // Native atlas face bounds in pixels for each N/E/S/W link mask.
        // Reading these constants does not allocate cropped wall textures.
        private static readonly int[] LeftPixels = {33,21,32,24,32,32,31,32,0,0,0,0,0,0,0,0};
        private static readonly int[] RightPixels = {224,235,256,256,224,224,256,256,224,224,256,256,224,224,256,256};
        internal static float SideEdge(int mask, int sign)
        {
            return (sign < 0 ? LeftPixels[mask & 15] : RightPixels[mask & 15]) / 256f - .5f;
        }

        internal static IEnumerable<PanelLine> SideLines(int mask, float cellZ, bool medicine)
        {
            float bottom = System.Math.Max(-.78f, cellZ - .5f);
            float top = System.Math.Min(.78f, cellZ + .5f);
            float center = (bottom + top) * .5f, height = top - bottom;
            Color shadow = new Color(.035f,.035f,.038f);
            Color face = new Color(.115f,.115f,.12f);
            Color bevel = new Color(.19f,.19f,.20f);
            Color marker = new Color(.23f,.04f,.035f);
            foreach (int sign in new[] {-1, 1})
            {
                float edge = SideEdge(mask, sign);
                yield return new PanelLine(edge + sign*.012f, center, .024f, height, shadow);
                yield return new PanelLine(edge + sign*.009f, center, .016f, height-.012f, face);
                yield return new PanelLine(edge + sign*.019f, center, .004f, height-.02f, bevel);
                if (bottom <= 0f && top >= 0f)
                    yield return new PanelLine(edge + sign*.012f, 0f, .024f, .008f, shadow);
                if (medicine)
                    foreach (float seam in new[] {-.37f,.37f})
                        if (seam > bottom && seam < top)
                            yield return new PanelLine(edge + sign*.009f, seam, .016f, .009f, shadow);
                if (sign > 0 && bottom < -.62f && top > -.62f)
                {
                    yield return new PanelLine(edge+.009f,-.62f,.009f,.014f,marker);
                    if (medicine) yield return new PanelLine(edge+.009f,-.66f,.009f,.014f,marker);
                }
            }
        }

        private sealed class WallCellGraphic : Graphic_Linked
        {
            internal WallCellGraphic(Graphic subGraphic) : base(subGraphic) { }
            internal Material At(Thing thing, IntVec3 cell) { return LinkedDrawMatFrom(thing, cell); }
            internal int Mask(Thing thing, IntVec3 cell)
            {
                int result = 0;
                for (int direction = 0; direction < 4; direction++)
                    if (ShouldLinkWith(cell + GenAdj.CardinalDirections[direction], thing)) result |= 1 << direction;
                return result;
            }
            public override bool ShouldLinkWith(IntVec3 cell, Thing parent)
            {
                // Internal cells must join even for an unspawned blueprint.
                return parent.OccupiedRect().Contains(cell) || base.ShouldLinkWith(cell, parent);
            }
        }
    }
}
