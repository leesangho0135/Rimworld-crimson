using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public class CrimsonFramePaletteGraphicData : GraphicData
    {
        public List<CrimsonPaletteRegion> preservePaletteRegions;

        internal bool PreservesPixel(int index, int width, int height)
        {
            if (preservePaletteRegions == null) return false;
            float x = (index % width + .5f) / width;
            float y = 1f - (index / width + .5f) / height;
            foreach (CrimsonPaletteRegion region in preservePaletteRegions)
                if (region.Contains(x, y)) return true;
            return false;
        }
    }

    public sealed class CrimsonPaletteRegion
    {
        // Normalized source image positions, measured from the upper left.
        public List<Vector2> points = new List<Vector2>();

        private Rect? bounds;

        internal bool Contains(float x, float y)
        {
            if (points.Count < 3) return false;
            if (!bounds.HasValue)
            {
                float left = 1f, top = 1f, right = 0f, bottom = 0f;
                foreach (Vector2 point in points)
                {
                    left = Mathf.Min(left, point.x);
                    top = Mathf.Min(top, point.y);
                    right = Mathf.Max(right, point.x);
                    bottom = Mathf.Max(bottom, point.y);
                }
                bounds = Rect.MinMaxRect(left, top, right, bottom);
            }
            if (!bounds.Value.Contains(new Vector2(x, y))) return false;

            bool inside = false;
            Vector2 previous = points[points.Count - 1];
            foreach (Vector2 point in points)
            {
                if ((point.y > y) != (previous.y > y)
                    && x < point.x + (previous.x - point.x) * (y - point.y) / (previous.y - point.y))
                    inside = !inside;
                previous = point;
            }
            return inside;
        }
    }

    // Shared surface treatment for the requested furniture, hull and ship
    // equipment only. Source PNGs, alpha, UVs and native lighting stay intact.
    internal static class CrimsonSurfacePalette
    {
        private static readonly Dictionary<Texture, Texture2D> textures = new Dictionary<Texture, Texture2D>();
        private static readonly Dictionary<Tuple<Texture, CrimsonFramePaletteGraphicData>, Texture2D> frameTextures =
            new Dictionary<Tuple<Texture, CrimsonFramePaletteGraphicData>, Texture2D>();
        private static readonly HashSet<Texture> treated = new HashSet<Texture>();
        private static readonly Dictionary<string, float> gains = new Dictionary<string, float>();
        private static bool installed;

        internal static void Install()
        {
            if (installed) return;
            Harmony harmony = new Harmony("rimod.crimson.sos2.surface-palette");
            foreach (Type type in new Type[] { typeof(Graphic_Single), typeof(Graphic_Multi), AccessTools.TypeByName("SaveOurShip2.Graphic_SingleOnOff") })
            {
                if (type == null) throw new InvalidOperationException("Missing scoped ship graphic type for surface palette.");
                MethodInfo init = AccessTools.Method(type, "Init", new Type[] { typeof(GraphicRequest) });
                if (init == null) throw new InvalidOperationException("Missing graphic initialization for " + type.FullName);
                if (init.DeclaringType != type) continue;
                harmony.Patch(init,
                    postfix: new HarmonyMethod(typeof(CrimsonSurfacePalette), "GraphicPostfix"));
            }
            installed = true;
        }

        internal static bool Includes(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.StartsWith("Crimson/Decor/", StringComparison.Ordinal)
                || path.StartsWith("Crimson/Hull/", StringComparison.Ordinal)
                || path.StartsWith("Crimson/Power/", StringComparison.Ordinal)
                || path.StartsWith("Crimson/Support/", StringComparison.Ordinal)
                || path.StartsWith("Crimson/Weapons/", StringComparison.Ordinal)
                || path.StartsWith("Crimson/Shield/", StringComparison.Ordinal);
        }

        private static void GraphicPostfix(Graphic __instance, [HarmonyArgument(0)] GraphicRequest request)
        {
            CrimsonFramePaletteGraphicData frame = request.graphicData as CrimsonFramePaletteGraphicData;
            if (frame == null && !Includes(request.path)) return;
            // Non-generic graphic initialization avoids Mono's shared generic
            // content-loader bodies. Do not change any source texture's name:
            // native random graphics use those names as resource addresses.
            for (Type type = __instance.GetType(); type != null && typeof(Graphic).IsAssignableFrom(type); type = type.BaseType)
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(Material))
                    {
                        Material source = (Material)field.GetValue(__instance);
                        if (source != null) field.SetValue(__instance, SurfaceMaterial(source, request.path, frame));
                    }
                    else if (field.FieldType == typeof(Material[]))
                    {
                        Material[] originals = (Material[])field.GetValue(__instance);
                        if (originals == null) continue;
                        Material[] materials = (Material[])originals.Clone();
                        for (int i = 0; i < materials.Length; i++)
                            if (materials[i] != null) materials[i] = SurfaceMaterial(materials[i], request.path, frame);
                        field.SetValue(__instance, materials);
                    }
                }
        }

        private static Material SurfaceMaterial(Material original, string path, CrimsonFramePaletteGraphicData frame)
        {
            Texture2D source = original.mainTexture as Texture2D;
            if (source == null) return original;
            string resource = path.Substring(0, path.LastIndexOf('/') + 1) + source.name;
            Material result = new Material(original);
            result.mainTexture = frame == null ? Surface(source, resource)
                : Apply(source, FamilyGain(resource, source, frame), frame: frame);
            return result;
        }

        internal static Texture2D Surface(Texture2D source, string path)
        {
            if (source == null || !Includes(path)) return source;
            bool hullWall = path == "Crimson/Hull/HullWall_Atlas" || path == "Crimson/Hull/HullWallIcon";
            return Apply(source, FamilyGain(path, source), hullWall);
        }

        internal static Texture2D Original(string path) { return ContentFinder<Texture2D>.Get(path, false); }
        internal static bool IsTreated(Texture texture) { return treated.Contains(texture); }
        internal static void Inherit(Texture source, Texture2D frame)
        {
            if (treated.Contains(source)) treated.Add(frame);
        }

        private static string Family(string path)
        {
            foreach (string direction in new string[] { "_north", "_east", "_south", "_west" })
                if (path.EndsWith(direction, StringComparison.Ordinal)) { path = path.Substring(0, path.Length - direction.Length); break; }
            if (path.EndsWith("/Active", StringComparison.Ordinal)) path = path.Substring(0, path.Length - 6) + "Inactive";
            if (path.EndsWith("_off", StringComparison.Ordinal)) path = path.Substring(0, path.Length - 4);
            return path;
        }

        private static float FamilyGain(string path, Texture2D source, CrimsonFramePaletteGraphicData frame = null)
        {
            // Textile-only surfaces and report diagrams have their own intended
            // brightness. Their metal-independent colours must not set a tone curve.
            if (path.Contains("/TacticalReports/") || path.Contains("/Rugs/")) return 1f;
            string family = Family(path);
            string cacheKey = frame == null ? family : "frame:" + family;
            float gain;
            if (gains.TryGetValue(cacheKey, out gain)) return gain;
            Texture2D reference = Original(family + "_off") ?? Original(family + "_north") ?? Original(family) ?? source;
            Color32[] pixels = Read(reference);
            int[] histogram = new int[256];
            int count = 0, opaque = 0;
            for (int index = 0; index < pixels.Length; index++)
            {
                Color32 pixel = pixels[index];
                if (pixel.a < 128) continue;
                if (frame != null && frame.PreservesPixel(index, reference.width, reference.height)) continue;
                opaque++;
                int luminance = Luminance(pixel);
                if (!IsMetal(pixel) || luminance < 12 || luminance > 160) continue;
                histogram[luminance]++; count++;
            }
            int median = 33, seen = 0;
            if (count >= Mathf.Max(16, opaque / 8))
                for (int i = 0; i < histogram.Length; i++) { seen += histogram[i]; if (seen * 2 >= count) { median = i; break; } }
            // Tanaka/hull reference is neutral RGB 33. Keep the already matched
            // families, never brighten black surfaces, and retain raised highlights.
            gain = median <= 36 ? 1f : Mathf.Clamp(33f / median, .55f, 1f);
            gains.Add(cacheKey, gain);
            return gain;
        }

        internal static bool IsMetal(Color32 pixel)
        {
            int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
            int min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
            if (max >= 204 || pixel.a == 0) return false;
            // Exact ratios keep boundary pixels (50,38,38), for example, in
            // the 24% metal region instead of losing them to float rounding.
            if ((max - min) * 100 <= max * 24) return true;
            // Include the warm gray/brown armour left by newer furniture art.
            // Saturated burgundy cloth, red reflections, blue glass and plants
            // remain outside this region.
            return max <= 160 && (max - min) * 100 <= max * 45 && pixel.r >= pixel.g
                && pixel.g * 100 >= pixel.b * 104 && (pixel.r - pixel.g) * 100 <= max * 28
                && (pixel.g - pixel.b) * 100 <= max * 20;
        }

        private static int Luminance(Color32 pixel)
        {
            return Mathf.RoundToInt(pixel.r * .2126f + pixel.g * .7152f + pixel.b * .0722f);
        }

        internal static Texture2D Metal(Texture source) { return Apply(source, 1f); }

        private static Texture2D Apply(Texture source, float gain, bool hullWall = false,
            CrimsonFramePaletteGraphicData frame = null)
        {
            Texture2D result;
            Tuple<Texture, CrimsonFramePaletteGraphicData> frameKey = frame == null ? null : Tuple.Create(source, frame);
            if (frame == null ? textures.TryGetValue(source, out result) : frameTextures.TryGetValue(frameKey, out result))
                return result;
            // Cropped native frames inherit their source's treatment. They must
            // not get a second tone curve when furniture submits its solid pass.
            if (treated.Contains(source)) return (Texture2D)source;
            Color32[] pixels = Read(source);
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0 || frame != null && frame.PreservesPixel(i, source.width, source.height)) continue;
                pixels[i] = MatchSurface(pixels[i], gain, hullWall, frame != null);
            }
            result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true);
            result.name = source.name;
            result.wrapMode = source.wrapMode; result.filterMode = source.filterMode;
            result.SetPixels32(pixels); result.Apply(true, true);
            if (frame == null) textures.Add(source, result);
            else frameTextures.Add(frameKey, result);
            treated.Add(result);
            return result;
        }

        internal static Color32 MatchSurface(Color32 pixel, float gain, bool hullWall, bool frameOnly)
        {
            if (pixel.a == 0) return pixel;
            int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
            int min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
            if (IsMetal(pixel) || ((hullWall || frameOnly) && (max - min) * 100 <= max * 24))
            {
                float luminance = Luminance(pixel);
                float highlight = Mathf.Clamp01((luminance - 96f) / 108f);
                highlight = highlight * highlight * (3f - 2f * highlight);
                // Raised hull rails retain the existing W04 tone and tint.
                float tone = hullWall ? (luminance <= 33f ? luminance : 33f + (luminance - 33f) * .35f)
                    : luminance * Mathf.Lerp(gain, 1f, highlight);
                if (frameOnly && tone > 52f) tone = 52f + (tone - 52f) * .35f;
                byte gray = (byte)Mathf.Clamp(Mathf.RoundToInt(tone), 0, 255);
                if (hullWall && luminance > 36f)
                {
                    float blend = Mathf.SmoothStep(0f, 1f, (luminance - 36f) / 19f);
                    float frame = Mathf.Max(12f, luminance - 32f);
                    Color tint = CrimsonHullWindowFrame.Tint;
                    return new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(tone, frame * tint.r, blend)), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(tone, frame * tint.g, blend)), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(tone, frame * tint.b, blend)), 0, 255), pixel.a);
                }
                return new Color32(gray, gray, gray, pixel.a);
            }
            if (!frameOnly && pixel.r >= 48 && pixel.r >= pixel.g * 1.6f && pixel.r >= pixel.b * 1.4f)
            {
                // Existing signals share the ship's hue near 357 degrees.
                byte floor = (byte)Mathf.Min(pixel.g, pixel.b);
                byte blue = (byte)Mathf.RoundToInt(floor + (pixel.r - floor) / 23f);
                return new Color32(pixel.r, floor, blue, pixel.a);
            }
            return pixel;
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
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); readable.Apply(false, false);
                return readable.GetPixels32();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (readable != null) UnityEngine.Object.Destroy(readable); }
        }
    }
}
