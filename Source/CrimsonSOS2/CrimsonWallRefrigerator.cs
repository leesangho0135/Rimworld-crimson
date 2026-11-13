using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // One flat wall insert rotates naturally; the generated atlas has the
    // powered image above the otherwise identical unpowered image.
    public sealed class CrimsonWallRefrigeratorGraphic : Graphic_Single
    {
        private Material on, off;
        public override bool ShouldDrawRotated { get { return true; } }
        public override Material MatSingle { get { return on ?? mat; } }
        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            CompPowerTrader power = thing == null ? null : thing.TryGetComp<CompPowerTrader>();
            CompFlickable flick = thing == null ? null : thing.TryGetComp<CompFlickable>();
            return thing == null || (power != null && power.PowerOn && (flick == null || flick.SwitchIsOn)) ? on : off;
        }
        public override Material MatSingleFor(Thing thing) { return MatAt(thing == null ? Rot4.South : thing.Rotation, thing); }
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            on = new Material(mat); off = new Material(mat);
            on.mainTexture = NativeFrame(mat.mainTexture, new Rect(27f, 77f, 1200f, 544f));
            off.mainTexture = NativeFrame(mat.mainTexture, new Rect(27f, 664f, 1200f, 545f));
            on.mainTextureScale = off.mainTextureScale = Vector2.one;
            on.mainTextureOffset = off.mainTextureOffset = Vector2.zero;
        }
        private static Texture2D NativeFrame(Texture source, Rect pixels)
        {
            Vector2 scale = new Vector2(pixels.width / source.width, pixels.height / source.height);
            Vector2 offset = new Vector2(pixels.x / source.width, 1f - pixels.yMax / source.height);
            int width = Mathf.RoundToInt(pixels.width), height = Mathf.RoundToInt(pixels.height);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(source, target, scale, offset); RenderTexture.active = target;
                Texture2D result = new Texture2D(width, height, TextureFormat.ARGB32, false);
                result.name = source.name + "WallFrame" + pixels.y;
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0); result.wrapMode = TextureWrapMode.Clamp;
                result.Apply(false, true); return result;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        public override Graphic GetColoredVersion(Shader shader, Color first, Color second)
        { return GraphicDatabase.Get<CrimsonWallRefrigeratorGraphic>(path, shader, drawSize, first, second, data); }
    }
}
