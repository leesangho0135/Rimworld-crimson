using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonStallDoorApproach
    {
        private static readonly ThingDef Door = DefDatabase<ThingDef>.GetNamed("Crimson_ToiletStallDoor");
        private static readonly ThingDef PartitionDoor = DefDatabase<ThingDef>.GetNamed("Crimson_PartitionDoor");
        private static readonly MethodInfo Notify = AccessTools.Method(Door.thingClass, "Notify_PawnApproaching");

        static CrimsonStallDoorApproach()
        {
            // DBH's approach hook searches only its original ThingDef. Its
            // privacy tests already recognize the native stall-door class.
            // Notify only our added doors; leave the original hook untouched.
            new Harmony("rimod.crimson.sos2.dbh.stalldoor").Patch(
                AccessTools.Method(typeof(Pawn_PathFollower), "SetupMoveIntoNextCell"),
                postfix: new HarmonyMethod(typeof(CrimsonStallDoorApproach), "AfterApproach"));
        }

        private static void AfterApproach(Pawn ___pawn, IntVec3 ___nextCell)
        {
            if (___pawn == null || !___pawn.Spawned || !___nextCell.InBounds(___pawn.Map)) return;
            Building door = ___nextCell.GetEdifice(___pawn.Map);
            if (door != null && (door.def == Door || door.def == PartitionDoor)) Notify.Invoke(door, new object[] { ___pawn });
        }
    }
}
