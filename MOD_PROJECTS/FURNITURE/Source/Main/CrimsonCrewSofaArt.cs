using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    internal static class CrimsonCrewSofaArt
    {
        private static Rect UV(float x, float y, float width, float height)
        { return new Rect(x/1774f,1f-(y+height)/887f,width/1774f,height/887f); }
        internal static CrimsonV51ArtSpec Create()
        {
            return new CrimsonV51ArtSpec(true,new[] {
                UV(53,163,456,180),UV(628,74,121,340),UV(887,143,538,221),UV(1546,74,121,340),
                UV(53,572,456,180),UV(628,483,121,340),UV(887,551,538,221),UV(1546,483,121,340) },
                new[] {new Vector2(3.03f,1.196053f),new Vector2(1.078324f,3.03f),new Vector2(3.03f,1.244665f),new Vector2(1.078324f,3.03f)},
                new[] {.0980265f,.015f,.1223325f,.015f},true);
        }
    }
    [StaticConstructorOnStartup]
    internal static class CrimsonCrewSofaIcon
    {
        static CrimsonCrewSofaIcon()
        {
            ThingDef def=DefDatabase<ThingDef>.GetNamedSilentFail("Crimson_LoungeSofaThreeSeat");
            if(def==null)return;
            def.uiIcon=(Texture2D)def.graphicData.Graphic.MatAt(Rot4.North).mainTexture;
            def.uiIconMaterial=null;
        }
    }
}
