using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Explicit Crimson equipment only. Atlas crops are cached, never ticked.
    [StaticConstructorOnStartup]
    public static class CrimsonWardenArt
    {
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Graphic> worn = new Dictionary<string, Graphic>();

        static CrimsonWardenArt()
        {
            foreach (string item in new[] { "Helmet", "Uniform", "Cloak", "Sword", "Pistol" })
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Crimson_Warden" + item);
                if (def != null) def.uiIcon = Icon(item);
            }
            foreach (string item in new[] { "StaffHelmet", "StaffUniform", "OfficerCoat", "SoldierHelmet", "SoldierUniform", "AssistDrones" })
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Crimson_" + item);
                if (def != null) def.uiIcon = Icon(item);
            }
            new Harmony("rimod.crimson.warden.apparel").CreateClassProcessor(typeof(CrimsonWardenApparelGraphic)).Patch();
        }

        public static Texture2D Icon(string item)
        {
            string key = item + ":icon";
            Texture2D result;
            if (textures.TryGetValue(key, out result)) return result;
            Rect source = CrimsonWardenFrames.Source(item, 0, 0);
            float ratio = source.width / source.height;
            int width = ratio > 1f ? 238 : Mathf.RoundToInt(238f * ratio);
            int height = ratio > 1f ? Mathf.RoundToInt(238f / ratio) : 238;
            result = Bake(item, source, new Rect((256 - width) / 2, (256 - height) / 2, width, height), false);
            textures.Add(key, result);
            return result;
        }

        public static Texture2D Worn(string item, int body, int row, bool mirror)
        {
            string key = item + ":" + body + ":" + row + ":" + mirror;
            Texture2D result;
            if (textures.TryGetValue(key, out result)) return result;
            Rect source = CrimsonWardenFrames.Source(item, body, row);
            Rect target = CrimsonWardenFrames.Target(item, body, row);
            if (mirror) target.x = 256f - target.xMax;
            result = Bake(item, source, target, mirror);
            if (item == "Uniform" || item == "StaffUniform" || item == "SoldierUniform") result = SealUniform(result, body, row, mirror);
            textures.Add(key, result);
            return result;
        }

        public static Graphic Apparel(string item, BodyTypeDef bodyType, Color color)
        {
            int body = Array.IndexOf(CrimsonWardenFrames.Bodies, bodyType == null ? "Male" : bodyType.defName);
            if (body < 0) body = 0;
            if (CrimsonRankEquipmentArt.IsHead(item)) body = 0;
            string path = "Crimson/Equipment/" + item + "/" + body;
            string key = path + ":" + color;
            Graphic result;
            if (!worn.TryGetValue(key, out result))
            {
                result = GraphicDatabase.Get<CrimsonWardenWornGraphic>(path, ShaderDatabase.Cutout,
                    Vector2.one, color);
                worn.Add(key, result);
            }
            return result;
        }

        private static Texture2D Bake(string item, Rect source, Rect target, bool mirror)
        {
            Texture2D atlas = ContentFinder<Texture2D>.Get(CrimsonRankEquipmentArt.TexturePath(item));
            Vector2 scale = new Vector2(source.width / atlas.width, source.height / atlas.height);
            Vector2 offset = new Vector2(source.x / atlas.width, 1f - source.yMax / atlas.height);
            if (mirror) { offset.x += scale.x; scale.x = -scale.x; }
            int width = Mathf.RoundToInt(target.width), height = Mathf.RoundToInt(target.height);
            RenderTexture prior = RenderTexture.active;
            RenderTexture inner = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture canvas = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(atlas, inner, scale, offset);
                RenderTexture.active = canvas;
                GL.Clear(true, true, Color.clear);
                Graphics.CopyTexture(inner, 0, 0, 0, 0, width, height, canvas, 0, 0,
                    Mathf.RoundToInt(target.x), 256 - Mathf.RoundToInt(target.yMax));
                Texture2D result = new Texture2D(256, 256, TextureFormat.ARGB32, false);
                result.name = "CrimsonWarden_" + item;
                result.wrapMode = TextureWrapMode.Clamp;
                result.filterMode = FilterMode.Bilinear;
                result.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                result.Apply(false, true);
                return result;
            }
            finally
            {
                RenderTexture.active = prior;
                RenderTexture.ReleaseTemporary(inner);
                RenderTexture.ReleaseTemporary(canvas);
            }
        }

        // The black pressure liner follows the game's own body silhouette. Thin
        // plate silhouettes therefore never expose skin at a large body's flank.
        private static Texture2D SealUniform(Texture2D plates, int body, int row, bool mirror)
        {
            BodyTypeDef type = DefDatabase<BodyTypeDef>.GetNamed(CrimsonWardenFrames.Bodies[body]);
            string direction = row == 0 ? "south" : row == 1 ? "east" : "north";
            Texture2D silhouette = ContentFinder<Texture2D>.Get(type.bodyNakedGraphicPath + "_" + direction);
            RenderTexture prior = RenderTexture.active;
            RenderTexture canvas = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32);
            try
            {
                RenderTexture.active = canvas;
                GL.Clear(true, true, Color.clear);
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0, 256, 256, 0);
                    Graphics.DrawTexture(new Rect(0, 0, 256, 256), silhouette,
                        new Rect(mirror ? 1 : 0, 0, mirror ? -1 : 1, 1), 0, 0, 0, 0,
                        new Color(.065f, .07f, .08f, 1f));
                    Graphics.DrawTexture(new Rect(0, 0, 256, 256), plates);
                }
                finally { GL.PopMatrix(); }
                Texture2D result = new Texture2D(256, 256, TextureFormat.ARGB32, false);
                result.name = plates.name + "_Sealed"; result.wrapMode = TextureWrapMode.Clamp;
                result.filterMode = FilterMode.Bilinear;
                result.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); result.Apply(false, true);
                UnityEngine.Object.Destroy(plates);
                return result;
            }
            finally { RenderTexture.active = prior; RenderTexture.ReleaseTemporary(canvas); }
        }
    }

    public sealed class CrimsonWardenGroundGraphic : Graphic_Single
    {
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            mat = MaterialPool.MatFrom(CrimsonWardenArt.Icon(request.path.Substring(request.path.LastIndexOf('/') + 1)), request.shader, request.color);
        }
    }

    public sealed class CrimsonWardenWornGraphic : Graphic_Multi
    {
        private readonly Material[] directions = new Material[4];
        public override Material MatSingle { get { return MatSouth; } }
        public override Material MatNorth { get { return directions[0]; } }
        public override Material MatEast { get { return directions[1]; } }
        public override Material MatSouth { get { return directions[2]; } }
        public override Material MatWest { get { return directions[3]; } }
        public override bool WestFlipped { get { return true; } }
        public override bool EastFlipped { get { return false; } }
        public override bool ShouldDrawRotated { get { return false; } }
        public override void TryInsertIntoAtlas(TextureAtlasGroup group) { }

        public override void Init(GraphicRequest request)
        {
            path = request.path; data = request.graphicData; drawSize = request.drawSize;
            color = request.color; colorTwo = request.colorTwo;
            string[] parts = path.Split('/');
            string item = parts[parts.Length - 2];
            int body = int.Parse(parts[parts.Length - 1]);
            for (int dir = 0; dir < 4; dir++)
            {
                int row = dir == 0 ? 2 : dir == 2 ? 0 : 1;
                // Humanlike pawn mesh sets already mirror their west UVs. Share
                // the east material, as native Graphic_Multi apparel does.
                directions[dir] = MaterialPool.MatFrom(CrimsonWardenArt.Worn(item, body, row, false), request.shader, color);
            }
        }
        public override Material MatAt(Rot4 rot, Thing thing = null) { return directions[rot.AsInt]; }
        public override Graphic GetColoredVersion(Shader shader, Color newColor, Color newColorTwo)
        { return GraphicDatabase.Get<CrimsonWardenWornGraphic>(path, shader, drawSize, newColor, newColorTwo, data); }
    }

    [HarmonyPatch(typeof(ApparelGraphicRecordGetter), "TryGetGraphicApparel")]
    internal static class CrimsonWardenApparelGraphic
    {
        private static bool Prefix(Apparel apparel, BodyTypeDef bodyType, ref ApparelGraphicRecord rec, ref bool __result)
        {
            string name = apparel.def.defName;
            string item = name == "Crimson_WardenHelmet" ? "Helmet" : name == "Crimson_WardenUniform" ? "Uniform"
                : name == "Crimson_WardenCloak" ? "Cloak"
                : name == "Crimson_StaffHelmet" ? "StaffHelmet"
                : name == "Crimson_StaffUniform" ? "StaffUniform"
                : name == "Crimson_OfficerCoat" ? "OfficerCoat"
                : name == "Crimson_SoldierHelmet" ? "SoldierHelmet"
                : name == "Crimson_SoldierUniform" ? "SoldierUniform"
                : name == "Crimson_AssistDrones" ? "AssistDrones" : null;
            if (item == null) return true;
            rec = new ApparelGraphicRecord(CrimsonWardenArt.Apparel(item, bodyType, apparel.DrawColor), apparel);
            __result = true;
            return false;
        }
    }
}
