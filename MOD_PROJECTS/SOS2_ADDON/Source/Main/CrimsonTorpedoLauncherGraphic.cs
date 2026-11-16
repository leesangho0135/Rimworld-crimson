using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonTorpedoLauncherGraphic : Graphic_Single
    {
        private static readonly Dictionary<Texture, Texture2D> Frames = new Dictionary<Texture, Texture2D>();
        public override bool ShouldDrawRotated { get { return true; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            Texture2D frame;
            if (!Frames.TryGetValue(mat.mainTexture, out frame))
            {
                // Crop only the runtime frame. Retain the generated source PNG
                // and its alpha, and give native ghost shaders a single texture.
                frame = NativeFrame(mat.mainTexture);
                Frames.Add(mat.mainTexture, frame);
            }
            mat = new Material(mat);
            mat.mainTexture = frame;
            mat.mainTextureScale = Vector2.one;
            mat.mainTextureOffset = Vector2.zero;
        }

        private static Texture2D NativeFrame(Texture source)
        {
            const int width = 823, height = 1598;
            Vector2 scale = new Vector2(width / 971f, height / 1620f);
            Vector2 offset = new Vector2(75f / 971f, 12f / 1620f);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(source, target, scale, offset);
                RenderTexture.active = target;
                Texture2D frame = new Texture2D(width, height, TextureFormat.ARGB32, true);
                frame.name = source.name + "ForwardLauncherFrame";
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                frame.wrapMode = TextureWrapMode.Clamp;
                frame.Apply(true, true);
                return frame;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonTorpedoLauncherGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonTorpedoLauncherIcon
    {
        static CrimsonTorpedoLauncherIcon()
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed("Crimson_TorpedoLauncher");
            def.uiIcon = (Texture2D)def.graphicData.Graphic.MatSingle.mainTexture;
            def.uiIconMaterial = null;
        }
    }
}
