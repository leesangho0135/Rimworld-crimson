using UnityEngine;
using Verse;
namespace CrimsonSOS2
{
    internal static class CrimsonResearchArt
    {
        internal static CrimsonV51ArtSpec Find(string path)
        {
            switch(path)
            {
                case "Crimson/Decor/Research/CrewSofaTwoAtlas": return new CrimsonV51ArtSpec(true, new[] { new Rect(0.023111612f,0.560315671f,0.267756483f,0.262683202f), new Rect(0.350620068f,0.517474634f,0.076662909f,0.402480271f), new Rect(0.504509583f,0.558060879f,0.284103720f,0.290868095f), new Rect(0.860766629f,0.517474634f,0.076099211f,0.402480271f), new Rect(0.023111612f,0.102593010f,0.267756483f,0.262683202f), new Rect(0.350620068f,0.058624577f,0.076662909f,0.402480271f), new Rect(0.504509583f,0.099210823f,0.284103720f,0.290868095f), new Rect(0.860766629f,0.058624577f,0.076099211f,0.402480271f) }, new[] { new Vector2(2.030000000f,0.995768421f), new Vector2(0.773333333f,2.030000000f), new Vector2(2.030000000f,1.039166667f), new Vector2(0.767647059f,2.030000000f) }, new[] { 0.000000000f, 0.015000000f, 0.019583333f, 0.015000000f }, true);
                case "Crimson/Decor/Research/CoffeeTableAtlas": return new CrimsonV51ArtSpec(false, new[] { new Rect(0.031567080f,0.378804961f,0.280157835f,0.218714769f), new Rect(0.364148816f,0.297632469f,0.098083427f,0.439684329f), new Rect(0.516347238f,0.377677565f,0.282412627f,0.219842165f), new Rect(0.849492672f,0.297632469f,0.098083427f,0.439684329f) }, new[] { new Vector2(2.000000000f,0.780684105f), new Vector2(0.892307692f,2.000000000f), new Vector2(2.000000000f,0.778443114f), new Vector2(0.892307692f,2.000000000f) }, new[] { 0.000000000f, 0.000000000f, 0.000000000f, 0.000000000f }, true);
                case "Crimson/Decor/Research/ToolCabinetAtlas": return new CrimsonV51ArtSpec(false, new[] { new Rect(0.020293123f,0.293122886f,0.320180383f,0.410372041f), new Rect(0.372604284f,0.293122886f,0.092446449f,0.414881623f), new Rect(0.501127396f,0.293122886f,0.337655017f,0.410372041f), new Rect(0.873731680f,0.293122886f,0.092446449f,0.414881623f) }, new[] { new Vector2(2.030000000f,1.300915493f), new Vector2(0.904673913f,2.030000000f), new Vector2(2.030000000f,1.233589316f), new Vector2(0.904673913f,2.030000000f) }, new[] { 0.150457746f, 0.015000000f, 0.116794658f, 0.015000000f }, true);
                case "Crimson/Decor/Research/ResearchBenchAtlas": return new CrimsonV51ArtSpec(true, new[] { new Rect(0.516910936f,0.553551297f,0.267192785f,0.325817362f), new Rect(0.336527621f,0.553551297f,0.128523112f,0.324689966f), new Rect(0.034949267f,0.553551297f,0.262683202f,0.324689966f), new Rect(0.835963923f,0.553551297f,0.128523112f,0.324689966f), new Rect(0.516910936f,0.109357384f,0.267192785f,0.325817362f), new Rect(0.336527621f,0.109357384f,0.128523112f,0.324689966f), new Rect(0.034949267f,0.109357384f,0.262683202f,0.324689966f), new Rect(0.835963923f,0.109357384f,0.128523112f,0.324689966f) }, new[] { new Vector2(2.960000000f,1.960000000f), new Vector2(1.960000000f,2.960000000f), new Vector2(2.960000000f,1.960000000f), new Vector2(1.960000000f,2.960000000f) }, new[] { 0.080000000f, 0.015000000f, 0.080000000f, 0.015000000f }, true);
                case "Crimson/Decor/Research/WallUplinkAtlas": return new CrimsonV51ArtSpec(true, new[] { new Rect(0.530439684f,0.602029312f,0.313979707f,0.329199549f), new Rect(0.385569335f,0.602029312f,0.089064262f,0.329199549f), new Rect(0.030439684f,0.602029312f,0.312852311f,0.329199549f), new Rect(0.868658399f,0.602029312f,0.090755355f,0.329199549f), new Rect(0.531003382f,0.100338219f,0.313979707f,0.329199549f), new Rect(0.385569335f,0.100338219f,0.089064262f,0.329199549f), new Rect(0.030439684f,0.100338219f,0.312852311f,0.329199549f), new Rect(0.868658399f,0.100338219f,0.090755355f,0.329199549f) }, new[] { new Vector2(0.980000000f,0.513752244f), new Vector2(0.530273973f,0.980000000f), new Vector2(0.980000000f,0.515603604f), new Vector2(0.540342466f,0.980000000f) }, new[] { 0.000000000f, 0.000000000f, 0.000000000f, 0.000000000f }, true);
                default: throw new System.ArgumentException("Unknown research furniture atlas: "+path);
            }
        }
    }
    [StaticConstructorOnStartup]
    internal static class CrimsonResearchFurnitureIcons
    {
        static CrimsonResearchFurnitureIcons()
        {
            foreach(string name in new[]{"Crimson_ResearchBench","Crimson_WallResearchUplink","Crimson_ToolCabinet","Crimson_LoungeSofaTwoSeat","Crimson_CoffeeTable"})
            {
                ThingDef def=DefDatabase<ThingDef>.GetNamed(name);
                Rot4 rotation=name=="Crimson_WallResearchUplink"||name=="Crimson_ResearchBench"?Rot4.North:Rot4.South;
                def.uiIcon=(Texture2D)def.graphicData.Graphic.MatAt(rotation).mainTexture;
                def.uiIconMaterial=null;
            }
        }
    }
}
