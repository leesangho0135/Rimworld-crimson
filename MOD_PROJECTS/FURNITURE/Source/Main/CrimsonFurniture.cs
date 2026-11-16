using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Keep SOS2's native power-state choice and protected material slots.
    // Both states share a measured UV window; generated PNGs stay unchanged.
    public sealed class CrimsonFurnitureStateGraphic : Graphic_SingleOnOff
    {
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation); }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation))); }
        public override bool ShouldDrawRotated { get { return true; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            mat = CrimsonFurnitureRegistration.Register(mat, request.path);
            matOff = CrimsonFurnitureRegistration.Register(matOff, request.path);
        }
    }

    public sealed class CrimsonFurnitureGraphic : Graphic_Single
    {
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation); }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation))); }
        public override bool ShouldDrawRotated { get { return true; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            mat = CrimsonFurnitureRegistration.Register(mat, request.path);
        }
    }

    public sealed class CrimsonFurnitureWallPlaceWorker : PlaceWorker
    {
        private readonly PlaceWorker backingRule = new CrimsonLifeSupportWallPlaceWorker();

        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
            return other != null && other.defName == "Crimson_Deck";
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            // The existing public rule already checks every backing wall cell.
            // No life-support implementation or shared wall flag is changed.
            AcceptanceReport backing = backingRule.AllowsPlacing(checkingDef, loc, rot,
                map, thingToIgnore, thing);
            if (!backing.Accepted) return backing;
            ThingDef buildingDef = checkingDef as ThingDef;
            if (buildingDef == null) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(loc, rot, buildingDef.size))
            {
                if (!cell.InBounds(map)) return false;
                foreach (Thing existing in map.thingGrid.ThingsListAtFast(cell))
                {
                    if (existing == thingToIgnore || existing == thing) continue;
                    ThingDef built = existing.def.entityDefToBuild as ThingDef ?? existing.def;
                    if (built == buildingDef) return "SpaceAlreadyOccupied".Translate();
                }
            }
            return true;
        }
    }

    internal static class CrimsonFurnitureRegistration
    {
        internal static Material Register(Material original, string texturePath)
        {
            Rect uv;
            switch (texturePath.Substring(texturePath.LastIndexOf('/') + 1))
            {
                case "WallDisplay":
                    uv = new Rect(66f / 2172f, 124f / 724f, 2063f / 2172f, 468f / 724f);
                    break;
                case "ServerPort":
                    uv = original.mainTexture != null
                        && original.mainTexture.name.EndsWith("_off")
                        ? new Rect(163f / 1008f, 67f / 1561f, 682f / 1008f, 1413f / 1561f)
                        : new Rect(165f / 992f, 68f / 1586f, 662f / 992f, 1432f / 1586f);
                    break;
                default:
                    return original;
            }
            Material result = new Material(original);
            result.mainTextureScale = new Vector2(uv.width, uv.height);
            result.mainTextureOffset = new Vector2(uv.x, uv.y);
            return result;
        }
    }
}
