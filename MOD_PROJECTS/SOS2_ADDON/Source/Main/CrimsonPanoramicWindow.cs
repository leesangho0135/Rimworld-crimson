using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // The original sprites are cropped by UVs and use the shared frame palette.
    public sealed class CrimsonPanoramaGraphicData : CrimsonFramePaletteGraphicData
    {
        public Vector2 uvScale = Vector2.one;
        public Vector2 uvOffset = Vector2.zero;
    }

    public sealed class CrimsonPanoramaGraphic : Graphic_Single
    {
        public override bool ShouldDrawRotated { get { return true; } }
        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        { return GraphicDatabase.Get<CrimsonPanoramaGraphic>(path, shader, drawSize, color, colorTwo, data, null); }
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            CrimsonPanoramaGraphicData layout = request.graphicData as CrimsonPanoramaGraphicData;
            if (layout == null) return;
            mat = new Material(mat);
            mat.mainTextureScale = layout.uvScale;
            mat.mainTextureOffset = layout.uvOffset;
        }
    }

    public sealed class Building_CrimsonPanoramicWindow : Building
    {
        public const string DefName = "Crimson_PanoramicWindowWide";

        public static bool IsBackingWall(ThingDef definition)
        {
            return definition != null && definition.building != null
                && definition.building.isEdifice
                && definition.passability == Traversability.Impassable;
        }

        public bool HasBackingWall
        {
            get
            {
                if (!Spawned) return false;
                foreach (IntVec3 cell in this.OccupiedRect())
                {
                    Building wall = cell.GetEdifice(Map);
                    if (wall == null || !IsBackingWall(wall.def)) return false;
                }
                return true;
            }
        }

        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n고정 우주 풍경 · 벽걸이 장식창\n"
                + (HasBackingWall ? "기존 벽 " + def.size.x + "칸 유지"
                    : "사용 불가: 뒤쪽에 연속된 벽 " + def.size.x + "칸이 필요합니다.");
        }
    }

    public sealed class CrimsonPanoramicWindowPlaceWorker : PlaceWorker
    {
        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
            ThingDef definition = other as ThingDef;
            if (definition != null) definition = definition.entityDefToBuild as ThingDef ?? definition;
            return Building_CrimsonPanoramicWindow.IsBackingWall(definition);
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            ThingDef definition = checkingDef as ThingDef;
            if (definition == null) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(loc, rot, definition.size))
            {
                if (!cell.InBounds(map)) return false;
                Building wall = cell.GetEdifice(map);
                if (wall == null || !Building_CrimsonPanoramicWindow.IsBackingWall(wall.def))
                    return "통행을 막는 기존 벽 " + definition.size.x + "칸 위에 설치해야 합니다. 벽은 철거되지 않습니다.";
                foreach (Thing existing in cell.GetThingList(map))
                {
                    if (existing == thing || existing == thingToIgnore) continue;
                    ThingDef built = existing.def.entityDefToBuild as ThingDef ?? existing.def;
                    if (built.thingClass == typeof(Building_CrimsonPanoramicWindow))
                        return "이미 파노라마 전망창이 설치되어 있습니다.";
                }
            }
            return true;
        }
    }
}
