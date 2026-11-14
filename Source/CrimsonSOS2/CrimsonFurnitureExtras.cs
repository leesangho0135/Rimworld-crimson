using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonExtrasGraphic : Graphic_Single
    {
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation); }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation))); }
        public override bool ShouldDrawRotated { get { return true; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            mat = CrimsonExtrasRegistration.Register(mat, request.path);
        }
    }

    public sealed class CrimsonDirectionalSeatGraphic : Graphic_SingleOnOff
    {
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation);
            Material signal = SignalFor(rot, thing);
            if (signal == null) return;
            Matrix4x4 matrix = CrimsonFurnitureDepth.Transform(this, loc, rot, thing, extraRotation);
            matrix.m13 += .002f;
            Graphics.DrawMesh(MeshAt(rot), matrix, signal, 0);
        }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation)));
            Material signal = SignalFor(thing.Rotation, thing);
            if (signal == null) return;
            Matrix4x4 matrix = CrimsonFurnitureDepth.Transform(this, RimWorld.GenThing.TrueCenter(thing), thing.Rotation, thing, 0f);
            matrix.m13 += .002f;
            Mesh mesh = MeshAt(thing.Rotation);
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            LayerSubMesh sub = layer.GetSubMesh(signal);
            int start = sub.verts.Count;
            for (int i = 0; i < vertices.Length; i++)
            {
                sub.verts.Add(matrix.MultiplyPoint3x4(vertices[i]));
                sub.uvs.Add(new Vector3(uvs[i].x, uvs[i].y, 0f));
                sub.colors.Add(new Color32(255,255,255,255));
            }
            foreach (int index in triangles) sub.tris.Add(start + index);
        }
        private readonly Material[] directions = new Material[4];
        private readonly Material[] directionsOff = new Material[4];
        private readonly Material[] signals = new Material[4];

        public override bool ShouldDrawRotated { get { return false; } }
        public override Material MatNorth { get { return directions[0]; } }
        public override Material MatEast { get { return directions[1]; } }
        public override Material MatSouth { get { return directions[2]; } }
        public override Material MatWest { get { return directions[3]; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            Rect[] windows = new[]
            {
                AtlasWindow(137f, 57f, 354f, 501f),
                AtlasWindow(783f, 55f, 331f, 504f),
                AtlasWindow(129f, 683f, 370f, 513f),
                AtlasWindow(764f, 683f, 334f, 503f)
            };
            for (int index = 0; index < directions.Length; index++)
            {
                int source = CrimsonFurnitureDirections.Source(path, index);
                directions[index] = Frame(mat, windows[source]);
                directionsOff[index] = Frame(matOff, windows[source]);
                if (request.path.EndsWith("CommandSeatDirections") && request.shader == ShaderDatabase.Cutout)
                    signals[index] = CreateSignal(directions[index]);
            }
            mat = directions[0];
            matOff = directionsOff[0];
        }

        private Material SignalFor(Rot4 rot, Thing thing)
        {
            int index = rot.AsInt;
            if (index < 0 || index >= signals.Length || signals[index] == null
                || thing == null || !thing.Spawned || thing.def.defName != "Crimson_CommandSeat"
                || base.MatAt(rot, thing) == matOff) return null;
            return signals[index];
        }

        internal static Material CreateSignal(Material frame)
        {
            // Isolate existing crimson pixels once while loading the graphic.
            // The cushion, metal and shading are never drawn in the unlit pass.
            Texture source = frame.mainTexture;
            int width = Mathf.RoundToInt(source.width * frame.mainTextureScale.x);
            int height = Mathf.RoundToInt(source.height * frame.mainTextureScale.y);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Texture2D mask = null;
            try
            {
                Graphics.Blit(source, target, frame.mainTextureScale, frame.mainTextureOffset);
                RenderTexture.active = target;
                mask = new Texture2D(width, height, TextureFormat.ARGB32, false);
                mask.ReadPixels(new Rect(0f,0f,width,height),0,0);
                Color32[] pixels = mask.GetPixels32();
                bool any = false;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    if (IsSignalPixel(pixel)) any = true;
                    else pixels[i] = new Color32(0,0,0,0);
                }
                if (!any) { Object.Destroy(mask); mask = null; return null; }
                mask.name = "CrimsonFurnitureSignal_" + source.name;
                mask.wrapMode = TextureWrapMode.Clamp;
                mask.SetPixels32(pixels);
                mask.Apply(false, true);
                Material signal = new Material(frame);
                signal.shader = ShaderDatabase.TransparentPostLight;
                signal.mainTexture = mask;
                signal.mainTextureScale = Vector2.one;
                signal.mainTextureOffset = Vector2.zero;
                return signal;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        internal static bool IsSignalPixel(Color32 pixel)
        {
            return pixel.a > 0 && pixel.r > 70 && pixel.r > pixel.g * 1.5f && pixel.r > pixel.b * 1.4f;
        }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            int index = rot.AsInt;
            if (index < 0 || index >= directions.Length) index = 0;
            return base.MatAt(rot, thing) == matOff ? directionsOff[index]
                : directions[index];
        }

        public override Mesh MeshAt(Rot4 rot)
        {
            // The chair keeps vertical legs in every view. Only the bench's
            // occupied footprint changes between long and wide.
            return MeshPool.GridPlane(drawSize);
        }

        public override Graphic GetColoredVersion(Shader shader, Color color,
            Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonDirectionalSeatGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }

        private static Rect AtlasWindow(float left, float top, float width, float height)
        {
            const float canvas = 1254f;
            return new Rect(left / canvas, (canvas - top - height) / canvas,
                width / canvas, height / canvas);
        }

        private static Material Frame(Material original, Rect window)
        {
            Material result = new Material(original);
            result.mainTextureScale = new Vector2(window.width, window.height);
            result.mainTextureOffset = new Vector2(window.x, window.y);
            return result;
        }
    }

    public sealed class CrimsonExtraStateGraphic : Graphic_SingleOnOff
    {
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation); }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation))); }
        public override bool ShouldDrawRotated { get { return true; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            mat = CrimsonExtrasRegistration.Register(mat, request.path);
            matOff = CrimsonExtrasRegistration.Register(matOff, request.path);
        }
    }

    internal static class CrimsonArtifactLightShape
    {
        internal static Vector2[] FrameUvs()
        {
            // Inset the four corners inside the existing artifact silhouette.
            // Coordinates use the powered frame's existing crop, not the case.
            return new[] { FrameUv(445f,699f), FrameUv(540f,596f),
                FrameUv(635f,699f), FrameUv(540f,821f) };
        }

        private static Vector2 FrameUv(float pixelX, float pixelY)
        { return new Vector2((pixelX - 210f) / 663f, (1249f - pixelY) / 1043f); }

        internal static Color Tint { get { return new Color(1.9f,0.24f,0.32f,1f); } }
    }

    public sealed class CrimsonFixedArtifactGraphic : Graphic_SingleOnOff
    {
        private Material artifactLight;
        private Mesh artifactMesh;
        private Vector3[] artifactVertices;
        private Vector2[] artifactUvs;
        public override bool ShouldDrawRotated { get { return false; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            mat = CrimsonExtrasRegistration.Register(mat, request.path);
            matOff = CrimsonExtrasRegistration.Register(matOff, request.path);
            artifactLight = new Material(mat);
            artifactLight.shader = ShaderDatabase.TransparentPostLight;
            artifactLight.color = mat.color * CrimsonArtifactLightShape.Tint;
            artifactUvs = CrimsonArtifactLightShape.FrameUvs();
            artifactVertices = new Vector3[artifactUvs.Length];
            for (int i = 0; i < artifactUvs.Length; i++)
                artifactVertices[i] = new Vector3((artifactUvs[i].x - 0.5f) * drawSize.x,
                    0f, (artifactUvs[i].y - 0.5f) * drawSize.y);
            artifactMesh = new Mesh();
            artifactMesh.vertices = artifactVertices;
            artifactMesh.uv = artifactUvs;
            artifactMesh.triangles = new[] { 0,1,2,0,2,3 };
            artifactMesh.RecalculateBounds();
            artifactMesh.RecalculateNormals();
        }

        public override Mesh MeshAt(Rot4 rot)
        {
            return MeshPool.GridPlane(drawSize);
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef,
            Thing thing, float extraRotation)
        {
            // Existing saved rotations stay intact; the case always uses its
            // upright front image, including construction previews.
            CrimsonFurnitureDepth.Draw(this, loc, Rot4.North, thingDef, thing, 0f);
            if (!ArtifactPowered(thing)) return;
            Matrix4x4 matrix = ArtifactTransform(loc, thing);
            Graphics.DrawMesh(artifactMesh, matrix, artifactLight, 0);
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation)));
            if (!ArtifactPowered(thing)) return;
            Matrix4x4 matrix = ArtifactTransform(RimWorld.GenThing.TrueCenter(thing), thing);
            LayerSubMesh sub = layer.GetSubMesh(artifactLight);
            int start = sub.verts.Count;
            for (int i = 0; i < artifactVertices.Length; i++)
            {
                sub.verts.Add(matrix.MultiplyPoint3x4(artifactVertices[i]));
                sub.uvs.Add(new Vector3(artifactUvs[i].x, artifactUvs[i].y, 0f));
                sub.colors.Add(new Color32(255,255,255,255));
            }
            sub.tris.Add(start); sub.tris.Add(start+1); sub.tris.Add(start+2);
            sub.tris.Add(start); sub.tris.Add(start+2); sub.tris.Add(start+3);
        }

        private static bool ArtifactPowered(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.def.defName != "Crimson_ArtifactCase") return false;
            RimWorld.CompPowerTrader power = thing.TryGetComp<RimWorld.CompPowerTrader>();
            RimWorld.CompFlickable flick = thing.TryGetComp<RimWorld.CompFlickable>();
            return power != null && power.PowerOn && (flick == null || flick.SwitchIsOn);
        }

        private Matrix4x4 ArtifactTransform(Vector3 loc, Thing thing)
        {
            Matrix4x4 matrix = CrimsonFurnitureDepth.Transform(this, loc, Rot4.North, thing, 0f);
            matrix.m13 += 0.002f;
            return matrix;
        }

        public override Graphic GetColoredVersion(Shader shader, Color color,
            Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonFixedArtifactGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }
    }

    public sealed class CrimsonHoverDecorGraphic : Graphic_SingleOnOff
    {
        private Graphic pedestal;

        public override bool ShouldDrawRotated { get { return false; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            mat = CrimsonExtrasRegistration.Register(mat, request.path);
            matOff = CrimsonExtrasRegistration.Register(matOff, request.path);
            pedestal = GraphicDatabase.Get<CrimsonExtraStateGraphic>(
                "Crimson/Decor/Extras/HoverPlinth", request.shader,
                new Vector2(2f, CrimsonDecorHoverMotion.BaseDepth),
                request.color, request.colorTwo);
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef,
            Thing thing, float extraRotation)
        {
            Vector3 baseLoc = loc;
            baseLoc.z += CrimsonDecorHoverMotion.BaseCenterZ;
            pedestal.Draw(baseLoc, Rot4.North, thing);

            float offset = 0f;
            // Construction previews stay still. Only the completed building bobs.
            if (thing is Building && thing.def.entityDefToBuild == null
                && base.MatAt(rot, thing) != matOff
                && Find.TickManager != null)
            {
                offset = CrimsonDecorHoverMotion.Offset(Find.TickManager.TicksGame,
                    thing.thingIDNumber);
            }
            Vector3 bodyLoc = loc;
            bodyLoc.y += 0.001f;
            float projectedHeight = CrimsonDecorHoverMotion.CenterZ(drawSize.y) + offset;
            bodyLoc.z += projectedHeight;
            CrimsonFurnitureDepth.Draw(this, bodyLoc, Rot4.North, thingDef, thing, 0f);
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Print(layer, thing, pedestal, new Vector2(2f, CrimsonDecorHoverMotion.BaseDepth), CrimsonDecorHoverMotion.BaseCenterZ);
            float bob = base.MatAt(thing.Rotation, thing) == matOff || Find.TickManager == null ? 0f
                : CrimsonDecorHoverMotion.Offset(Find.TickManager.TicksGame, thing.thingIDNumber);
            CrimsonFurnitureDepth.Print(layer, thing, this, drawSize, CrimsonDecorHoverMotion.CenterZ(drawSize.y) + bob);
        }

        public override Graphic GetColoredVersion(Shader shader, Color color,
            Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonHoverDecorGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }
    }

    internal static class CrimsonExtrasRegistration
    {
        internal static Material Register(Material original, string texturePath)
        {
            Rect uv;
            switch (texturePath.Substring(texturePath.LastIndexOf('/') + 1))
            {
                case "CommandSeat":
                    uv = new Rect(188f / 1199f, 69f / 1312f, 808f / 1199f, 1175f / 1312f);
                    break;
                case "CommsPanel":
                    uv = original.mainTexture != null
                        && original.mainTexture.name.EndsWith("_off")
                        ? new Rect(121f / 1254f, 121f / 1254f, 988f / 1254f, 1014f / 1254f)
                        : new Rect(122f / 1262f, 97f / 1246f, 1013f / 1262f, 1038f / 1246f);
                    break;
                case "ArtifactCase":
                    uv = original.mainTexture != null
                        && original.mainTexture.name.EndsWith("_off")
                        ? new Rect(203f / 1083f, 188f / 1453f, 676f / 1083f, 1057f / 1453f)
                        : new Rect(210f / 1083f, 204f / 1453f, 663f / 1083f, 1043f / 1453f);
                    break;
                case "CrewBench":
                    uv = new Rect(81f / 1774f, 97f / 887f, 1612f / 1774f, 660f / 887f);
                    break;
                case "WallStripLight":
                    uv = new Rect(128f / 2172f, 117f / 724f, 1917f / 2172f, 456f / 724f);
                    break;
                case "ResonanceRingBody":
                    uv = new Rect(118f / 1312f, 82f / 1199f, 1076f / 1312f, 1034f / 1199f);
                    break;
                case "HoverPlinth":
                    uv = new Rect(119f / 2172f, 81f / 724f, 1936f / 2172f, 542f / 724f);
                    break;
                case "PresenceBody":
                    uv = new Rect(104f / 1024f, 29f / 1536f, 817f / 1024f, 1484f / 1536f);
                    break;
                default:
                    return original;
            }
            Material result = new Material(original);
            result.mainTextureScale = new Vector2(uv.width, uv.height);
            result.mainTextureOffset = new Vector2(uv.x, uv.y);
            return result;
        }
    }
}
