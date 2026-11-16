using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Shared surface treatment for the requested furniture, hull and ship
    // equipment only. Source PNGs, alpha, UVs and native lighting stay intact.
    internal static class CrimsonSurfacePalette
    {
        private static readonly Dictionary<Texture, Texture2D> textures = new Dictionary<Texture, Texture2D>();
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
            if (!Includes(request.path)) return;
            // Non-generic graphic initialization avoids Mono's shared generic
            // content-loader bodies. Do not change any source texture's name:
            // native random graphics use those names as resource addresses.
            for (Type type = __instance.GetType(); type != null && typeof(Graphic).IsAssignableFrom(type); type = type.BaseType)
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(Material))
                    {
                        Material source = (Material)field.GetValue(__instance);
                        if (source != null) field.SetValue(__instance, SurfaceMaterial(source, request.path));
                    }
                    else if (field.FieldType == typeof(Material[]))
                    {
                        Material[] originals = (Material[])field.GetValue(__instance);
                        if (originals == null) continue;
                        Material[] materials = (Material[])originals.Clone();
                        for (int i = 0; i < materials.Length; i++) if (materials[i] != null) materials[i] = SurfaceMaterial(materials[i], request.path);
                        field.SetValue(__instance, materials);
                    }
                }
        }

        private static Material SurfaceMaterial(Material original, string path)
        {
            Texture2D source = original.mainTexture as Texture2D;
            if (source == null) return original;
            string resource = path.Substring(0, path.LastIndexOf('/') + 1) + source.name;
            Material result = new Material(original);
            result.mainTexture = Surface(source, resource);
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

        private static float FamilyGain(string path, Texture2D source)
        {
            // Textile-only surfaces and report diagrams have their own intended
            // brightness. Their metal-independent colours must not set a tone curve.
            if (path.Contains("/TacticalReports/") || path.Contains("/Rugs/")) return 1f;
            string family = Family(path);
            float gain;
            if (gains.TryGetValue(family, out gain)) return gain;
            Texture2D reference = Original(family + "_off") ?? Original(family + "_north") ?? Original(family) ?? source;
            Color32[] pixels = Read(reference);
            int[] histogram = new int[256];
            int count = 0, opaque = 0;
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a < 128) continue;
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
            gains.Add(family, gain);
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

        private static Texture2D Apply(Texture source, float gain, bool hullWall = false)
        {
            Texture2D result;
            if (textures.TryGetValue(source, out result)) return result;
            // Cropped native frames inherit their source's treatment. They must
            // not get a second tone curve when furniture submits its solid pass.
            if (treated.Contains(source)) return (Texture2D)source;
            Color32[] pixels = Read(source);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.a == 0) continue;
                int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                int min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                if (IsMetal(pixel) || (hullWall && (max - min) * 100 <= max * 24))
                {
                    float luminance = Luminance(pixel);
                    float highlight = Mathf.Clamp01((luminance - 96f) / 108f);
                    highlight = highlight * highlight * (3f - 2f * highlight);
                    // Preserve the wall's dark panels. Raised rails use W04's
                    // metal values (median 32, upper decile 52) and its existing
                    // frame tint, rather than the previous neutral-gray curve.
                    float tone = hullWall ? (luminance <= 33f ? luminance : 33f + (luminance - 33f) * .35f)
                        : luminance * Mathf.Lerp(gain, 1f, highlight);
                    byte gray = (byte)Mathf.Clamp(Mathf.RoundToInt(tone), 0, 255);
                    pixels[i] = new Color32(gray, gray, gray, pixel.a);
                    if (hullWall && luminance > 36f)
                    {
                        float blend = Mathf.SmoothStep(0f, 1f, (luminance - 36f) / 19f);
                        float frame = Mathf.Max(12f, luminance - 32f);
                        Color tint = CrimsonHullWindowFrame.Tint;
                        pixels[i] = new Color32(
                            (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(tone, frame * tint.r, blend)), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(tone, frame * tint.g, blend)), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(tone, frame * tint.b, blend)), 0, 255), pixel.a);
                    }
                }
                else if (pixel.r >= 48 && pixel.r >= pixel.g * 1.6f && pixel.r >= pixel.b * 1.4f)
                {
                    // Keep each signal's intensity/saturation and all white-hot
                    // highlights; share the ship's existing hue near 357 degrees.
                    byte floor = (byte)Mathf.Min(pixel.g, pixel.b);
                    byte blue = (byte)Mathf.RoundToInt(floor + (pixel.r - floor) / 23f);
                    pixels[i] = new Color32(pixel.r, floor, blue, pixel.a);
                }
            }
            result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true);
            result.name = source.name;
            result.wrapMode = source.wrapMode; result.filterMode = source.filterMode;
            result.SetPixels32(pixels); result.Apply(true, true);
            textures.Add(source, result);
            treated.Add(result);
            return result;
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
