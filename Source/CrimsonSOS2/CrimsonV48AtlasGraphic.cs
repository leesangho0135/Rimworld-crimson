using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // New V48 objects use four physical views. Source PNGs stay unchanged;
    // UV windows and per-view meshes retain upright supports in every direction.
    public sealed class CrimsonV48AtlasGraphic : Graphic_Single
    {
        private readonly Material[] frames = new Material[4];
        private CrimsonV48ArtSpec registration;

        public override bool ShouldDrawRotated { get { return false; } }
        public override Material MatSingle { get { return frames[2] ?? mat; } }
        public override Material MatNorth { get { return frames[0] ?? mat; } }
        public override Material MatEast { get { return frames[1] ?? mat; } }
        public override Material MatSouth { get { return frames[2] ?? mat; } }
        public override Material MatWest { get { return frames[3] ?? mat; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            registration = CrimsonV48ArtRegistration.Find(path, drawSize);
            Material original = mat;
            for (int direction = 0; direction < frames.Length; direction++)
            {
                Rect window = registration.windows[direction];
                Material frame = new Material(original);
                Vector2 originalScale = original.mainTextureScale;
                frame.mainTextureScale = Vector2.Scale(originalScale,
                    new Vector2(window.width, window.height));
                frame.mainTextureOffset = original.mainTextureOffset
                    + Vector2.Scale(originalScale, new Vector2(window.x, window.y));
                if (registration.horizontalTexture != null && (direction & 1) == 1)
                {
                    Texture2D source = ContentFinder<Texture2D>.Get(registration.horizontalTexture);
                    frame.mainTexture = CrimsonV51FurnitureGraphic.NativeFrame(source,
                        window.size, new Vector2(window.x, window.y), direction);
                    frame.mainTextureScale = Vector2.one;
                    frame.mainTextureOffset = Vector2.zero;
                }
                frames[direction] = frame;
            }
            mat = frames[2];
        }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            return frames[Direction(rot)] ?? mat;
        }

        public override Material MatSingleFor(Thing thing)
        {
            return thing == null ? MatSingle : MatAt(thing.Rotation, thing);
        }

        public override Mesh MeshAt(Rot4 rot)
        {
            return MeshPool.GridPlane(registration.sizes[Direction(rot)]);
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef,
            Thing thing, float extraRotation)
        {
            loc.z += registration.offsets[Direction(rot)];
            if (path.StartsWith("Crimson/Decor/Hydroponics/")) base.DrawWorker(loc, rot, thingDef, thing, 0f);
            else CrimsonFurnitureDepth.Draw(this, loc, rot, thingDef, thing, 0f);
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            Rot4 rot = thing.Rotation;
            int direction = Direction(rot);
            if (!path.StartsWith("Crimson/Decor/Hydroponics/"))
            {
                CrimsonFurnitureDepth.Print(layer, thing, this, registration.sizes[direction], registration.offsets[direction]);
                return;
            }
            Vector3 center = RimWorld.GenThing.TrueCenter(thing) + DrawOffset(rot);
            center.z += registration.offsets[direction];
            Printer_Plane.PrintPlane(layer, center, registration.sizes[direction],
                MatAt(rot, thing), 0f, false, null, null, 0.01f, 0f);
        }

        public override Graphic GetColoredVersion(Shader shader, Color color,
            Color colorTwo)
        {
            // Native construction/ghost shaders keep their requested tint.
            if (shader == ShaderDatabase.Cutout || shader == ShaderDatabase.CutoutComplex)
            {
                color = Color.white;
                colorTwo = Color.white;
            }
            return GraphicDatabase.Get<CrimsonV48AtlasGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }

        private static int Direction(Rot4 rot)
        {
            int value = rot.AsInt;
            return value >= 0 && value < 4 ? value : 2;
        }
    }

    internal sealed class CrimsonV48ArtSpec
    {
        internal readonly Rect[] windows;
        internal readonly Vector2[] sizes;
        internal readonly float[] offsets;
        internal readonly string horizontalTexture;

        internal CrimsonV48ArtSpec(Rect[] windows, Vector2[] sizes, float[] offsets,
            string horizontalTexture = null)
        {
            this.windows = windows;
            this.sizes = sizes;
            this.offsets = offsets;
            this.horizontalTexture = horizontalTexture;
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonV48AtlasIcons
    {
        static CrimsonV48AtlasIcons()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.graphicData == null
                    || def.graphicData.graphicClass != typeof(CrimsonV48AtlasGraphic)) continue;
                Register(def);
            }
        }

        internal static void Register(ThingDef def)
        {
            Material front = def.graphicData.Graphic.MatAt(Rot4.South, null);
            Register(def, front);
        }

        internal static void Register(ThingDef def, Material front)
        {
            Texture2D source = ContentFinder<Texture2D>.Get(def.graphicData.texPath);
            Vector2 scale = front.mainTextureScale;
            Vector2 offset = front.mainTextureOffset;
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale.x));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale.y));
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0,
                RenderTextureFormat.ARGB32);
            try
            {
                // Only a runtime icon is baked. No source or deployed PNG is edited.
                Graphics.Blit(source, target, scale, offset);
                RenderTexture.active = target;
                Texture2D icon = new Texture2D(width, height, TextureFormat.ARGB32, false);
                icon.name = def.defName + "V48Icon";
                icon.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                icon.Apply(false, true);
                def.uiIcon = icon;
                def.uiIconMaterial = null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
