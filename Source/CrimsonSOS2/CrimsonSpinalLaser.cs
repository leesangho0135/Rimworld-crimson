using HarmonyLib;
using SaveOurShip2;
using Verse;

namespace CrimsonSOS2
{
    // The capacitor and barrel are contained in this single building. Retain
    // SOS2's fixed-facing spinal firing path, bridge, heat and power gates.
    public sealed class CrimsonSpinalLaser : Building_ShipTurret
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            ResetIntegratedMount();
        }

        internal void ResetIntegratedMount()
        {
            AmplifierCount = 0;
            AmplifierDamageBonus = 0f;
        }

        public override string GetInspectString()
        {
            return CrimsonWeaponStatus.Append(base.GetInspectString(), CrimsonWeaponStatus.Building(this));
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonSpinalLaserPatches
    {
        static CrimsonSpinalLaserPatches()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.integratedspinal");
            harmony.Patch(AccessTools.Method(typeof(Building_ShipTurret), "SpinalRecalc"),
                prefix: new HarmonyMethod(typeof(CrimsonSpinalLaserPatches), "IntegratedMountPrefix"));
        }

        private static bool IntegratedMountPrefix(Building_ShipTurret __instance)
        {
            CrimsonSpinalLaser integrated = __instance as CrimsonSpinalLaser;
            if (integrated == null) return true;
            // External amplifiers never alter this weapon, including after a
            // save/reload. Native multi-part spinal cannons use their old path.
            integrated.ResetIntegratedMount();
            return false;
        }
    }
}
