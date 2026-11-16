using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    internal static class CrimsonCommsConsoleArt
    {
        internal static CrimsonV51ArtSpec Create()
        {
            // Each cardinal view is independently drawn. Side views cover a
            // real 1x3 floor footprint; OFF and ON use the same physical mesh.
            return new CrimsonV51ArtSpec(true, new[] { new Rect(0.517474634f,0.594137542f,0.291995490f,0.255918828f), new Rect(0.370913191f,0.511837655f,0.073844419f,0.462232244f), new Rect(0.016910936f,0.594137542f,0.292559188f,0.255918828f), new Rect(0.857948140f,0.511837655f,0.073844419f,0.462232244f), new Rect(0.517474634f,0.093573844f,0.291431793f,0.255918828f), new Rect(0.370913191f,0.022547914f,0.073844419f,0.463359639f), new Rect(0.017474634f,0.093573844f,0.291995490f,0.255918828f), new Rect(0.857948140f,0.022547914f,0.073844419f,0.463359639f) },
                new[] { new Vector2(2.980000000f,1.305907336f), new Vector2(0.980000000f,2.980000000f), new Vector2(2.980000000f,1.303391137f), new Vector2(0.980000000f,2.980000000f) }, new[] { 0.162953668f,0.000000000f,0.161695568f,0.000000000f }, true);
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonCommsConsoleIcon
    {
        static CrimsonCommsConsoleIcon()
        {
            ThingDef definition = DefDatabase<ThingDef>.GetNamed("Crimson_CommsConsole");
            definition.uiIcon = (Texture2D)definition.graphicData.Graphic.MatAt(Rot4.North).mainTexture;
            definition.uiIconMaterial = null;
        }
    }
}
