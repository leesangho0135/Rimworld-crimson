using HarmonyLib;
using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonBedSleepPatches
    {
        static CrimsonBedSleepPatches()
        {
            new Harmony("rimod.crimson.sos2.bed-sleep").Patch(
                AccessTools.Method(typeof(Pawn), "CheckForDisturbedSleep", new[] { typeof(Pawn) }),
                prefix: new HarmonyMethod(typeof(CrimsonBedSleep), "AllowDisturbance"));
        }
    }

    internal static class CrimsonBedSleep
    {
        // This native handler only creates the SleepDisturbed memory. Damage,
        // forced jobs and other wake-up mechanisms remain under native control.
        internal static bool AllowDisturbance(Pawn __instance)
        {
            if (__instance == null) return true;
            Building_Bed bed = __instance.CurrentBed();
            return bed == null || !PreventsDisturbance(bed.def);
        }

        internal static bool PreventsDisturbance(ThingDef bed)
        {
            return bed != null && (bed.defName == "Crimson_CrewBed"
                || bed.defName == "Crimson_CrewDoubleBed");
        }
    }
}
