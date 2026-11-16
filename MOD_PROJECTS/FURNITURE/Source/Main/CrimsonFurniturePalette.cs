using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Normalize low-chroma metal at upload time, not every draw call. Colored
    // textiles, glass, bright medical linens and vegetation retain their hue.
    internal static class CrimsonFurniturePalette
    {
        internal static Texture2D Metal(Texture source)
        {
            return CrimsonSurfacePalette.Metal(source);
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
