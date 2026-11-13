using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    // Register only the new facilities. Native definitions, research projects,
    // recipes and other benches retain their own properties and checks.
    [StaticConstructorOnStartup]
    internal static class CrimsonResearchFurniture
    {
        private static readonly ThingDef Bench = DefDatabase<ThingDef>.GetNamed("Crimson_ResearchBench");
        private static readonly ThingDef Uplink = DefDatabase<ThingDef>.GetNamed("Crimson_WallResearchUplink");
        private static readonly ThingDef Tools = DefDatabase<ThingDef>.GetNamed("Crimson_ToolCabinet");
        private static readonly ThingDef NativeBench = DefDatabase<ThingDef>.GetNamed("HiTechResearchBench");
        private static readonly ThingDef NativeUplink = DefDatabase<ThingDef>.GetNamed("ArchotechUplink");
        private static readonly ThingDef Analyzer = DefDatabase<ThingDef>.GetNamed("MultiAnalyzer");
        private static readonly ThingDef NativeTools = DefDatabase<ThingDef>.GetNamed("ToolCabinet");
        private static readonly Dictionary<ThingDef, ThingDef> facilityViews = new Dictionary<ThingDef, ThingDef>();
        private static readonly MethodInfo clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        static CrimsonResearchFurniture()
        {
            CopyFacilityRules(Tools, NativeTools);
            CopyFacilityRules(Uplink, NativeUplink);
            CompProperties_Facility uplink = Uplink.GetCompProperties<CompProperties_Facility>();
            foreach (ThingDef def in Analyzer.GetCompProperties<CompProperties_Facility>().linkableBuildings)
                if (!uplink.linkableBuildings.Contains(def)) uplink.linkableBuildings.Add(def);
            if (!uplink.linkableBuildings.Contains(Bench)) uplink.linkableBuildings.Add(Bench);

            Harmony harmony = new Harmony("rimod.crimson.sos2.research-furniture");
            harmony.Patch(AccessTools.Method(typeof(ResearchProjectDef), "CanBeResearchedAt"),
                transpiler: new HarmonyMethod(typeof(CrimsonResearchFurniture), "BenchComparison"));
            MethodInfo predicate = typeof(ResearchProjectDef).GetNestedTypes(BindingFlags.NonPublic)
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                .Single(method => method.Name == "<CanBeResearchedAt>b__0");
            harmony.Patch(predicate, transpiler: new HarmonyMethod(typeof(CrimsonResearchFurniture), "FacilityComparison"));
            harmony.Patch(AccessTools.Method(typeof(CompAffectedByFacilities), "PotentialThingsToLinkTo"),
                prefix: new HarmonyMethod(typeof(CrimsonResearchFurniture), "ExtendFacilityDefinitions"));
        }

        private static void CopyFacilityRules(ThingDef target, ThingDef reference)
        {
            CompProperties_Facility own = target.GetCompProperties<CompProperties_Facility>();
            CompProperties_Facility source = reference.GetCompProperties<CompProperties_Facility>();
            foreach (FieldInfo field in typeof(CompProperties_Facility).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.Name == "compClass" || field.Name == "statOffsets" || field.Name == "maxSimultaneous"
                    || field.Name == "linkableBuildings") continue;
                field.SetValue(own, field.GetValue(source));
            }
            own.linkableBuildings = new List<ThingDef>(source.linkableBuildings);
        }

        private static bool BenchMatches(ThingDef actual, ThingDef required)
        { return actual == required || (actual == Bench && required == NativeBench); }

        private static bool FacilityMatches(ThingDef actual, ThingDef required)
        { return actual == required || (actual == Uplink && (required == NativeUplink || required == Analyzer)); }

        // Replace just native identity comparisons. Power, activity, project
        // prerequisites and every other native/mod check continue to run.
        private static IEnumerable<CodeInstruction> BenchComparison(IEnumerable<CodeInstruction> instructions)
        { return ReplaceComparison(instructions, true); }
        private static IEnumerable<CodeInstruction> FacilityComparison(IEnumerable<CodeInstruction> instructions)
        { return ReplaceComparison(instructions, false); }
        private static IEnumerable<CodeInstruction> ReplaceComparison(IEnumerable<CodeInstruction> instructions, bool bench)
        {
            List<CodeInstruction> code = instructions.ToList();
            int replaced = 0;
            for (int i = 0; i < code.Count; i++)
            {
                CodeInstruction current = code[i];
                FieldInfo field = i == 0 ? null : code[i - 1].operand as FieldInfo;
                bool match = bench
                    ? (field != null && field.Name == "requiredResearchBuilding"
                        && (current.opcode == OpCodes.Beq || current.opcode == OpCodes.Beq_S))
                    : current.opcode == OpCodes.Bne_Un || current.opcode == OpCodes.Bne_Un_S;
                if (!match) { yield return current; continue; }
                CodeInstruction call = new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(CrimsonResearchFurniture), bench ? "BenchMatches" : "FacilityMatches"));
                call.labels.AddRange(current.labels); call.blocks.AddRange(current.blocks);
                yield return call;
                yield return new CodeInstruction(bench ? OpCodes.Brtrue : OpCodes.Brfalse, current.operand);
                replaced++;
            }
            if (replaced != 1) throw new InvalidOperationException("Crimson research identity comparison changed: " + replaced);
        }

        // Let the native candidate pipeline apply distance, LOS, nearest-first
        // ordering and maxSimultaneous. A detached definition view extends just
        // that query; no native definition or live building is changed.
        private static void ExtendFacilityDefinitions(ref ThingDef myDef)
        {
            if (myDef == null) return;
            ThingDef cached;
            if (facilityViews.TryGetValue(myDef, out cached)) { myDef = cached; return; }
            CompProperties_AffectedByFacilities original = myDef.GetCompProperties<CompProperties_AffectedByFacilities>();
            if (original == null || original.linkableFacilities == null) return;
            List<ThingDef> facilities = new List<ThingDef>(original.linkableFacilities);
            if (facilities.Contains(NativeTools) && !facilities.Contains(Tools)) facilities.Add(Tools);
            if ((facilities.Contains(NativeUplink) || facilities.Contains(Analyzer)) && !facilities.Contains(Uplink)) facilities.Add(Uplink);
            if (facilities.Count == original.linkableFacilities.Count) return;
            ThingDef view = (ThingDef)clone.Invoke(myDef, null);
            CompProperties_AffectedByFacilities extended = (CompProperties_AffectedByFacilities)clone.Invoke(original, null);
            extended.linkableFacilities = facilities;
            view.comps = new List<CompProperties>(myDef.comps);
            view.comps[view.comps.IndexOf(original)] = extended;
            facilityViews.Add(myDef, view);
            myDef = view;
        }
    }
}
