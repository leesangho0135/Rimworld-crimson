using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonFloorGuideIcons
    {
        static CrimsonFloorGuideIcons()
        {
            Register("Crimson_FloorBeacon", "FloorBeaconStates", 146, 283, 593, 324);
        }

        private static void Register(string defName, string file, int x, int y, int width, int height)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed(defName);
            Texture2D source = ContentFinder<Texture2D>.Get("Crimson/Decor/Guides/" + file);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            try
            {
                // Native build icons discard material UVs. Render only the ON
                // frame into a runtime icon; the source atlas stays unchanged.
                Graphics.Blit(source, target, new Vector2(width / 1774f, height / 887f),
                    new Vector2(x / 1774f, (887f - y - height) / 887f));
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

    // The unchanged atlas contains matched ON/OFF frames. Native power chooses
    // the frame; light emission remains entirely in CompGlower.
    public sealed class CrimsonFloorGuideGraphic : Graphic_Single
    {
        private Material unpowered;
        public override bool ShouldDrawRotated { get { return true; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            Material original = mat;
            mat = Frame(original, 146f, 283f, 593f, 324f);
            unpowered = Frame(original, 1033f, 283f, 593f, 324f);
            // An unlit metal fixture follows the room's normal illumination.
            if (original.shader == ShaderDatabase.TransparentPostLight)
                unpowered.shader = ShaderDatabase.Cutout;
        }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            CompPowerTrader power = thing == null ? null : thing.TryGetComp<CompPowerTrader>();
            return power != null && (power.PowerNet == null || !power.PowerOn)
                ? unpowered : mat;
        }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonFloorGuideGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }

        private static Material Frame(Material original, float x, float y, float width, float height)
        {
            Material result = new Material(original);
            result.mainTextureScale = new Vector2(width / 1774f, height / 887f);
            result.mainTextureOffset = new Vector2(x / 1774f, (887f - y - height) / 887f);
            return result;
        }
    }

    public class CrimsonFloorGuidePlaceWorker : PlaceWorker
    {

        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
            ThingDef target = other as ThingDef;
            return target != null && target.defName == "Crimson_Deck";
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (!loc.InBounds(map)) return false;
            bool deck = false;
            foreach (Thing existing in map.thingGrid.ThingsListAtFast(loc))
            {
                if (existing == thingToIgnore || existing == thing) continue;
                ThingDef target = existing.def.entityDefToBuild as ThingDef ?? existing.def;
                if (target.defName == "Crimson_Deck") deck = true;
                if (target.defName == "Crimson_FloorBeacon"
                    || CrimsonDeckLight.IsLight(target)) return "SpaceAlreadyOccupied".Translate();
                if (target.building != null && target.building.isEdifice) return "Crimson_DeckLightBlocked".Translate();
            }
            if (!deck) return "Crimson_DeckLightNeedsDeck".Translate();
            return true;
        }
    }

}
