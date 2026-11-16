using HarmonyLib;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonWeaponPatches
    {
        internal const string ProjectileDefName = "Crimson_SingularityProjectile";

        static CrimsonWeaponPatches()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.torpedoes");
            harmony.Patch(
                AccessTools.Method(typeof(Building_ShipTurretTorpedo), "TorpedoTubePos"),
                prefix: new HarmonyMethod(typeof(CrimsonWeaponPatches), "TubePositionPrefix"));
            harmony.Patch(
                AccessTools.Method(typeof(DamageWorker), "ExplosionAffectCell"),
                postfix: new HarmonyMethod(typeof(CrimsonWeaponPatches), "ExplosionCellPostfix"));
        }

        private static bool TubePositionPrefix(Building_ShipTurretTorpedo __instance, ref Vector3 __result)
        {
            CrimsonTorpedoLauncher launcher = __instance as CrimsonTorpedoLauncher;
            if (launcher == null)
            {
                return true;
            }

            __result = launcher.NextTubeOffset();
            return false;
        }

        private static void ExplosionCellPostfix(Explosion __0, IntVec3 __1)
        {
            // All original projectiles leave immediately. SOS2's antigrain
            // Impact, damage worker, radii and additional explosions still run.
            if (__0.projectile == null || __0.projectile.defName != ProjectileDefName || __0.Map == null)
            {
                return;
            }

            ThingDef fireDef = DefDatabase<ThingDef>.GetNamed("Crimson_Echo");
            foreach (Thing existing in __1.GetThingList(__0.Map))
            {
                if (existing.def == fireDef)
                {
                    return;
                }
            }

            // Keep the original antigrain scatter/random damage sequence intact.
            // Chance and initial size match SOS2's mechanite explosion worker.
            Rand.PushState(__0.thingIDNumber ^ __1.GetHashCode());
            try
            {
                if (Rand.Chance(0.5f))
                {
                    Fire fire = (Fire)ThingMaker.MakeThing(fireDef);
                    fire.fireSize = 1.75f;
                    GenSpawn.Spawn(fire, __1, __0.Map, Rot4.North);
                }
            }
            finally
            {
                Rand.PopState();
            }
        }
    }
}
