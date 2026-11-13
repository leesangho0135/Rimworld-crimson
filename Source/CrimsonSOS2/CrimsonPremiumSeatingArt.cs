using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Measured UVs preserve raw generated PNGs, physical feet and distinct views.
    internal static class CrimsonPremiumSeatingArt
    {
        internal static CrimsonV51ArtSpec Find(string path)
        {
            switch (path)
            {
                case "Crimson/Decor/Seating/Premium/LoungeChairAtlas": return new CrimsonV51ArtSpec(true, new[] {new Rect(0.02029312f,0.54453213f,0.21025930f,0.32581736f),new Rect(0.27790304f,0.54453213f,0.18827508f,0.33145434f),new Rect(0.51521984f,0.54453213f,0.21984216f,0.32919955f),new Rect(0.78438557f,0.54453213f,0.18827508f,0.33145434f),new Rect(0.02029312f,0.04396843f,0.21025930f,0.32581736f),new Rect(0.27790304f,0.04396843f,0.18827508f,0.33145434f),new Rect(0.51550169f,0.04396843f,0.21984216f,0.32919955f),new Rect(0.78410372f,0.04396843f,0.18827508f,0.33145434f)}, new[] {new Vector2(0.99531088f,0.77116580f),new Vector2(0.89124352f,0.78450777f),new Vector2(1.04067358f,0.77917098f),new Vector2(0.89124352f,0.78450777f)}, new[] {-0.04975389f,-0.04308290f,-0.04575130f,-0.04308290f}, true);
                case "Crimson/Decor/Seating/Premium/DiningChairAtlas": return new CrimsonV51ArtSpec(true, new[] {new Rect(0.06820744f,0.53889515f,0.11499436f,0.39120631f),new Rect(0.31285231f,0.53889515f,0.12344983f,0.41826381f),new Rect(0.56708005f,0.53889515f,0.11612176f,0.41939121f),new Rect(0.81341601f,0.53889515f,0.12344983f,0.41826381f),new Rect(0.06820744f,0.03833145f,0.11499436f,0.39120631f),new Rect(0.31285231f,0.03833145f,0.12344983f,0.41826381f),new Rect(0.56708005f,0.03833145f,0.11612176f,0.41939121f),new Rect(0.81341601f,0.03833145f,0.12344983f,0.41826381f)}, new[] {new Vector2(0.68673267f,1.16811881f),new Vector2(0.73722772f,1.24891089f),new Vector2(0.69346535f,1.25227723f),new Vector2(0.73722772f,1.24891089f)}, new[] {0.14732673f,0.18772277f,0.18940594f,0.18772277f}, true);
                case "Crimson/Decor/Seating/Premium/SofaAtlas": return new CrimsonV51ArtSpec(true, new[] {new Rect(0.01127396f,0.55806088f,0.27057497f,0.28861330f),new Rect(0.30721533f,0.52311161f,0.11555806f,0.46674183f),new Rect(0.49774521f,0.55806088f,0.29255919f,0.30214205f),new Rect(0.84103720f,0.52311161f,0.11555806f,0.46674183f),new Rect(0.01127396f,0.05749718f,0.27057497f,0.28861330f),new Rect(0.30721533f,0.02254791f,0.11555806f,0.46674183f),new Rect(0.49774521f,0.05749718f,0.29255919f,0.30214205f),new Rect(0.84103720f,0.02254791f,0.11555806f,0.46674183f)}, new[] {new Vector2(1.88271845f,1.00411650f),new Vector2(0.80407767f,2f),new Vector2(2.03568932f,1.05118447f),new Vector2(0.80407767f,2f)}, new[] {0.06421359f,0.06215534f,0.08774757f,0.06215534f}, true);
                default: throw new System.ArgumentException("Unknown premium seating atlas: " + path);
            }
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonPremiumSeatingIcons
    {
        static CrimsonPremiumSeatingIcons()
        {
            foreach (string name in new[] { "Crimson_PremiumLoungeChair", "Crimson_PremiumDiningChair", "Crimson_PremiumSofa" })
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamed(name);
                def.uiIcon = (Texture2D)def.graphicData.Graphic.MatAt(Rot4.South, null).mainTexture;
                def.uiIconMaterial = null;
            }
        }
    }
}
