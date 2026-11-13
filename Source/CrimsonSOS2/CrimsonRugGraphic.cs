using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Reuses the approved, unmodified A1 textile. Only UVs and native plane
    // geometry adapt its frame to the three occupied floor rectangles.
    public sealed class CrimsonRugGraphic : Graphic_Single
    {
        private const float TextureWidth = 1024f;
        private const float TextureHeight = 1536f;
        private const float ClothLeft = 100f;
        private const float ClothTop = 128f;
        private const float ClothWidth = 825f;
        private const float ClothHeight = 1278f;
        private const float CornerPixels = 80f;
        public const float TileCornerSize = 0.16f;

        public static Rect SourcePixelWindow
        {
            get { return new Rect(ClothLeft, ClothTop, ClothWidth, ClothHeight); }
        }

        public static Rect UVWindow
        {
            get { return new Rect(ClothLeft / TextureWidth,
                1f - (ClothTop + ClothHeight) / TextureHeight,
                ClothWidth / TextureWidth, ClothHeight / TextureHeight); }
        }

        private readonly Mesh[] cardinalMeshes = new Mesh[4];
        private readonly Vector2[][] printedUvs = new Vector2[9][];
        private float[] columns;
        private float[] rows;
        private Vector2[] latticeUvs;

        // MeshAt already rotates the entire floor rectangle. Native
        // AngleFromRot/QuatFromRot must therefore contribute no second turn.
        public override bool ShouldDrawRotated { get { return false; } }

        public override void Init(GraphicRequest request)
        {
            // Keep the requested native shader and both colors, including
            // blueprint/construction tints. Cutout is supplied by the rug Def.
            base.Init(request);

            float corner = Mathf.Min(TileCornerSize,
                Mathf.Min(drawSize.x, drawSize.y) * 0.49f);
            columns = new[] { -drawSize.x / 2f, -drawSize.x / 2f + corner,
                drawSize.x / 2f - corner, drawSize.x / 2f };
            rows = new[] { -drawSize.y / 2f, -drawSize.y / 2f + corner,
                drawSize.y / 2f - corner, drawSize.y / 2f };

            // The source portrait becomes the base wide rectangle through UVs,
            // not by rotating/resampling its PNG. Both axes retain 80px corners.
            float[] sourceColumns = { 0f, CornerPixels,
                ClothHeight - CornerPixels, ClothHeight };
            float[] sourceRows = { 0f, CornerPixels,
                ClothWidth - CornerPixels, ClothWidth };
            latticeUvs = new Vector2[16];
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    float sourceX = ClothLeft + ClothWidth - sourceRows[row];
                    float sourceY = ClothTop + ClothHeight - sourceColumns[column];
                    latticeUvs[row * 4 + column] = new Vector2(
                        sourceX / TextureWidth, 1f - sourceY / TextureHeight);
                }
            }

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    int lowerLeft = row * 4 + column;
                    // Printer_Plane's native order is SW, NW, NE, SE.
                    printedUvs[row * 3 + column] = new[] {
                        latticeUvs[lowerLeft], latticeUvs[lowerLeft + 4],
                        latticeUvs[lowerLeft + 5], latticeUvs[lowerLeft + 1]
                    };
                }
            }
            for (int direction = 0; direction < cardinalMeshes.Length; direction++)
                cardinalMeshes[direction] = MakeMesh(direction);
        }

        public override Mesh MeshAt(Rot4 rot)
        {
            int direction = rot.AsInt;
            if (direction < 0 || direction >= cardinalMeshes.Length) direction = 0;
            return cardinalMeshes[direction];
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            // Native Graphic.Print emits one unsliced plane and does not call
            // MeshAt. Preserve the same nine slices for map meshes/blueprints.
            Rot4 rotation = thing.Rotation;
            float angle = rotation.AsAngle + extraRotation;
            Quaternion turn = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 center = RimWorld.GenThing.TrueCenter(thing) + DrawOffset(rotation);
            Material material = MatAt(rotation, thing);
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    Vector3 localCenter = new Vector3(
                        (columns[column] + columns[column + 1]) / 2f, 0f,
                        (rows[row] + rows[row + 1]) / 2f);
                    Vector2 size = new Vector2(columns[column + 1] - columns[column],
                        rows[row + 1] - rows[row]);
                    Printer_Plane.PrintPlane(layer, center + turn * localCenter,
                        size, material, angle, false,
                        printedUvs[row * 3 + column], null, 0f, 0f);
                }
            }
        }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            // Graphic_Single's implementation would lose this subclass and its
            // slices. Use the identical native request contract with this type.
            return GraphicDatabase.Get<CrimsonRugGraphic>(path, shader, drawSize,
                color, colorTwo, data, null);
        }

        private Mesh MakeMesh(int direction)
        {
            Vector3[] vertices = new Vector3[16];
            Vector3[] normals = new Vector3[16];
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    int index = row * 4 + column;
                    vertices[index] = Turn(columns[column], rows[row], direction);
                    normals[index] = Vector3.up;
                }
            }
            int[] triangles = new int[54];
            int next = 0;
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    int lowerLeft = row * 4 + column;
                    triangles[next++] = lowerLeft;
                    triangles[next++] = lowerLeft + 4;
                    triangles[next++] = lowerLeft + 5;
                    triangles[next++] = lowerLeft;
                    triangles[next++] = lowerLeft + 5;
                    triangles[next++] = lowerLeft + 1;
                }
            }
            Mesh mesh = new Mesh();
            mesh.name = "Crimson A1 sliced rug " + drawSize + " rot " + direction;
            mesh.vertices = vertices;
            mesh.uv = latticeUvs;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Turn(float x, float z, int direction)
        {
            // Exact quarter turns avoid accumulated float offsets at shared
            // edges; every mesh stays centered on the native occupied center.
            switch (direction)
            {
                case 1: return new Vector3(z, 0f, -x);
                case 2: return new Vector3(-x, 0f, -z);
                case 3: return new Vector3(-z, 0f, x);
                default: return new Vector3(x, 0f, z);
            }
        }
    }
}
