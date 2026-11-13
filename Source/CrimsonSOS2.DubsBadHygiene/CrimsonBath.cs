using DubsBadHygiene;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Only the CS bath's rendering changes. Water, temperature, use jobs,
    // comfort, quality, hygiene and save state remain native Building_bath.
    [StaticConstructorOnStartup]
    public sealed class CrimsonBath : Building_bath
    {
        private static readonly Material[] waterMaterials = new Material[16];
        private static Mesh foamMesh, foamHighlights;
        private static Material foamMaterial, foamHighlightMaterial;

        private bool HasBathingOccupant
        {
            get
            {
                return occupant != null && occupant.Spawned && !occupant.Destroyed
                    && occupant.Map == Map && WaterInTub > 0f && occupant.CurJob != null
                    && occupant.CurJob.targetA.Thing == this;
            }
        }

        private Vector3 BowlCenter
        {
            get
            {
                // The independently drawn views put the inside of the bowl
                // above the footprint's center, away from its thick front rim.
                Vector3 offset;
                switch (Rotation.AsInt)
                {
                    case 0: offset = new Vector3(-0.05f, 0f, 0.14f); break;
                    case 2: offset = new Vector3(0.06f, 0f, 0.14f); break;
                    case 3: offset = new Vector3(0f, 0f, 0.07f); break;
                    default: offset = new Vector3(0f, 0f, 0.14f); break;
                }
                return DrawPos + offset;
            }
        }

        internal Vector3 BathingPawnDrawPosition(Pawn pawn)
        {
            // DBH's Rotation+180 body angle puts the head at local -Z. Keep
            // enough room inside each view's end wall for the exposed head.
            float headward = Rotation.AsInt == 0 || Rotation.AsInt == 2 ? 0.06f : 0.12f;
            return BowlCenter + Rotation.AsQuat * new Vector3(0f, 0f, -headward * PawnScale(pawn));
        }

        private static float PawnScale(Pawn pawn)
        {
            return pawn.ageTracker == null ? 1f
                : Mathf.Sqrt(pawn.ageTracker.CurLifeStage.bodySizeFactor);
        }

        private static float FoamWidthScale(Pawn pawn)
        {
            BodyTypeDef body = pawn.story == null ? null : pawn.story.bodyType;
            if (body == null) return 1f;
            // Fat's native body graphic is1.5 times as wide; Hulk's painted
            // shoulders need extra room even though its native X scale is1.
            return Mathf.Max(Mathf.Max(1f, body.bodyGraphicScale.x), body == BodyTypeDefOf.Hulk ? 1.35f : 1f);
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            bool drawParent = true;
            for (int i = 0; i < AllComps.Count; i++)
                if (AllComps[i].DontDrawParent()) { drawParent = false; break; }
            if (drawParent) Graphic.Draw(drawLoc, Rotation, this, 0f);
            Comps_DrawAt(drawLoc, flip);
            Comps_PostDraw();

            if (DrawBrokenPipe && pipe != null && brokenPipe
                && (blockageComp == null || !blockageComp.blocked))
            {
                DubUtils.RenderPulsingOverlay(this, GraphicsCache.MissingPipes,
                    DrawPos, MeshPool.plane08, Quaternion.identity);
            }

            // Draw an inset water surface from the actual native fill level,
            // instead of DBH's hardcoded 1x2 white rim/water overlay. Sixteen
            // cached alpha steps avoid allocating a material every draw frame.
            if (waterLevel > 0f)
            {
                int step = Mathf.Clamp(Mathf.RoundToInt(waterLevel * 15f), 1, 15);
                if (waterMaterials[step] == null)
                    waterMaterials[step] = MaterialPool.MatFrom(BaseContent.WhiteTex,
                        ShaderDatabase.Transparent, new Color(0.15f, 0.17f, 0.19f, 0.65f * step / 15f));
                bool bathing = HasBathingOccupant;
                Vector3 surface = BowlCenter;
                surface.y += WaterOffset;
                // The water tint stays below the foam, away from the head.
                if (bathing) surface += Rotation.AsQuat * new Vector3(0f, 0f, 0.35f);
                Graphics.DrawMesh(MeshPool.GridPlane(new Vector2(0.38f, bathing ? 0.62f : 1.05f)),
                    surface, Rotation.AsQuat, waterMaterials[step], 0);
            }
            if (occupant != null) DubUtils.RenderPawnWithY(occupant, DrawPos, 0f);
            if (HasBathingOccupant) DrawBathFoam();
        }

        private void DrawBathFoam()
        {
            if (foamMesh == null)
            {
                // Different sizes and staggered centers make a continuous
                // irregular cloud with circular lobes, without straight edges.
                foamMesh = BubbleMesh(new[] {
                    new Vector3(-0.130f, -0.075f, 0.225f),
                    new Vector3( 0.115f, -0.055f, 0.215f),
                    new Vector3( 0.005f, -0.160f, 0.140f),
                    new Vector3(-0.075f,  0.105f, 0.255f),
                    new Vector3( 0.105f,  0.180f, 0.250f),
                    new Vector3(-0.130f,  0.300f, 0.215f),
                    new Vector3( 0.005f,  0.390f, 0.240f),
                    new Vector3(-0.085f,  0.510f, 0.170f),
                    new Vector3( 0.105f,  0.475f, 0.185f),
                    new Vector3(-0.225f,  0.140f, 0.120f)
                }, "Crimson bath foam");
                foamHighlights = BubbleMesh(new[] {
                    new Vector3(-0.100f, -0.080f, 0.085f),
                    new Vector3( 0.100f,  0.220f, 0.110f),
                    new Vector3(-0.045f,  0.460f, 0.080f)
                }, "Crimson bath foam highlights");
                foamMaterial = MaterialPool.MatFrom(BaseContent.WhiteTex,
                    ShaderDatabase.Transparent, new Color(0.83f, 0.88f, 0.90f, 1f));
                foamHighlightMaterial = MaterialPool.MatFrom(BaseContent.WhiteTex,
                    ShaderDatabase.Transparent, new Color(0.94f, 0.97f, 0.97f, 0.32f));
            }

            // Foam stays on the water, independent of the exposed upper
            // body's headward lean. The opaque overlapping lobes reach the
            // neck and follow the rendered body width across the shoulders.
            Vector3 position = BowlCenter;
            position.y = Mathf.Max(occupant.DrawPos.y + 0.03f, DrawPos.y + WaterOffset + 0.02f);
            float bodyScale = PawnScale(occupant);
            Vector3 scale = new Vector3(bodyScale * FoamWidthScale(occupant), 1f, bodyScale);
            Graphics.DrawMesh(foamMesh, Matrix4x4.TRS(position, Rotation.AsQuat, scale), foamMaterial, 0);
            position.y += 0.002f;
            Graphics.DrawMesh(foamHighlights, Matrix4x4.TRS(position, Rotation.AsQuat, scale), foamHighlightMaterial, 0);
        }

        private static Mesh BubbleMesh(Vector3[] circles, string name)
        {
            const int segments = 28;
            Vector3[] vertices = new Vector3[circles.Length * (segments + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[circles.Length * segments * 3];
            for (int circle = 0; circle < circles.Length; circle++)
            {
                Vector3 shape = circles[circle];
                Vector3 center = new Vector3(shape.x, 0f, shape.y);
                int first = circle * (segments + 1);
                vertices[first] = center;
                uv[first] = new Vector2(0.5f, 0.5f);
                for (int edge = 0; edge < segments; edge++)
                {
                    float angle = edge * Mathf.PI * 2f / segments;
                    float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                    vertices[first + edge + 1] = center + new Vector3(x * shape.z, 0f, z * shape.z);
                    uv[first + edge + 1] = new Vector2((x + 1f) * 0.5f, (z + 1f) * 0.5f);
                    int triangle = (circle * segments + edge) * 3;
                    triangles[triangle] = first;
                    triangles[triangle + 1] = first + (edge + 1) % segments + 1;
                    triangles[triangle + 2] = first + edge + 1;
                }
            }
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
