using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Habitat atlases contain OFF on the left and ON on the right. Only the
    // material changes; native meshes remain. Only the upright IV atlas stays fixed.
    public sealed class CrimsonHabitatGraphic : Graphic_Single
    {
        private Material inactive;
        private bool fixedOrientation;

        public override bool ShouldDrawRotated { get { return !fixedOrientation; } }

        public override float DrawRotatedExtraAngleOffset
        {
            get
            {
                // Match the native sleeping head for live beds and their
                // construction graphics without changing sleeping slots.
                return path == "Crimson/Decor/Habitat/SingleBedStates"
                    || path == "Crimson/Decor/Habitat/DoubleBedStates"
                    || path == "Crimson/Decor/Habitat/MedicalBedStates"
                    ? 180f : base.DrawRotatedExtraAngleOffset;
            }
        }

        public override void Init(GraphicRequest request)
        {
            fixedOrientation = !string.IsNullOrEmpty(request.path)
                && request.path.Substring(request.path.LastIndexOf('/') + 1) == "IVStandStates";
            base.Init(request);
            Material original = mat;
            inactive = CrimsonHabitatRegistration.Register(original, request.path, false);
            mat = CrimsonHabitatRegistration.Register(original, request.path, true);
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation); }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation)));
        }

        public override Material MatSingleFor(Thing thing)
        {
            return MaterialFor(thing);
        }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            return MaterialFor(thing);
        }

        private Material MaterialFor(Thing thing)
        {
            CompPowerTrader power = thing == null ? null : thing.TryGetComp<CompPowerTrader>();
            // Icons and construction graphics have no live power component.
            // Their already-requested shader/color stays on the ON material.
            return power != null && !power.PowerOn ? inactive : mat;
        }

        public override Graphic GetColoredVersion(Shader shader, Color color,
            Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonHabitatGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }
    }

    internal static class CrimsonHabitatRegistration
    {
        internal static Material Register(Material original, string texturePath, bool active)
        {
            if (string.IsNullOrEmpty(texturePath)) return original;
            Rect uv;
            switch (texturePath.Substring(texturePath.LastIndexOf('/') + 1))
            {
                case "SingleBedStates":
                    // Same517x1251 alpha128 body in each frame, plus4px margin.
                    // The ON body is one pixel right of the half-frame origin.
                    uv = AtlasWindow(active ? 580f : 18f, 73f, 525f, 1259f,
                        1122f, 1402f);
                    break;
                case "DoubleBedStates":
                    uv = AtlasWindow(active ? 968.5f : 81f, 57f, 725f, 798f,
                        1774f, 887f);
                    break;
                case "EndTableStates":
                    uv = AtlasWindow(active ? 963f : 76f, 47f, 735f, 801f,
                        1774f, 887f);
                    break;
                case "WardrobeStates":
                    uv = AtlasWindow(active ? 915f : 28f, 256f, 831f, 405f,
                        1774f, 887f);
                    break;
                case "MedicalBedStates":
                    uv = AtlasWindow(active ? 679f : 48f, 36f, 536f, 1174f,
                        1263f, 1246f);
                    break;
                case "VitalsMonitorStates":
                    uv = AtlasWindow(active ? 1108.5f : 22f, 86f, 1042f, 563f,
                        2172f, 724f);
                    break;
                case "MedicineCabinet3x1States":
                    uv = active ? new Rect(0.507891770f,0.324689966f,0.484780158f,0.358511838f) : new Rect(0.007891770f,0.324689966f,0.484216460f,0.358511838f);
                    break;
                case "MedicineCabinetStates":
                    uv = AtlasWindow(active ? 925f : 38f, 234f, 811f, 437f,
                        1774f, 887f);
                    break;
                case "IVStandStates":
                    uv = AtlasWindow(active ? 783f : 206f, 54f, 349f, 1104f,
                        1322f, 1190f);
                    break;
                default:
                    return original;
            }
            Material result = new Material(original);
            // Retain the normal Cutout body request and any native blueprint
            // shader/tint. ON is not replaced by a full-bright lighting shader.
            Vector2 scale = original.mainTextureScale;
            result.mainTextureScale = Vector2.Scale(scale, new Vector2(uv.width, uv.height));
            result.mainTextureOffset = original.mainTextureOffset
                + Vector2.Scale(scale, new Vector2(uv.x, uv.y));
            return result;
        }

        private static Rect AtlasWindow(float left, float top, float width, float height,
            float canvasWidth, float canvasHeight)
        {
            return new Rect(left / canvasWidth, (canvasHeight - top - height) / canvasHeight,
                width / canvasWidth, height / canvasHeight);
        }
    }
}
