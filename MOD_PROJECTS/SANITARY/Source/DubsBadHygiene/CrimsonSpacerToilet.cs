using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DubsBadHygiene;
using HarmonyLib;
using Verse;
using Verse.Sound;

namespace CrimsonSOS2
{
    // Keep the native building class and save type. Only this fixture has an
    // integrated waste outlet; other spacer toilets retain DBH's dump checks.
    [StaticConstructorOnStartup]
    internal static class CrimsonSpacerToilet
    {
        static CrimsonSpacerToilet()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.dbh.spacertoilet");
            harmony.Patch(AccessTools.Method(typeof(Building_SpacerToilet), "Working"),
                transpiler: new HarmonyMethod(typeof(CrimsonSpacerToilet), "IntegratedOutlet"));
            harmony.Patch(AccessTools.Method(typeof(Building_SpacerToilet), "TryUseFlush"),
                prefix: new HarmonyMethod(typeof(CrimsonSpacerToilet), "Flush"));
        }

        private static bool IsCrimson(Building_SpacerToilet toilet)
        { return toilet.def.defName == "Crimson_SpacerToilet"; }

        private static bool HasOutlet(IEnumerable<SewageVacuumDump> outlets, Building_SpacerToilet toilet)
        { return IsCrimson(toilet) || outlets.Any(); }

        private static IEnumerable<CodeInstruction> IntegratedOutlet(IEnumerable<CodeInstruction> instructions)
        {
            int matches = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo method = instruction.operand as MethodInfo;
                if (method != null && method.DeclaringType == typeof(GenCollection)
                    && method.Name == "Any" && method.IsGenericMethod
                    && method.GetGenericArguments()[0] == typeof(SewageVacuumDump))
                {
                    CodeInstruction instance = new CodeInstruction(OpCodes.Ldarg_0);
                    instance.labels.AddRange(instruction.labels);
                    instance.blocks.AddRange(instruction.blocks);
                    yield return instance;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CrimsonSpacerToilet), "HasOutlet"));
                    matches++;
                }
                else yield return instruction;
            }
            if (matches != 1) throw new InvalidOperationException("Crimson spacer toilet: expected one native vacuum outlet check.");
        }

        private static bool Flush(Building_SpacerToilet __instance, ref bool __result)
        {
            if (!IsCrimson(__instance)) return true;
            // Retain native power, atmosphere and plumbing usability checks.
            __result = __instance.Working(0f).Accepted;
            if (__result) DubDef.vacuumtoilet.PlayOneShot(__instance);
            return false;
        }
    }
}
