using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    internal static class CrimsonElectricSmelterArt
    {
        private static Rect Window(int left, int top, int right, int bottom)
        {
            return new Rect(left / 1774f, (887 - bottom) / 887f,
                (right - left) / 1774f, (bottom - top) / 887f);
        }

        internal static CrimsonV51ArtSpec Create()
        {
            // Separate cardinal artwork keeps the 2x3 side views broad enough
            // and places their controls along the native interaction edge.
            return new CrimsonV51ArtSpec(true, new[] {
                Window(64,133,524,396), Window(586,52,832,397),
                Window(911,132,1373,395), Window(1447,52,1693,397),
                Window(64,577,524,840), Window(586,498,832,843),
                Window(911,577,1373,840), Window(1447,498,1693,843)
            }, new[] {
                new Vector2(2.96f,1.96f), new Vector2(1.96f,2.96f),
                new Vector2(2.96f,1.96f), new Vector2(1.96f,2.96f)
            }, new[] { 0f,0f,0f,0f }, true);
        }
        internal static CrimsonV51ArtSpec CreateRecessed()
        {
            return new CrimsonV51ArtSpec(true, new[] {
                Window(65,131,524,396), Window(586,50,830,398),
                Window(911,130,1374,395), Window(1448,50,1693,398),
                Window(65,576,524,841), Window(586,494,830,842),
                Window(911,575,1374,840), Window(1448,494,1693,842)
            }, new[] {
                new Vector2(2.96f,1.96f), new Vector2(1.96f,2.96f),
                new Vector2(2.96f,1.96f), new Vector2(1.96f,2.96f)
            }, new[] { 0f,0f,0f,0f }, true);
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonElectricSmelterIcon
    {
        static CrimsonElectricSmelterIcon()
        {
            ThingDef definition=DefDatabase<ThingDef>.GetNamed("Crimson_ElectricSmelter");
            definition.uiIcon=(Texture2D)definition.graphicData.Graphic.MatNorth.mainTexture;
            definition.uiIconMaterial=null;
        }
    }
}
