using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Each atlas is raw generated art: four independently drawn cardinal views,
    // OFF above ON. UVs isolate the actual silhouettes without editing pixels.
    public sealed class CrimsonStorageHygieneGraphic : Graphic_Single
    {
        private readonly Material[] active = new Material[4];
        private readonly Material[] inactive = new Material[4];
        private bool rectangularFootprint;
        private int defaultDirection = 2;

        public override bool ShouldDrawRotated { get { return false; } }
        public override Material MatSingle { get { return active[defaultDirection] ?? mat; } }
        public override Material MatNorth { get { return active[0] ?? mat; } }
        public override Material MatEast { get { return active[1] ?? mat; } }
        public override Material MatSouth { get { return active[2] ?? mat; } }
        public override Material MatWest { get { return active[3] ?? mat; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            string key = request.path.Substring(request.path.LastIndexOf('/') + 1);
            bool bath = key == "BathtubStates";
            bool basin = key == "BasinV43States" || key == "BasinV44States" || key == "BasinV119States";
            defaultDirection = bath ? 1 : basin ? 0 : 2;
            rectangularFootprint = key == "StandardShelfStates"
                || key == "LargeShelfStates" || key == "BathtubStates"
                || key == "BilliardsTableStates" || key == "LargeTelevisionStates";
            Rect[] windows = Windows(key);
            if (IsFollowupAtlas(key)) windows = PreserveAspect(windows, drawSize);
            float onRow = key == "BasinV44States" || key == "BasinV119States" ? 407f : 443f;
            Material original = mat;
            for (int direction = 0; direction < active.Length; direction++)
            {
                // Dubs uses a native1x2 bath and Rotation+180 for the body.
                // Keep that convention and map it to the matching atlas view.
                int artDirection = direction;
                if (bath) artDirection = (direction + 1) % 4;
                // DBH's basin faces its local -Z interaction cell. The raw
                // atlas labels describe the opposite physical facing, so its
                // bowl must face the user while the mounting face meets wall.
                else if (basin) artDirection = (direction + 2) % 4;
                inactive[direction] = Frame(original, windows[artDirection], false, onRow);
                active[direction] = Frame(original, windows[artDirection], true, onRow);
            }
            mat = active[defaultDirection];
        }

        public override Material MatSingleFor(Thing thing)
        {
            return thing == null ? MatSingle : MatAt(thing.Rotation, thing);
        }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            int direction = rot.AsInt;
            if (direction < 0 || direction >= active.Length) direction = defaultDirection;
            CompPowerTrader power = thing == null ? null : thing.TryGetComp<CompPowerTrader>();
            CompFlickable flick = thing == null ? null : thing.TryGetComp<CompFlickable>();
            bool on = (power == null || power.PowerOn)
                && (flick == null || flick.SwitchIsOn);
            // Native blueprint/construction requests have no live Thing. Their
            // shader and tint stay intact while displaying one powered frame.
            return on ? active[direction] : inactive[direction];
        }

        public override Mesh MeshAt(Rot4 rot)
        {
            // Cardinal artwork keeps upright columns and feet upright. Only
            // rectangular floor footprints exchange width and length.
            Vector2 size = rectangularFootprint && rot.IsHorizontal
                ? new Vector2(drawSize.y, drawSize.x) : drawSize;
            return MeshPool.GridPlane(size);
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation)));
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation); }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonStorageHygieneGraphic>(
                path, shader, drawSize, color, colorTwo, data, null);
        }

        private static Rect[] Windows(string key)
        {
            // Alpha128 bounds were measured read-only. Each OFF/ON pair uses
            // exactly the same local window; Init selects the atlas row spacing.
            switch (key)
            {
                case "ToiletV44AngularStates":
                    // DBH's UseToilet faces the pawn opposite the building's
                    // rotation. Match the bowl's front to that native facing.
                    return new[] { Pixel(955,111,304,305), Pixel(1373,115,361,301),
                        Pixel(77,112,292,304), Pixel(483,115,363,301) };
                case "BasinV44States":
                    return new[] { Pixel(25,76,395,334), Pixel(479,84,388,329),
                        Pixel(907,76,403,333), Pixel(1348,84,385,329) };
                case "BasinV119States":
                    return new[] { Pixel(22,78,400,327), Pixel(477,86,385,325),
                        Pixel(904,80,415,325), Pixel(1357,86,384,325) };
                case "PokerTableV44States":
                    return new[] { Pixel(73,83,298,303), Pixel(519,84,293,302),
                        Pixel(962,84,295,302), Pixel(1408,84,293,302) };
                case "ToiletV43States":
                    return new[] { Pixel(93,58,267,342), Pixel(509,87,328,299),
                        Pixel(972,58,266,342), Pixel(1374,87,326,299) };
                case "BasinV43States":
                    return new[] { Pixel(29,110,388,239), Pixel(495,109,330,244),
                        Pixel(925,106,367,222), Pixel(1392,109,329,244) };
                case "LoungeChairV43States":
                    return new[] { Pixel(54,95,336,298), Pixel(517,92,299,301),
                        Pixel(939,88,338,305), Pixel(1400,92,300,301) };
                case "DiningChairV43States":
                    return new[] { Pixel(128,65,188,342), Pixel(556,35,205,372), Pixel(1014,45,190,364), Pixel(1457,35,205,372) };
                case "WorkChairV43States":
                    return new[] { Pixel(98,55,248,368), Pixel(554,23,227,400), Pixel(984,39,250,384), Pixel(1437,23,227,400) };
                case "ToiletV42States":
                    return new[] { Pixel(105,36,233,381), Pixel(528,33,281,384),
                        Pixel(999,32,219,387), Pixel(1409,33,282,384) };
                case "BasinV42States":
                    return new[] { Pixel(66,105,313,291), Pixel(530,114,269,282),
                        Pixel(959,105,300,282), Pixel(1419,115,269,281) };
                case "ShowerV42States":
                    return new[] { Pixel(143,14,158,415), Pixel(574,16,189,413),
                        Pixel(1031,14,156,415), Pixel(1453,16,190,413) };
                case "LoungeChairStates":
                    return new[] { Pixel(60,76,327,319), Pixel(532,54,266,346),
                        Pixel(947,58,324,342), Pixel(1419,54,267,346) };
                case "DiningChairV42States":
                    return new[] { Pixel(126,67,194,341), Pixel(538,35,208,373),
                        Pixel(1018,57,185,359), Pixel(1452,35,206,373) };
                case "WorkChairStates":
                    return new[] { Pixel(89,54,267,376), Pixel(552,17,236,413),
                        Pixel(976,32,267,398), Pixel(1428,17,239,413) };
                case "SafeV42States":
                    return new[] { Pixel(86,46,299,358), Pixel(545,46,260,358),
                        Pixel(947,46,324,359), Pixel(1411,46,262,358) };
                case "OpenCabinetStates":
                    return new[] { Pixel(80,54,311,361), Pixel(536,54,238,366),
                        Pixel(948,54,328,367), Pixel(1446,54,241,367) };
                case "SmallCabinetStates":
                    return new[] { Pixel(86,47,299,356), Pixel(545,47,225,356),
                        Pixel(948,47,321,360), Pixel(1445,47,225,356) };
                case "StandardShelfStates":
                    return new[] { Pixel(52,80,452,335), Pixel(625,34,186,384),
                        Pixel(940,80,452,335), Pixel(1515,34,186,384) };
                case "LargeShelfStates":
                    return new[] { Pixel(44,129,575,253), Pixel(699,53,138,345),
                        Pixel(931,129,575,253), Pixel(1587,53,137,345) };
                case "CargoContainerStates":
                    return new[] { Pixel(51,26,346,394), Pixel(483,37,359,383),
                        Pixel(935,35,348,385), Pixel(1375,37,363,383) };
                case "SpacerToiletStates":
                    return new[] { Pixel(98,34,249,383), Pixel(493,89,343,309),
                        Pixel(982,35,253,388), Pixel(1381,89,345,309) };
                case "BasinStates":
                    return new[] { Pixel(78,58,296,354), Pixel(518,58,296,354),
                        Pixel(959,44,295,368), Pixel(1400,58,295,354) };
                case "PowerShowerStates":
                    return new[] { Pixel(87,37,270,375), Pixel(503,62,311,349),
                        Pixel(967,12,283,402), Pixel(1402,62,312,349) };
                case "BathtubStates":
                    return new[] { Pixel(70,98,417,269), Pixel(589,22,224,406),
                        Pixel(939,131,408,236), Pixel(1460,22,226,406) };
                case "PokerTableStates":
                    return new[] { Pixel(54,27,339,375), Pixel(496,31,338,371),
                        Pixel(938,30,343,372), Pixel(1384,30,338,372) };
                case "BilliardsTableStates":
                    return new[] { Pixel(101,28,241,404), Pixel(486,123,358,262),
                        Pixel(988,28,241,404), Pixel(1374,123,357,262) };
                case "ChessTableStates":
                    return new[] { Pixel(82,44,299,356), Pixel(521,44,301,356),
                        Pixel(958,45,299,355), Pixel(1393,45,301,355) };
                case "LargeTelevisionStates":
                    return new[] { Pixel(69,119,439,276), Pixel(644,53,119,344),
                        Pixel(926,120,448,276), Pixel(1544,53,117,344) };
                default:
                    Log.Error("Unsupported Crimson storage/hygiene atlas: " + key);
                    return new[] { Pixel(0,0,443,443), Pixel(443,0,443,443),
                        Pixel(887,0,443,443), Pixel(1330,0,443,443) };
            }
        }

        private static bool IsFollowupAtlas(string key)
        {
            return key == "ToiletV44AngularStates" || key == "BasinV44States" || key == "BasinV119States"
                || key == "PokerTableV44States"
                || key == "ToiletV43States" || key == "BasinV43States"
                || key == "LoungeChairV43States" || key == "DiningChairV43States"
                || key == "WorkChairV43States"
                || key == "ToiletV42States" || key == "BasinV42States"
                || key == "ShowerV42States" || key == "LoungeChairStates"
                || key == "DiningChairV42States" || key == "WorkChairStates"
                || key == "SafeV42States" || key == "OpenCabinetStates";
        }

        private static Rect[] PreserveAspect(Rect[] windows, Vector2 worldSize)
        {
            float ratio = worldSize.x / worldSize.y;
            float width = 0f, height = 0f;
            for (int i = 0; i < windows.Length; i++)
            {
                width = Mathf.Max(width, windows[i].width);
                height = Mathf.Max(height, windows[i].height);
            }
            height = Mathf.Ceil(Mathf.Max(height, width / ratio));
            width = height * ratio;
            Rect[] result = new Rect[windows.Length];
            for (int i = 0; i < windows.Length; i++)
                result[i] = Pixel(windows[i].center.x - width / 2f,
                    windows[i].yMax - height, width, height);
            return result;
        }

        private static Rect Pixel(float left, float top, float width, float height)
        {
            // Kept in pixel coordinates until a power state selects its row.
            return new Rect(left, top, width, height);
        }

        private static Material Frame(Material original, Rect pixel, bool on, float row)
        {
            const float width = 1774f, height = 887f;
            float top = pixel.y + (on ? row : 0f);
            Rect uv = new Rect(pixel.x / width, (height - top - pixel.height) / height,
                pixel.width / width, pixel.height / height);
            Material result = new Material(original);
            Vector2 scale = original.mainTextureScale;
            result.mainTextureScale = Vector2.Scale(scale, new Vector2(uv.width, uv.height));
            result.mainTextureOffset = original.mainTextureOffset
                + Vector2.Scale(scale, new Vector2(uv.x, uv.y));
            return result;
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonStorageHygieneIcons
    {
        static CrimsonStorageHygieneIcons()
        {
            Register("Crimson_StorageCabinetSmall");
            Register("Crimson_OpenStorageCabinetSmall");
            Register("Crimson_StorageShelf");
            Register("Crimson_StorageShelfLarge");
            Register("Crimson_StorageCargoContainer");
            Register("Crimson_SpacerToilet");
            Register("Crimson_WashBasin");
            Register("Crimson_PowerShower");
            Register("Crimson_Bathtub");
            Register("Crimson_PokerTable");
            Register("Crimson_BilliardsTable");
            Register("Crimson_ChessTable");
            Register("Crimson_LargeTelevision");
            Register("Crimson_LoungeChair");
            Register("Crimson_DiningChair");
            Register("Crimson_WorkChair");
        }

        private static void Register(string defName)
        {
            // Optional Dubs definitions are absent when that mod is disabled.
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || def.graphicData == null) return;
            Material frame = def.graphicData.Graphic.MatAt(def.defaultPlacingRot, null);
            Texture2D source = ContentFinder<Texture2D>.Get(def.graphicData.texPath);
            if (defName == "Crimson_LargeTelevision" && frame.mainTexture != source)
            {
                // The wall TV uses an isolated native frame for both ghost
                // shaders and its icon, rather than blitting the full atlas.
                def.uiIcon = (Texture2D)frame.mainTexture;
                def.uiIconMaterial = null;
                return;
            }
            Vector2 scale = frame.mainTextureScale, offset = frame.mainTextureOffset;
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale.x));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale.y));
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0,
                RenderTextureFormat.ARGB32);
            try
            {
                // Native UI icons discard atlas UVs. Bake only the selected
                // frame in memory; source textures remain byte-for-byte intact.
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

