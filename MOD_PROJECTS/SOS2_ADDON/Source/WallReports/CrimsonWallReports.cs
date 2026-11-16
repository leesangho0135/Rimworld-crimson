using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CompProperties_CrimsonWallReport : CompProperties
    {
        public CompProperties_CrimsonWallReport()
        {
            compClass = typeof(CompCrimsonWallReport);
        }
    }

    public sealed class CompCrimsonWallReport : ThingComp
    {
        internal const string TextureRoot = "Crimson/Decor/Furniture/WallReports/";
        private static readonly string[] Names = { "우주 항행", "레이저 함포 보고", "미사일 함포 보고", "재배실" };
        private static readonly string[] Paths = { TextureRoot + "Navigation/WallDisplay", TextureRoot + "Laser/WallDisplay", TextureRoot + "Missile/WallDisplay", "Crimson/Decor/Furniture/GrowthDisplay/WallDisplay" };
        private int page;

        private int DefaultPage { get { return parent != null && parent.def != null && parent.def.defName == "Crimson_GrowthWallDisplay" ? 3 : 0; } }
        public int Page { get { return page >= 0 && page < Names.Length ? page : DefaultPage; } }
        internal static string TextureForPage(int index) { return Paths[index]; }
        public override void PostPostMake() { base.PostPostMake(); page = DefaultPage; }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref page, "crimsonWallReportPage", DefaultPage);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) page = Page;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Action
            {
                defaultLabel = "화면 전환",
                defaultDesc = "현재: " + Names[Page] + "\n다음: " + Names[(Page + 1) % Names.Length]
                    + "\n우주 항행 → 레이저 함포 보고 → 미사일 함포 보고 → 재배실 순서로 전환합니다.",
                icon = ContentFinder<Texture2D>.Get(TextureForPage(Page)),
                groupable = false,
                action = delegate { page = (Page + 1) % Names.Length; }
            };
        }

        public override string CompInspectStringExtra()
        {
            return "표시 화면: " + Names[Page];
        }
    }

    // The original power/flick selector still owns the blank off-state. Only
    // its powered material changes; the registered mesh and wall depth remain.
    public sealed class CrimsonWallReportGraphic : Graphic_Single
    {
        private Graphic[] reports;

        public override bool ShouldDrawRotated { get { return true; } }
        public override Material MatSingle { get { return reports == null ? base.MatSingle : reports[0].MatSingle; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            reports = new[]
            {
                ReportGraphic(0, request),
                ReportGraphic(1, request),
                ReportGraphic(2, request),
                ReportGraphic(3, request)
            };
        }

        private static Graphic ReportGraphic(int index, GraphicRequest request)
        {
            return GraphicDatabase.Get<CrimsonFurnitureStateGraphic>(
                CompCrimsonWallReport.TextureForPage(index), request.shader,
                request.drawSize, request.color, request.colorTwo, request.graphicData, null);
        }

        private Graphic Selected(Thing thing)
        {
            CompCrimsonWallReport report = thing == null ? null : thing.TryGetComp<CompCrimsonWallReport>();
            return reports[report == null ? 0 : report.Page];
        }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            return Selected(thing).MatAt(rot, thing);
        }

        public override Material MatSingleFor(Thing thing) { return MatAt(thing == null ? Rot4.North : thing.Rotation, thing); }
        public override Mesh MeshAt(Rot4 rot) { return reports[0].MeshAt(rot); }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        {
            Selected(thing).DrawWorker(loc, rot, def, thing, extraRotation);
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            Selected(thing).Print(layer, thing, extraRotation);
        }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonWallReportGraphic>(path, shader, drawSize, color, colorTwo, data, null);
        }
    }
}
