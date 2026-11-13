using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonBathPawnDraw
    {
        static CrimsonBathPawnDraw()
        {
            new Harmony("rimod.crimson.sos2.dbh.bathdraw").Patch(
                AccessTools.PropertyGetter(typeof(Pawn), "DrawPos"),
                postfix: new HarmonyMethod(typeof(CrimsonBathPawnDraw), "AfterPawnDrawPos"));
        }

        private static void AfterPawnDrawPos(Pawn __instance, ref Vector3 __result)
        {
            if (!__instance.Spawned) return;
            Job job = __instance.CurJob;
            CrimsonBath bath = job == null ? null : job.targetA.Thing as CrimsonBath;
            if (bath == null || !bath.Spawned || bath.Map != __instance.Map
                || bath.occupant != __instance || bath.WaterInTub <= 0f) return;

            // DBH 1.6's RenderPawnWithY is a no-op. Keep the real pawn's
            // position/pathing and altitude; lean only its occupied-bath art
            // towards the head end, leaving the shoulders above the foam.
            Vector3 center = bath.BathingPawnDrawPosition(__instance);
            __result.x = center.x;
            __result.z = center.z;
        }
    }
}
