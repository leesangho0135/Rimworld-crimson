using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    // A workbench's explicit recipes do not include recipes whose recipeUsers
    // name the original bench. Read its complete public list, without changing
    // either the original ThingDef or shared RecipeDefs.
    [StaticConstructorOnStartup]
    internal static class CrimsonWorkshopRecipes
    {
        private static readonly Dictionary<ThingDef, ThingDef> originals =
            new Dictionary<ThingDef, ThingDef>();

        static CrimsonWorkshopRecipes()
        {
            Register("Crimson_InductionCooker", "ElectricStove");
            Register("Crimson_MachiningBench", "TableMachining");
            Register("Crimson_FabricationBench", "FabricationBench");
            if (originals.Count == 0) return;
            Harmony harmony = new Harmony("rimod.crimson.sos2.workshop-recipes");
            harmony.Patch(AccessTools.PropertyGetter(typeof(ThingDef), "AllRecipes"),
                postfix: new HarmonyMethod(typeof(CrimsonWorkshopRecipes), "CompleteRecipes"));
        }

        private static void Register(string crimsonName, string nativeName)
        {
            ThingDef crimson = DefDatabase<ThingDef>.GetNamedSilentFail(crimsonName);
            ThingDef native = DefDatabase<ThingDef>.GetNamedSilentFail(nativeName);
            if (crimson != null && native != null) originals.Add(crimson, native);
        }

        private static void CompleteRecipes(ThingDef __instance, ref List<RecipeDef> __result)
        {
            ThingDef original;
            if (!originals.TryGetValue(__instance, out original)) return;
            List<RecipeDef> combined = new List<RecipeDef>(original.AllRecipes);
            if (__result != null)
                foreach (RecipeDef recipe in __result)
                    if (!combined.Contains(recipe)) combined.Add(recipe);
            __result = combined;
        }
    }

}
