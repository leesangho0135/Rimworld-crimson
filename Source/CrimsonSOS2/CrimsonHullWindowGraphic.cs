using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Keep the selected W04 pixels intact. Stretch only the two glass spans:
    // end frames and the central status light retain their physical width.
    public sealed class CrimsonHullWindowGraphic : Graphic_Single
    {
        private static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, Mesh> frameMeshes = new Dictionary<int, Mesh>();
        private static readonly Dictionary<Material, Material> frameMaterials = new Dictionary<Material, Material>();
        private static Material backingFloor;
        public override bool ShouldDrawRotated { get { return true; } }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonHullWindowGraphic>(path, shader, drawSize,
                color, colorTwo, data, null);
        }

        public override Mesh MeshAt(Rot4 rot) { return WindowMesh(Mathf.RoundToInt(drawSize.x)); }

        private static Mesh WindowMesh(int width)
        {
            Mesh mesh;
            if (meshes.TryGetValue(width, out mesh)) return mesh;
            float half = width * 0.5f;
            float[] x = { -half, -half + 0.24f, -0.09f, 0.09f, half - 0.24f, half };
            float[] u = { 61f / 2172f, 310f / 2172f, 1010f / 2172f,
                1162f / 2172f, 1862f / 2172f, 2111f / 2172f };
            float bottom = 85f / 724f, top = 623f / 724f;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> triangles = new List<int>();
            for (int segment = 0; segment < 5; segment++)
            {
                int start = vertices.Count;
                vertices.Add(new Vector3(x[segment], 0f, -0.5f));
                vertices.Add(new Vector3(x[segment], 0f, 0.5f));
                vertices.Add(new Vector3(x[segment + 1], 0f, 0.5f));
                vertices.Add(new Vector3(x[segment + 1], 0f, -0.5f));
                uv.Add(new Vector2(u[segment], bottom));
                uv.Add(new Vector2(u[segment], top));
                uv.Add(new Vector2(u[segment + 1], top));
                uv.Add(new Vector2(u[segment + 1], bottom));
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            mesh = new Mesh();
            mesh.name = "CrimsonHullWindow" + width;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            meshes.Add(width, mesh); return mesh;
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        {
            Quaternion facing = Quaternion.AngleAxis(rot.AsAngle + extraRotation, Vector3.up);
            Matrix4x4 transform = Matrix4x4.TRS(loc + DrawOffset(rot), facing, Vector3.one);
            Material material = MatAt(rot, thing);
            Graphics.DrawMesh(MeshAt(rot), transform, material, 0);
            Material frame = FrameMaterial(material);
            if (frame != null)
            {
                transform.m13 += 0.001f;
                Graphics.DrawMesh(FrameMesh(Mathf.RoundToInt(drawSize.x)), transform, frame, 0);
            }
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            // Match ordinary Crimson hull walls: hide native archotech support
            // terrain through the artwork's small transparent frame corners.
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
            Vector3 location = GenThing.TrueCenter(thing) + DrawOffset(thing.Rotation);
            Quaternion facing = Quaternion.AngleAxis(thing.Rotation.AsAngle + extraRotation, Vector3.up);
            Matrix4x4 transform = Matrix4x4.TRS(location, facing, Vector3.one);
            Material material = MatAt(thing.Rotation, thing);
            PrintMesh(layer,mesh,transform,material);
            Material frame = FrameMaterial(material);
            if (frame != null)
            {
                transform.m13 += 0.001f;
                PrintMesh(layer,FrameMesh(Mathf.RoundToInt(drawSize.x)),transform,frame);
            }
        }

        private static void PrintMesh(SectionLayer layer, Mesh mesh, Matrix4x4 transform, Material material)
        {
            LayerSubMesh sub = layer.GetSubMesh(material);
            int start = sub.verts.Count;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            for (int i = 0; i < vertices.Length; i++)
            {
                sub.verts.Add(transform.MultiplyPoint3x4(vertices[i]));
                sub.uvs.Add(new Vector3(uv[i].x, uv[i].y, 0f));
                sub.colors.Add(new Color32(255, 255, 255, 255));
            }
            foreach (int triangle in mesh.triangles) sub.tris.Add(start + triangle);
        }

        private static Material FrameMaterial(Material original)
        {
            // Preserve native ghost/blueprint shaders and their requested tint.
            if (original.shader != ShaderDatabase.Cutout && original.shader != ShaderDatabase.CutoutComplex)
                return null;
            Material frame;
            if (!frameMaterials.TryGetValue(original,out frame))
            {
                frame = new Material(original);
                frame.color = original.color * CrimsonHullWindowFrame.Tint;
                frameMaterials.Add(original,frame);
            }
            return frame;
        }

        private static Mesh FrameMesh(int width)
        {
            Mesh result;
            if (frameMeshes.TryGetValue(width,out result)) return result;
            Vector2[] pixels = CrimsonHullWindowFrame.PixelTriangles();
            Vector3[] vertices = new Vector3[pixels.Length];
            Vector2[] uv = new Vector2[pixels.Length]; int[] triangles = new int[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                vertices[i] = CrimsonHullWindowFrame.Position(pixels[i],width);
                uv[i] = CrimsonHullWindowFrame.Uv(pixels[i]); triangles[i] = i;
            }
            result = new Mesh(); result.name = "CrimsonHullWindowFrame"+width;
            result.vertices = vertices; result.uv = uv; result.triangles = triangles;
            result.RecalculateNormals(); result.RecalculateBounds();
            frameMeshes.Add(width,result); return result;
        }
    }
}
