using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CompProperties_CrimsonFixedFurniture : CompProperties
    {
        public bool legendaryOnly;
        public float originalMaxHitPoints;
        public bool goldReference;
        public bool facilityComfortIsFinal;
        internal bool valuesBaked;
        internal List<StatModifier> hiddenStatOffsets;
        internal float facilityComfortFactor = 1f;

        public CompProperties_CrimsonFixedFurniture()
        {
            compClass = typeof(CrimsonFixedFurniture);
        }
    }

    // These explicit definitions have no quality. Legendary-equivalent values
    // are baked into their bases once when definitions finish loading.
    public sealed class CrimsonFixedFurniture : ThingComp
    {
        private static List<StatDef> cachedStats;
        private int standardSaveVersion;

        private CompProperties_CrimsonFixedFurniture Settings
        {
            get { return (CompProperties_CrimsonFixedFurniture)props; }
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            Normalize();
            if (parent.def.useHitPoints) parent.HitPoints = parent.MaxHitPoints;
            standardSaveVersion = 1;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref standardSaveVersion, "crimsonFurnitureStandardVersion", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                float condition = -1f;
                if (standardSaveVersion == 0 && !Settings.legendaryOnly
                    && Settings.originalMaxHitPoints > 0f && parent.def.useHitPoints)
                    condition = OriginalCondition();
                Normalize();
                if (condition >= 0f)
                    parent.HitPoints = UnityEngine.Mathf.Clamp(
                        UnityEngine.Mathf.RoundToInt(condition * parent.MaxHitPoints), 1, parent.MaxHitPoints);
                standardSaveVersion = 1;
            }
        }

        private float OriginalCondition()
        {
            float maximum = Settings.originalMaxHitPoints;
            if (parent.Stuff != null)
            {
                if (parent.Stuff.stuffProps.statFactors != null)
                    foreach (StatModifier modifier in parent.Stuff.stuffProps.statFactors)
                        if (modifier.stat == StatDefOf.MaxHitPoints) maximum *= modifier.value;
                if (parent.Stuff.stuffProps.statOffsets != null)
                    foreach (StatModifier modifier in parent.Stuff.stuffProps.statOffsets)
                        if (modifier.stat == StatDefOf.MaxHitPoints) maximum += modifier.value;
            }
            StatRequest request = StatRequest.For(parent);
            if (StatDefOf.MaxHitPoints.parts != null)
                foreach (StatPart_Quality part in StatDefOf.MaxHitPoints.parts.OfType<StatPart_Quality>())
                    part.TransformValue(request, ref maximum);
            StatDefOf.MaxHitPoints.Worker.FinalizeValue(request, ref maximum, true);
            return UnityEngine.Mathf.Clamp01(parent.HitPoints / UnityEngine.Mathf.Max(1f, maximum));
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Normalize();
            if (respawningAfterLoad)
                LongEventHandler.ExecuteWhenFinished(delegate { CrimsonFixedFurnitureRegistration.EnsureArt(parent); });
            else CrimsonFixedFurnitureRegistration.EnsureArt(parent);
        }

        public override void PostPostGeneratedForTrader(TraderKindDef trader, PlanetTile forTile, Faction forFaction)
        {
            base.PostPostGeneratedForTrader(trader, forTile, forFaction);
            CrimsonFixedFurnitureRegistration.EnsureArt(parent);
        }

        private void Normalize()
        {
            // Old saves can retain a Stuff reference after a definition stops
            // being stuffable. Remove it so material bonuses cannot stack twice.
            if (parent.Stuff != null) parent.SetStuffDirect(null);
            if (cachedStats == null)
            {
                cachedStats = DefDatabase<StatDef>.AllDefsListForReading.Where(stat =>
                    stat == StatDefOf.MaxHitPoints || stat == StatDefOf.Beauty
                    || stat == StatDefOf.MarketValue || stat == StatDefOf.Flammability
                    || stat == StatDefOf.Mass || stat == StatDefOf.WorkToBuild
                    || stat == StatDefOf.WorkToMake
                    || (stat.parts != null && stat.parts.Any(part => part is StatPart_Quality))
                    || HasMaterialModifier(ThingDefOf.Gold, stat)
                    || HasMaterialModifier(ThingDefOf.Plasteel, stat)).ToList();
            }
            foreach (StatDef stat in cachedStats) stat.Worker.ClearCacheForThing(parent);
        }

        public override float GetStatOffset(StatDef stat)
        {
            if (Settings.hiddenStatOffsets != null)
                foreach (StatModifier modifier in Settings.hiddenStatOffsets)
                    if (modifier.stat == stat) return modifier.value;
            return 0f;
        }

        private static bool HasMaterialModifier(ThingDef material, StatDef stat)
        {
            return material.stuffProps.statFactors != null
                    && material.stuffProps.statFactors.Any(modifier => modifier.stat == stat)
                || material.stuffProps.statOffsets != null
                    && material.stuffProps.statOffsets.Any(modifier => modifier.stat == stat);
        }

        public override string GetDescriptionPart()
        {
            return Settings.legendaryOnly
                ? "카펫 규격: 품질 등급 없음. 초경조직 제작, 트럼보가죽 기준 미관과 고정 성능."
                : "고정 규격: 품질 등급 없음. 성능과 가치는 고정되며 재료를 선택하지 않습니다. 불에 타지 않습니다.";
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonFixedFurnitureRegistration
    {
        private static readonly MethodInfo clone = typeof(object).GetMethod("MemberwiseClone",
            BindingFlags.Instance | BindingFlags.NonPublic);

        static CrimsonFixedFurnitureRegistration()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                if (def.GetCompProperties<CompProperties_CrimsonFixedFurniture>() != null)
                    Bake(def);
            Harmony harmony = new Harmony("rimod.crimson.furniture.fixed-standard");
            harmony.Patch(AccessTools.Method(typeof(CompAffectedByFacilities), "GetStatOffset"),
                postfix: new HarmonyMethod(typeof(CrimsonFixedFurnitureRegistration), "FacilityOffset"));
            harmony.Patch(AccessTools.Method(typeof(FocusStrengthOffset_BuildingDefsWithQuality), "OffsetForBuilding"),
                prefix: new HarmonyMethod(typeof(CrimsonFixedFurnitureRegistration), "NeighborFocus"));
            harmony.Patch(AccessTools.Method(typeof(ThingSetMakerUtility), "AssignQuality"),
                postfix: new HarmonyMethod(typeof(CrimsonFixedFurnitureRegistration), "GeneratedArt"));
            harmony.Patch(AccessTools.Method(typeof(CompArt), "GetDescriptionPart"),
                prefix: new HarmonyMethod(typeof(CrimsonFixedFurnitureRegistration), "InspectArt"));
            harmony.Patch(AccessTools.Method(typeof(CompArt), "CompInspectStringExtra"),
                prefix: new HarmonyMethod(typeof(CrimsonFixedFurnitureRegistration), "InspectArt"));
        }

        private static void SetBase(ThingDef def, StatDef stat, float value)
        {
            StatModifier modifier = def.statBases.FirstOrDefault(entry => entry.stat == stat);
            if (modifier == null) def.statBases.Add(new StatModifier { stat = stat, value = value });
            else modifier.value = value;
        }

        private static float ApplyReference(StatDef stat, StatRequest request, float value)
        {
            foreach (StatPart_Quality part in stat.parts.OfType<StatPart_Quality>())
                part.TransformValue(request, ref value);
            return value;
        }

        private static void Bake(ThingDef def)
        {
            CompProperties_CrimsonFixedFurniture original = def.GetCompProperties<CompProperties_CrimsonFixedFurniture>();
            if (original.valuesBaked) return;
            // Inheritance can share property and stat objects. Change only this
            // concrete opted-in definition, never a parent or another building.
            CompProperties_CrimsonFixedFurniture settings = (CompProperties_CrimsonFixedFurniture)clone.Invoke(original, null);
            settings.hiddenStatOffsets = new List<StatModifier>();
            def.comps = new List<CompProperties>(def.comps);
            def.comps[def.comps.IndexOf(original)] = settings;
            def.statBases = def.statBases == null ? new List<StatModifier>() : def.statBases.Select(
                entry => new StatModifier { stat = entry.stat, value = entry.value }).ToList();
            if (settings.goldReference) ApplyGoldReference(def);
            StatRequest normal = StatRequest.For(def, null, QualityCategory.Normal);
            StatRequest reference = StatRequest.For(def, null, QualityCategory.Legendary);
            // Collect before mutating any bases; MarketValueIgnoreHp delegates
            // to MarketValue and must not receive a second value adjustment.
            List<StatModifier> bases = new List<StatModifier>();
            foreach (StatDef stat in DefDatabase<StatDef>.AllDefsListForReading)
            {
                if (stat.defName == "MarketValueIgnoreHp" || stat.parts == null
                    || !stat.parts.Any(part => part is StatPart_Quality)) continue;
                float value = stat.Worker.GetValueUnfinalized(normal, true);
                float baked = ApplyReference(stat, reference, value);
                if (stat == StatDefOf.Comfort)
                    settings.facilityComfortFactor = ApplyReference(stat, reference, 1f);
                if (baked == value) continue;
                if (stat == StatDefOf.MarketValue || stat == StatDefOf.Beauty
                    || def.statBases.Any(entry => entry.stat == stat) || stat.Worker.ShouldShowFor(normal))
                    bases.Add(new StatModifier { stat = stat, value = baked });
                else
                    // Preserve native defaults used by callers without making
                    // irrelevant weapon/apparel stats appear in furniture UI.
                    settings.hiddenStatOffsets.Add(new StatModifier { stat = stat, value = baked - value });
            }
            foreach (StatModifier modifier in bases) SetBase(def, modifier.stat, modifier.value);
            for (int i = 0; i < def.comps.Count; i++)
            {
                CompProperties_StatOffsetBase focus = def.comps[i] as CompProperties_StatOffsetBase;
                if (focus != null && focus.offsets != null && focus.offsets.Any(offset => offset is FocusStrengthOffset_Quality))
                {
                    CompProperties_StatOffsetBase fixedFocus = (CompProperties_StatOffsetBase)clone.Invoke(focus, null);
                    float bonus = focus.offsets.OfType<FocusStrengthOffset_Quality>().Sum(
                        offset => UnityEngine.Mathf.Round(offset.curve.Evaluate(6f) * 100f) / 100f);
                    fixedFocus.offsets = focus.offsets.Where(offset => !(offset is FocusStrengthOffset_Quality)).ToList();
                    def.comps[i] = fixedFocus;
                    float value = def.statBases.Where(entry => entry.stat == focus.statDef)
                        .Select(entry => entry.value).DefaultIfEmpty(focus.statDef.defaultBaseValue).First();
                    SetBase(def, focus.statDef, value + bonus);
                }
                CompProperties_FacilityQualityBased facility = def.comps[i] as CompProperties_FacilityQualityBased;
                if (facility != null)
                {
                    CompProperties_Facility fixedFacility = new CompProperties_Facility();
                    foreach (FieldInfo field in typeof(CompProperties_Facility).GetFields(BindingFlags.Instance | BindingFlags.Public))
                        field.SetValue(fixedFacility, field.GetValue(facility));
                    fixedFacility.compClass = typeof(CompFacility);
                    fixedFacility.statOffsets = facility.statOffsetsPerQuality.Select(entry => new StatModifier {
                        stat = entry.Key, value = entry.Value[QualityCategory.Legendary] }).ToList();
                    def.comps[i] = fixedFacility;
                }
            }
            def.comps.RemoveAll(properties => typeof(CompQuality).IsAssignableFrom(properties.compClass));
            def.drawGUIOverlayQuality = false;
            settings.valuesBaked = true;
        }

        private static void ApplyGoldReference(ThingDef def)
        {
            // New fixed-material furniture can request the same native gold
            // comparison used for the established collection. This detached
            // definition changes neither its actual recipe nor any material Def.
            ThingDef reference = (ThingDef)clone.Invoke(def, null);
            reference.statBases = def.statBases.Where(entry => entry.stat != StatDefOf.MarketValue)
                .Select(entry => new StatModifier { stat = entry.stat, value = entry.value }).ToList();
            reference.stuffCategories = new List<StuffCategoryDef> { StuffCategoryDefOf.Metallic };
            reference.costStuffCount = 0;
            reference.defaultStuff = ThingDefOf.Gold;
            Dictionary<ThingDef, int> costs = new Dictionary<ThingDef, int>();
            foreach (ThingDefCountClass ingredient in def.costList)
            {
                bool material = ingredient.thingDef.stuffProps != null;
                ThingDef key = material ? ThingDefOf.Gold : ingredient.thingDef;
                int count = material ? UnityEngine.Mathf.RoundToInt(ingredient.count
                    * ingredient.thingDef.VolumePerUnit / ThingDefOf.Gold.VolumePerUnit) : ingredient.count;
                int previous; costs.TryGetValue(key, out previous); costs[key] = previous + count;
            }
            reference.costList = costs.Select(entry => new ThingDefCountClass(entry.Key, entry.Value)).ToList();
            StatRequest request = StatRequest.For(reference, ThingDefOf.Gold, QualityCategory.Normal);
            float beauty = StatDefOf.Beauty.Worker.GetValueUnfinalized(request, true);
            float value = StatDefOf.MarketValue.Worker.GetValueUnfinalized(request, true);
            SetBase(def, StatDefOf.Beauty, beauty);
            SetBase(def, StatDefOf.MarketValue, value);
        }

        private static void FacilityOffset(CompAffectedByFacilities __instance, StatDef stat, ref float __result)
        {
            if (stat != StatDefOf.Comfort || __instance.parent == null) return;
            CompProperties_CrimsonFixedFurniture settings = __instance.parent.def.GetCompProperties<CompProperties_CrimsonFixedFurniture>();
            // Opted-in beds receive the already-final comfort offsets shown by
            // their linked facilities; do not apply Legendary a second time.
            if (settings != null && settings.valuesBaked && !settings.facilityComfortIsFinal)
                __result *= settings.facilityComfortFactor;
        }

        private static bool NeighborFocus(FocusStrengthOffset_BuildingDefsWithQuality __instance, Thing __0, ref float __result)
        {
            if (__0 == null || __0.TryGetComp<CrimsonFixedFurniture>() == null) return true;
            __result = __instance.focusPerQuality.Evaluate(6f);
            return false;
        }

        internal static void EnsureArt(Thing thing)
        {
            if (thing == null) return;
            thing = thing.GetInnerIfMinified();
            if (thing.TryGetComp<CrimsonFixedFurniture>() == null) return;
            CompArt art = thing.TryGetComp<CompArt>();
            if (art != null && !art.Active) art.InitializeArt(ArtGenerationContext.Outsider);
        }

        private static void GeneratedArt(Thing __0) { EnsureArt(__0); }
        private static void InspectArt(CompArt __instance) { EnsureArt(__instance.parent); }
    }
}
