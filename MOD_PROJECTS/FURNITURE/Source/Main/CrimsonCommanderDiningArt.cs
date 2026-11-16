using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    internal static class CrimsonCommanderDiningArt
    {
        private static Rect Window(int left, int top, int right, int bottom)
        {
            return new Rect(left / 1536f, (1024 - bottom) / 1024f,
                (right - left) / 1536f, (bottom - top) / 1024f);
        }

        internal static CrimsonV51ArtSpec Create()
        {
            return new CrimsonV51ArtSpec(false,
                new[] { Window(150,111,697,424), Window(1013,39,1291,470),
                    Window(150,623,697,936), Window(1013,550,1291,982) },
                new[] { new Vector2(3.04f,2.10f), new Vector2(2.04f,3.18f),
                    new Vector2(3.04f,2.10f), new Vector2(2.04f,3.18f) },
                new[] { 0.05f,0.05f,0.05f,0.05f }, true);
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonCommanderDiningIcon
    {
        static CrimsonCommanderDiningIcon()
        {
            ThingDef definition = DefDatabase<ThingDef>.GetNamed("Crimson_CommanderDiningTable");
            definition.uiIcon = (Texture2D)definition.graphicData.Graphic.MatNorth.mainTexture;
            definition.uiIconMaterial = null;
        }
    }
}
