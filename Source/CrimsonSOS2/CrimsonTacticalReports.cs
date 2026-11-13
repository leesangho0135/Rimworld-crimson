using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    internal static class CrimsonTacticalReports
    {
        internal static readonly string[] Files = {
            "LaserFireControl", "LaserCapacitorPulse", "LaserHeatLoad", "LaserDamageReport",
            "MissileTrajectory", "MissileSalvoStatus", "StasisBiology", "NavigationCommand",
            "NavigationAudience", "OfficerOperations", "ConductorStrategy"
        };
        internal static readonly string[] Names = {
            "레이저 사격 통제", "레이저 축전·맥동", "레이저 열부하", "레이저 피해 보고",
            "미사일 항적", "미사일 일제사격", "동면 생체 보고", "항행 · 사령실",
            "항행 · 알현실", "장교 작전 보고", "지휘자 전략 보고"
        };
        internal static string Path(int index) { return "Crimson/Decor/Furniture/TacticalReports/" + Files[index]; }
    }

    public sealed class CompProperties_CrimsonTacticalReport : CompProperties
    {
        public CompProperties_CrimsonTacticalReport() { compClass = typeof(CompCrimsonTacticalReport); }
    }

    public sealed class CompCrimsonTacticalReport : ThingComp
    {
        private int report;
        public int SelectedReport { get { return report; } }
        public void SelectReport(int value)
        {
            if (value < 0 || value >= CrimsonTacticalReports.Files.Length || value == report) return;
            report = value;
            if (parent.Spawned) parent.DirtyMapMesh(parent.Map);
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref report, "crimsonTacticalReport", 0);
            if (report < 0 || report >= CrimsonTacticalReports.Files.Length) report = 0;
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            if (parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Action {
                defaultLabel = "투영 화면: " + CrimsonTacticalReports.Names[report],
                defaultDesc = "이 테이블에 표시할 심홍색 장식 보고 화면을 선택합니다.",
                icon = ContentFinder<Texture2D>.Get(CrimsonTacticalReports.Path(report)),
                groupable = false,
                action = delegate {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    for (int i = 0; i < CrimsonTacticalReports.Names.Length; i++)
                    {
                        int selected = i;
                        options.Add(new FloatMenuOption(CrimsonTacticalReports.Names[i], delegate { SelectReport(selected); }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }
        public override string CompInspectStringExtra() { return "투영 화면: " + CrimsonTacticalReports.Names[report]; }
    }

    // Reuse the locked V137 housings, parallel projection and direction sizes.
    // Only the glass's inner polygon is replaced, including the off state.
    // Composites are built lazily once per report/direction/power state; no
    // texture readback, allocations or per-tick material rebuilds are needed.
    public sealed class CrimsonTacticalReportGraphic : Graphic_Single
    {
        private Graphic housing;
        private readonly Dictionary<int, Material> frames = new Dictionary<int, Material>();
        private readonly Dictionary<int, Color32[]> housingPixels = new Dictionary<int, Color32[]>();
        private static readonly Dictionary<int, ReportPixels> Reports = new Dictionary<int, ReportPixels>();
        private sealed class ReportPixels { internal Color32[] pixels; internal int width, height; }
        public override bool ShouldDrawRotated { get { return false; } }
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            housing = GraphicDatabase.Get<CrimsonV51FurnitureGraphic>(path, request.shader,
                drawSize, request.color, request.colorTwo, data, null);
        }
        public override Material MatSingle { get { return housing == null ? base.MatSingle : MaterialFor(Rot4.South, true, 0, null); } }
        public override Material MatNorth { get { return housing == null ? base.MatSingle : MaterialFor(Rot4.North, true, 0, null); } }
        public override Material MatSouth { get { return MatSingle; } }
        public override Material MatEast { get { return housing == null ? base.MatSingle : MaterialFor(Rot4.East, true, 0, null); } }
        public override Material MatWest { get { return MatEast; } }
        public override Material MatAt(Rot4 rotation, Thing thing = null)
        {
            if (housing == null) return base.MatAt(rotation, thing);
            CompCrimsonTacticalReport selection = thing == null ? null : thing.TryGetComp<CompCrimsonTacticalReport>();
            CompPowerTrader power = thing == null ? null : thing.TryGetComp<CompPowerTrader>();
            CompFlickable flick = thing == null ? null : thing.TryGetComp<CompFlickable>();
            bool powered = thing == null || (power != null && power.PowerOn && (flick == null || flick.SwitchIsOn));
            return MaterialFor(rotation, powered, selection == null ? 0 : selection.SelectedReport, thing);
        }
        public override Material MatSingleFor(Thing thing) { return thing == null ? MatSingle : MatAt(thing.Rotation, thing); }
        public override Mesh MeshAt(Rot4 rotation) { return housing.MeshAt(rotation); }
        public override void DrawWorker(Vector3 location, Rot4 rotation, ThingDef definition, Thing thing, float extraRotation)
        {
            location.z += ((CrimsonV51FurnitureGraphic)housing).FrameOffset(rotation);
            CrimsonFurnitureDepth.Draw(this, location, rotation, definition, thing, extraRotation);
        }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation)),
                ((CrimsonV51FurnitureGraphic)housing).FrameOffset(thing.Rotation));
        }
        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            if (shader == ShaderDatabase.Cutout || shader == ShaderDatabase.CutoutComplex) { color = Color.white; colorTwo = Color.white; }
            return GraphicDatabase.Get<CrimsonTacticalReportGraphic>(path, shader, drawSize, color, colorTwo, data, null);
        }
        private Material MaterialFor(Rot4 direction, bool powered, int report, Thing thing)
        {
            int facing = direction.IsHorizontal ? 1 : 0;
            int state = powered ? report + 1 : 0;
            int key = facing * 12 + state;
            Material material;
            if (frames.TryGetValue(key, out material)) return material;
            Rot4 canonical = facing == 0 ? Rot4.North : Rot4.East;
            Material original = housing.MatAt(canonical, thing);
            Texture source = original.mainTexture;
            int w = source.width, h = source.height;
            int baseKey = facing * 2 + (powered ? 1 : 0);
            Color32[] originalPixels;
            if (!housingPixels.TryGetValue(baseKey, out originalPixels))
            {
                originalPixels = ReadPixels(source);
                housingPixels.Add(baseKey, originalPixels);
            }
            Color32[] pixels = (Color32[])originalPixels.Clone();
            Vector2[] polygon = ScreenPolygon(facing != 0);
            float left = float.MaxValue, right = float.MinValue, top = float.MaxValue, bottom = float.MinValue;
            foreach (Vector2 point in polygon) { left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x); top = Mathf.Min(top, point.y); bottom = Mathf.Max(bottom, point.y); }
            ReportPixels image = powered ? Image(report) : null;
            bool portrait = bottom - top > right - left;
            float availableW = portrait ? bottom - top : right - left;
            float availableH = portrait ? right - left : bottom - top;
            float imageW = image == null ? 1 : image.width, imageH = image == null ? 1 : image.height;
            float scale = Mathf.Min(availableW / imageW, availableH / imageH) * .94f;
            float displayW = imageW * scale, displayH = imageH * scale;
            int sampleRadius = Mathf.Clamp(Mathf.CeilToInt(.5f / scale), 1, 2);
            for (int y = Mathf.Max(0, Mathf.FloorToInt(top)); y < Mathf.Min(h, Mathf.CeilToInt(bottom)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(left)); x < Mathf.Min(w, Mathf.CeilToInt(right)); x++)
                {
                    if (!Inside(polygon, x + .5f, y + .5f)) continue;
                    Color32 result = powered ? new Color32(12, 5, 9, 255) : new Color32(9, 6, 9, 255);
                    if (image != null)
                    {
                        float cx = x + .5f - (left + right) * .5f;
                        float cy = y + .5f - (top + bottom) * .5f;
                        float u = (portrait ? cy : cx) / displayW + .5f;
                        float v = (portrait ? -cx : cy) / displayH + .5f;
                        if (u >= 0f && u < 1f && v >= 0f && v < 1f)
                            result = SampleReport(image, u, v, sampleRadius);
                    }
                    pixels[(h - 1 - y) * w + x] = result;
                }
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, true);
            texture.name = "CrimsonTacticalReport" + report + "_" + facing + "_" + powered;
            texture.wrapMode = TextureWrapMode.Clamp; texture.SetPixels32(pixels); texture.Apply(true, true);
            material = new Material(original); material.mainTexture = texture;
            material.mainTextureScale = Vector2.one; material.mainTextureOffset = Vector2.zero;
            frames.Add(key, material); return material;
        }
        private static Color32 SampleReport(ReportPixels image, float u, float v, int radius)
        {
            // Preserve thin signal lines when reducing a large report to the
            // table's small glass texture. Nearest-neighbor alone drops whole
            // graph segments; retain the strongest crimson sample in its footprint.
            int x = Mathf.Min(image.width - 1, (int)(u * image.width));
            int y = image.height - 1 - Mathf.Min(image.height - 1, (int)(v * image.height));
            Color32 brightest = image.pixels[y * image.width + x];
            int x0 = Mathf.Max(0, x - radius), x1 = Mathf.Min(image.width - 1, x + radius);
            int y0 = Mathf.Max(0, y - radius), y1 = Mathf.Min(image.height - 1, y + radius);
            for (int row = y0; row <= y1; row++) for (int column = x0; column <= x1; column++)
            {
                Color32 sample = image.pixels[row * image.width + column];
                if (sample.r > brightest.r) brightest = sample;
            }
            return brightest;
        }
        private static ReportPixels Image(int index)
        {
            ReportPixels result;
            if (!Reports.TryGetValue(index, out result))
            {
                Texture2D texture = ContentFinder<Texture2D>.Get(CrimsonTacticalReports.Path(index));
                int width = Mathf.Min(texture.width, 512);
                int height = Mathf.Max(1, Mathf.RoundToInt(texture.height * ((float)width / texture.width)));
                result = new ReportPixels { pixels = ReadPixels(texture, width, height), width = width, height = height };
                Reports.Add(index, result);
            }
            return result;
        }
        private static Color32[] ReadPixels(Texture source, int width = 0, int height = 0)
        {
            if (width == 0) width = source.width;
            if (height == 0) height = source.height;
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Texture2D readable = null;
            try
            {
                Graphics.Blit(source, target); RenderTexture.active = target;
                readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0); readable.Apply(false, false);
                return readable.GetPixels32();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (readable != null) UnityEngine.Object.Destroy(readable); }
        }
        private Vector2[] ScreenPolygon(bool horizontal)
        {
            int x, y; Vector2[] points;
            if (path.EndsWith("TacticalTableLargeParallelV137", StringComparison.Ordinal))
            {
                x = horizontal ? 829 : 274; y = horizontal ? 142 : 16;
                points = horizontal ? Points(887,177,1229,177,1253,200,1253,334,1231,356,887,356,866,334,866,199)
                    : Points(335,53,480,53,503,75,503,360,480,382,335,382,314,360,314,74);
            }
            else if (path.EndsWith("TacticalTableCompactParallelV137", StringComparison.Ordinal))
            {
                x = horizontal ? 1011 : 240; y = horizontal ? 38 : 195;
                points = horizontal ? Points(1070,79,1143,79,1167,98,1167,398,1145,420,1070,420,1052,402,1052,98)
                    : Points(298,240,682,240,700,257,700,319,681,338,297,338,279,320,279,258);
            }
            else
            {
                x = horizontal ? 972 : 171; y = horizontal ? 27 : 110;
                points = horizontal ? Points(1037,64,1202,64,1227,89,1227,391,1202,415,1040,415,1014,391,1014,88)
                    : Points(238,151,683,151,705,174,705,346,681,369,239,369,216,347,216,174);
            }
            for (int i = 0; i < points.Length; i++) points[i] -= new Vector2(x, y);
            return points;
        }
        private static Vector2[] Points(params int[] values)
        {
            Vector2[] points = new Vector2[values.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(values[i * 2], values[i * 2 + 1]);
            return points;
        }
        private static bool Inside(Vector2[] polygon, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if ((polygon[i].y > y) != (polygon[j].y > y) &&
                    x < (polygon[j].x - polygon[i].x) * (y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            return inside;
        }
    }
}
