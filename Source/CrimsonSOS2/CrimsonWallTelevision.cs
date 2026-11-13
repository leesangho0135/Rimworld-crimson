using HarmonyLib;
using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    // Native attachments rotate toward their backing wall. Keep that placement
    // and destruction contract while this TV's audience sits on the room side.
    [StaticConstructorOnStartup]
    internal static class CrimsonWallTelevisionPatches
    {
        static CrimsonWallTelevisionPatches()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.wall-television");
            harmony.Patch(AccessTools.Method(typeof(WatchBuildingUtility), "CalculateAllowedDirections"),
                prefix: new HarmonyMethod(typeof(CrimsonWallTelevisionPatches), "RoomSideDirection"));
        }

        private static void RoomSideDirection(ThingDef __0, ref Rot4 __1)
        {
            if (__0 != null && __0.defName == "Crimson_LargeTelevision") __1 = __1.Opposite;
        }
    }
}
