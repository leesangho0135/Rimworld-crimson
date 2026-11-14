using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Normalize low-chroma metal at upload time, not every draw call. Colored
    // textiles, glass, bright medical linens and vegetation retain their hue.
    internal static class CrimsonFurniturePalette
    {
        private static readonly Dictionary<Texture, Texture2D> textures = new Dictionary<Texture, Texture2D>();
        internal static Texture2D Metal(Texture source)
        {
            Texture2D result;
            if (textures.TryGetValue(source, out result)) return result;
            Color32[] pixels = Read(source);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                if (p.a == 0) continue;
                float max = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                float min = Mathf.Min(p.r, Mathf.Min(p.g, p.b));
                if (max >= 204f || max - min > max * .24f) continue;
                float luminance = (p.r * .2126f + p.g * .7152f + p.b * .0722f) / 255f;
                byte gray = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(luminance, 1.06f) * 247f), 0, 255);
                pixels[i] = new Color32(gray, gray, gray, p.a);
            }
            result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true);
            result.name = source.name + "_CrimsonNeutralMetal";
            result.wrapMode = TextureWrapMode.Clamp;
            result.SetPixels32(pixels); result.Apply(true, true);
            textures.Add(source, result);
            return result;
        }

        internal static Material Signal(Material on, Material off)
        {
            Color32[] powered = Read(on.mainTexture);
            Color32[] unpowered = Read(off.mainTexture);
            int w = on.mainTexture.width, h = on.mainTexture.height;
            int offW = off.mainTexture.width, offH = off.mainTexture.height;
            bool any = false;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int index = y * w + x;
                Color32 p = powered[index];
                Color32 dark = unpowered[Mathf.Min(offH - 1, y * offH / h) * offW + Mathf.Min(offW - 1, x * offW / w)];
                // A static burgundy cushion is present in both states. Only
                // changed, strongly red signal pixels belong in the unlit pass.
                bool signal = p.a > 100 && dark.a > 100 && p.r > 100
                    && p.r > p.g * 1.6f && p.r > p.b * 1.4f && p.r - dark.r > 55;
                if (!signal) powered[index] = new Color32(0, 0, 0, 0);
                else any = true;
            }
            if (!any) return null;
            Texture2D mask = new Texture2D(w, h, TextureFormat.RGBA32, true);
            mask.name = on.mainTexture.name + "_SignalDelta";
            mask.wrapMode = TextureWrapMode.Clamp;
            mask.SetPixels32(powered); mask.Apply(true, true);
            return MaterialPool.MatFrom(mask, ShaderDatabase.TransparentPostLight, Color.white);
        }

        private static Color32[] Read(Texture source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Texture2D readable = null;
            try
            {
                Graphics.Blit(source, target); RenderTexture.active = target;
                readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply(false, false);
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (readable != null) Object.Destroy(readable);
            }
        }
    }
}
