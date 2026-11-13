using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonHabitatIcons
    {
        static CrimsonHabitatIcons()
        {
            Register("Crimson_CrewBed");
            Register("Crimson_CrewDoubleBed");
            Register("Crimson_BedsideTable");
            Register("Crimson_Wardrobe");
            Register("Crimson_MedicalBed");
            Register("Crimson_VitalsMonitor");
            Register("Crimson_MedicineCabinet");
            Register("Crimson_IVStand");
        }

        private static void Register(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed(defName);
            // A null Thing selects the registered ON material. Keep its UVs
            // as the single source of the icon crop, including half-pixel edges.
            Material active = def.graphicData.Graphic.MatAt(Rot4.North, null);
            Texture2D source = ContentFinder<Texture2D>.Get(def.graphicData.texPath);
            Vector2 scale = active.mainTextureScale;
            Vector2 offset = active.mainTextureOffset;
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale.x));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale.y));
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0,
                RenderTextureFormat.ARGB32);
            try
            {
                // Native Def icons draw the texture directly and discard atlas
                // UVs. Bake only an in-memory icon; the source PNG stays intact.
                Graphics.Blit(source, target, scale, offset);
                RenderTexture.active = target;
                Texture2D icon = new Texture2D(width, height, TextureFormat.ARGB32, false);
                icon.name = defName + "Icon";
                icon.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                icon.Apply(false, true);
                def.uiIcon = icon;
                def.uiIconMaterial = null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
