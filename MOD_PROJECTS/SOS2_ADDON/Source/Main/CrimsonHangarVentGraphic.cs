using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    internal static class CrimsonHangarVentArt
    {
        internal static Rect[] TextureWindows()
        {
            return new[] { new Rect(0.027910686f,0.649920255f,0.692982456f,0.142743222f), new Rect(0.837320574f,0.513556619f,0.109250399f,0.472886762f), new Rect(0.027910686f,0.150717703f,0.692982456f,0.141945774f), new Rect(0.836523126f,0.014354067f,0.110047847f,0.473684211f) };
        }

        internal static Vector2 SizeFor(int direction)
        { return direction == 1 || direction == 3 ? new Vector2(1f,5f) : new Vector2(5f,1f); }

    }

    // Distinct interior, exterior and side views share one full hull-wall footprint.
    // Cropped mesh UVs keep the raw PNG and native construction tints intact.
    public sealed class CrimsonHangarVentGraphic : Graphic_Single
    {
        private readonly Mesh[] panels = new Mesh[4];
        private Material backingFloor;
        public override bool ShouldDrawRotated { get { return false; } }

        public override Mesh MeshAt(Rot4 rot)
        {
            int direction = rot.AsInt;
            if (direction < 0 || direction > 3) direction = 0;
            if (panels[direction] != null) return panels[direction];
            Rect uv = CrimsonHangarVentArt.TextureWindows()[direction];
            Vector2 size = CrimsonHangarVentArt.SizeFor(direction);
            float length = Mathf.Max(drawSize.x, drawSize.y);
            if (length > 5f)
                size = direction == 1 || direction == 3 ? new Vector2(1f, length) : new Vector2(length, 1f);
            float halfWidth = size.x * 0.5f;
            float halfDepth = size.y * 0.5f;
            Mesh panel = new Mesh();
            panel.name = length > 5f ? "CrimsonHangarVent" + length + "x1" : "CrimsonHangarVent5x1";
            panel.vertices = new[] { new Vector3(-halfWidth,0f,-halfDepth),
                new Vector3(-halfWidth,0f,halfDepth), new Vector3(halfWidth,0f,halfDepth),
                new Vector3(halfWidth,0f,-halfDepth) };
            panel.uv = new[] { new Vector2(uv.xMin,uv.yMin), new Vector2(uv.xMin,uv.yMax),
                new Vector2(uv.xMax,uv.yMax), new Vector2(uv.xMax,uv.yMin) };
            panel.triangles = new[] { 0,1,2,0,2,3 };
            if (length > 5f)
            {
                // The original half-cell end frames keep their width. Only
                // the central four-cell span grows for the longer variant.
                bool vertical = direction == 1 || direction == 3;
                float[] positions = { -length * .5f, -length * .5f + .5f, length * .5f - .5f, length * .5f };
                float[] fractions = { 0f, .1f, .9f, 1f };
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> texCoords = new List<Vector2>();
                List<int> triangles = new List<int>();
                for (int i = 0; i < 3; i++)
                {
                    int start = vertices.Count;
                    float left = vertical ? -.5f : positions[i], right = vertical ? .5f : positions[i + 1];
                    float bottom = vertical ? positions[i] : -.5f, top = vertical ? positions[i + 1] : .5f;
                    float u0 = vertical ? uv.xMin : Mathf.Lerp(uv.xMin, uv.xMax, fractions[i]);
                    float u1 = vertical ? uv.xMax : Mathf.Lerp(uv.xMin, uv.xMax, fractions[i + 1]);
                    float v0 = vertical ? Mathf.Lerp(uv.yMin, uv.yMax, fractions[i]) : uv.yMin;
                    float v1 = vertical ? Mathf.Lerp(uv.yMin, uv.yMax, fractions[i + 1]) : uv.yMax;
                    vertices.Add(new Vector3(left, 0f, bottom)); vertices.Add(new Vector3(left, 0f, top));
                    vertices.Add(new Vector3(right, 0f, top)); vertices.Add(new Vector3(right, 0f, bottom));
                    texCoords.Add(new Vector2(u0, v0)); texCoords.Add(new Vector2(u0, v1));
                    texCoords.Add(new Vector2(u1, v1)); texCoords.Add(new Vector2(u1, v0));
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
                panel.vertices = vertices.ToArray(); panel.uv = texCoords.ToArray(); panel.triangles = triangles.ToArray();
            }
            panel.RecalculateNormals(); panel.RecalculateBounds();
            panels[direction] = panel;
            return panel;
        }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonHangarVentGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        {
            Quaternion facing = Quaternion.AngleAxis(extraRotation, Vector3.up);
            Matrix4x4 matrix = Matrix4x4.TRS(loc + DrawOffset(rot), facing, Vector3.one);
            Graphics.DrawMesh(MeshAt(rot), matrix, MatAt(rot, thing), 0);
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            // Match the existing hull windows at clipped frame corners, without
            // replacing terrain or exposing the native support-tile artwork.
            if (backingFloor == null)
                backingFloor = GraphicDatabase.Get<Graphic_Single>("Crimson/Hull/DeckSeamless",
                    ShaderDatabase.Cutout, Vector2.one, Color.white).MatSingle;
            if (thing.def.entityDefToBuild == null)
                foreach (IntVec3 cell in thing.OccupiedRect())
                {
                    Vector3 floor = cell.ToVector3Shifted();
                    floor.y = AltitudeLayer.FloorEmplacement.AltitudeFor();
                    Printer_Plane.PrintPlane(layer, floor, Vector2.one, backingFloor);
                }
            Mesh mesh = MeshAt(thing.Rotation);
            Quaternion facing = Quaternion.AngleAxis(extraRotation, Vector3.up);
            Matrix4x4 matrix = Matrix4x4.TRS(GenThing.TrueCenter(thing) + DrawOffset(thing.Rotation), facing, Vector3.one);
            LayerSubMesh sub = layer.GetSubMesh(MatAt(thing.Rotation, thing));
            int start = sub.verts.Count;
            Vector3[] vertices = mesh.vertices; Vector2[] uv = mesh.uv;
            for (int i = 0; i < vertices.Length; i++)
            {
                sub.verts.Add(matrix.MultiplyPoint3x4(vertices[i]));
                sub.uvs.Add(new Vector3(uv[i].x,uv[i].y,0f));
                sub.colors.Add(new Color32(255,255,255,255));
            }
            foreach (int triangle in mesh.triangles) sub.tris.Add(start + triangle);
        }
    }
}
