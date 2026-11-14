using System;
using HarmonyLib;
using Verse;

namespace CrimsonSOS2
{
    // Tell native save compatibility that these removals are intentional.
    // Do not remap old guide lights into an unrelated building or edit a save.
    [StaticConstructorOnStartup]
    internal static class CrimsonRetiredFurniture
    {
        static CrimsonRetiredFurniture()
        {
            new Harmony("rimod.crimson.retired-furniture").Patch(
                AccessTools.Method(typeof(BackCompatibility), "WasDefRemoved"),
                prefix: new HarmonyMethod(typeof(CrimsonRetiredFurniture), "WasRemoved"));
        }
        private static bool WasRemoved(string __0, Type __1, ref bool __result)
        {
            if (__1 != typeof(ThingDef) || (__0 != "Crimson_ThresholdGuide"
                && __0 != "Blueprint_Crimson_ThresholdGuide" && __0 != "Frame_Crimson_ThresholdGuide")) return true;
            __result = true;
            return false;
        }
    }
}
