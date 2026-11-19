using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonSculptureVariants : DefModExtension
    {
        public List<string> variants = new List<string>();
        public List<string> variantLabels = new List<string>();
        public List<Vector2> drawSizes = new List<Vector2>();
    }

    public sealed class CrimsonSculpture : Building_Art
    {
        private const string TextureRoot = "Crimson/Decor/Sculptures/";
        private string variantId;
        private bool mirrored;
        private Graphic cachedGraphic;
        private string cachedVariantId;
        private bool cachedMirrored;

        public string VariantId { get { return variantId; } }
        public bool Mirrored { get { return mirrored; } }

        private CrimsonSculptureVariants Variants
        {
            get { return def.GetModExtension<CrimsonSculptureVariants>(); }
        }

        public override Graphic Graphic
        {
            get
            {
                CrimsonSculptureVariants variants = Variants;
                if (variants == null || variants.variants.Count == 0) return base.Graphic;
                int index = VariantIndex(variants);
                string selectedId = variants.variants[index];
                if (cachedGraphic == null || cachedVariantId != selectedId
                    || cachedMirrored != mirrored)
                {
                    Vector2 size = variants.drawSizes.Count == variants.variants.Count
                        ? variants.drawSizes[index] : def.graphicData.drawSize;
                    cachedGraphic = mirrored
                        ? GraphicDatabase.Get<CrimsonMirroredSculptureGraphic>(
                            TextureRoot + selectedId, ShaderDatabase.Cutout, size,
                            Color.white, Color.white, def.graphicData, null)
                        : GraphicDatabase.Get<CrimsonSculptureGraphic>(
                            TextureRoot + selectedId, ShaderDatabase.Cutout, size,
                            Color.white, Color.white, def.graphicData, null);
                    cachedVariantId = selectedId;
                    cachedMirrored = mirrored;
                }
                return cachedGraphic;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            // Resolve before native art setup can request the first graphic.
            // A minified/reinstalled building retains its nonempty saved ID.
            EnsureVariant(respawningAfterLoad);
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref variantId, "crimsonSculptureVariantId", null);
            Scribe_Values.Look(ref mirrored, "crimsonSculptureMirrored", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // A missing/retired ID is repaired deterministically, never
                // with a new random draw while loading a saved sculpture.
                EnsureVariant(true);
                cachedGraphic = null;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            CrimsonSculptureVariants variants = Variants;
            if (!Spawned || Faction != Faction.OfPlayer || variants == null
                || variants.variants.Count == 0) yield break;
            yield return new Command_Action
            {
                defaultLabel = mirrored ? "좌우반전 해제" : "좌우반전",
                defaultDesc = "현재 조형의 좌우 방향만 바꿉니다. 선택된 외형과 예술품의 품질은 그대로 유지됩니다.",
                icon = ContentFinder<Texture2D>.Get(TextureRoot
                    + variants.variants[VariantIndex(variants)]),
                groupable = false,
                action = ToggleMirror
            };
        }

        public override string GetInspectString()
        {
            string result = base.GetInspectString();
            CrimsonSculptureVariants variants = Variants;
            if (variants == null || variants.variants.Count == 0) return result;
            int index = VariantIndex(variants);
            string label = variants.variantLabels.Count == variants.variants.Count
                ? variants.variantLabels[index] : variants.variants[index];
            return (string.IsNullOrEmpty(result) ? "" : result + "\n")
                + "조형: " + label + (mirrored ? " (좌우반전)" : "");
        }

        private void EnsureVariant(bool loading)
        {
            CrimsonSculptureVariants variants = Variants;
            if (variants == null || variants.variants.Count == 0) return;
            if (!string.IsNullOrEmpty(variantId)
                && variants.variants.Contains(variantId)) return;
            variantId = variants.variants[loading ? 0 : Rand.Range(0, variants.variants.Count)];
            cachedGraphic = null;
        }

        private int VariantIndex(CrimsonSculptureVariants variants)
        {
            int index = string.IsNullOrEmpty(variantId) ? -1
                : variants.variants.IndexOf(variantId);
            // Icons, ghosts and unspawned construction previews use the first
            // approved candidate without changing state or consuming Rand.
            return index >= 0 ? index : 0;
        }

        private void ToggleMirror()
        {
            mirrored = !mirrored;
            cachedGraphic = null;
        }
    }

    // Freestanding sculptures stay upright; only the wall relief opts in to
    // rotating its shallow artwork with the supporting wall.
    public class CrimsonSculptureGraphic : Graphic_Single
    {
        public override bool ShouldDrawRotated { get { return data != null && data.drawRotated; } }

        public override Mesh MeshAt(Rot4 rot)
        {
            return MeshPool.GridPlane(drawSize);
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef,
            Thing thing, float extraRotation)
        {
            if (thingDef != null && thingDef.defName != "Crimson_WallRelief")
                loc.z += Mathf.Max(0f, (drawSize.y - thingDef.size.z) / 2f);
            CrimsonFurnitureDepth.Draw(this, loc, ShouldDrawRotated ? rot : Rot4.North,
                thingDef, thing, ShouldDrawRotated ? extraRotation : 0f);
        }

        public override Graphic GetColoredVersion(Shader shader, Color color,
            Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonSculptureGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }
    }

    public sealed class CrimsonMirroredSculptureGraphic : CrimsonSculptureGraphic
    {
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            // GraphicDatabase shares graphics/materials by request. Clone the
            // normal material before changing UVs so other buildings stay intact.
            Material flipped = new Material(mat);
            Vector2 scale = flipped.mainTextureScale;
            Vector2 offset = flipped.mainTextureOffset;
            offset.x += scale.x;
            scale.x = -scale.x;
            flipped.mainTextureScale = scale;
            flipped.mainTextureOffset = offset;
            mat = flipped;
        }

        public override Graphic GetColoredVersion(Shader shader, Color color,
            Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonMirroredSculptureGraphic>(path,
                shader, drawSize, color, colorTwo, data, null);
        }
    }
}
