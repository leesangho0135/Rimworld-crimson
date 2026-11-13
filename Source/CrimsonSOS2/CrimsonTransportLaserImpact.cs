using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonTransportLaserImpact
    {
        static CrimsonTransportLaserImpact()
        {
            Type laser = AccessTools.TypeByName("SaveOurShip2.Projectile_ExplosiveShipLaser");
            if (laser == null) throw new InvalidOperationException("SOS2 laser projectile type is missing.");
            new Harmony("rimod.crimson.sos2.transport.laserimpact").Patch(
                AccessTools.Method(laser, "Impact"),
                prefix: new HarmonyMethod(typeof(CrimsonTransportLaserImpact), "ImpactPrefix"));
        }

        private static void ImpactPrefix(Projectile __instance, Thing hitThing,
            bool blockedByShield, ref Vector3 ___destination, ref int ___ticksToImpact)
        {
            // SOS2's same-map explosive impact can retain the previous tick's
            // cell when a fast laser crosses several cells at once. Only this
            // transport's ground beam is corrected; native damage and effects
            // still run, and all original/space projectiles are left alone.
            if (blockedByShield || hitThing == null || __instance.Map == null
                || hitThing.Map != __instance.Map || __instance.Launcher == null
                || __instance.Launcher.def.defName != "Crimson_PilgrimTransport"
                || __instance.def.defName != "Crimson_PilgrimLaserProjectile") return;

            __instance.Position = hitThing.Position;
            ___destination = hitThing.DrawPos;
            ___destination.y = 0f;
            ___ticksToImpact = 0;
        }
    }
}
