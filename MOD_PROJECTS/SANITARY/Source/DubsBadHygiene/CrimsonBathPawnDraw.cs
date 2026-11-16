using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonBathPawnDraw
    {
        private static readonly System.Reflection.FieldInfo RendererPawn = AccessTools.Field(typeof(PawnRenderer), "pawn");
        static CrimsonBathPawnDraw()
        {
            new Harmony("rimod.crimson.sos2.dbh.bathdraw").Patch(
                AccessTools.PropertyGetter(typeof(Pawn), "DrawPos"),
                postfix: new HarmonyMethod(typeof(CrimsonBathPawnDraw), "AfterPawnDrawPos"));
            HarmonyMethod angle = new HarmonyMethod(typeof(CrimsonBathPawnDraw), "AfterBodyAngle");
            angle.priority = Priority.Last;
            new Harmony("rimod.crimson.sos2.dbh.bath-angle").Patch(
                AccessTools.Method(typeof(PawnRenderer), "BodyAngle"), postfix: angle);
        }

        private static void AfterBodyAngle(PawnRenderer __instance, ref float __result)
        {
            Pawn pawn = (Pawn)RendererPawn.GetValue(__instance);
            CrimsonBath bath = pawn.CurJob == null ? null : pawn.CurJob.targetA.Thing as CrimsonBath;
            if (bath != null && bath.occupant == pawn && bath.WaterInTub > 0f)
                __result = (__result + 90f) % 360f;
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
