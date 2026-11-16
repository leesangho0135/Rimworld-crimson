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
                Rot4 rotation=Rot4.North;
                def.uiIcon=(Texture2D)def.graphicData.Graphic.MatAt(rotation).mainTexture;
                def.uiIconMaterial=null;
            }
        }
    }
}
