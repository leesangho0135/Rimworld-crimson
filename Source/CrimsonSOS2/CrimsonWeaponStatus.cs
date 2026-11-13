using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SaveOurShip2;
using Vehicles;
using Verse;

namespace CrimsonSOS2
{
    // Inspect-only explanations for the three Crimson weapon owners. Native
    // firing gates, switches, targeting and timers remain authoritative.
    internal static class CrimsonWeaponStatus
    {
        private static readonly FieldInfo BurstTicks = AccessTools.Field(typeof(VehicleTurret), "burstTicks");
        private static readonly MethodInfo WarmingUpGetter = AccessTools.PropertyGetter(typeof(Building_ShipTurret), "WarmingUp");

        internal static string Append(string original, string status)
        {
            if (string.IsNullOrEmpty(status)) return original;
            if (!string.IsNullOrEmpty(original) && original.Contains(status)) return original;
            return string.IsNullOrEmpty(original) ? status : original.TrimEnd() + "\n" + status;
        }

        internal static string Building(Building_ShipTurret turret)
        {
            if (turret == null || !turret.Spawned) return null;
            // The inherited inspector already reports missing bridge/power and
            // empty torpedo storage. Do not repeat those native explanations.
            if (!turret.ConnectedToBridge || turret.heatComp == null || turret.heatComp.myNet == null) return null;
            CompFlickable flick = turret.TryGetComp<CompFlickable>();
            if (flick != null && !flick.SwitchIsOn) return null;
            if (turret.powerComp != null && !turret.powerComp.PowerOn) return null;
            if (turret.holdFire) return "대기: 사격 금지";
            if (turret.heatComp.myNet.venting) return null;
            if (turret.torpComp != null && !turret.torpComp.Loaded) return null;

            if (!turret.GroundDefenseMode)
            {
                if (turret.PointDefenseMode) return "요격 모드: 적함 포격 안 함";
                if (turret.mapComp == null || turret.mapComp.ShipMapState != ShipMapState.inCombat)
                    return "대기: 함선전 없음";
                ShipMapComp origin = turret.mapComp.OriginMapComp;
                float range = origin == null ? turret.mapComp.Range : origin.Range;
                if (!turret.InRangeSC(range))
                    return "대기: 함선 사거리 밖 (" + range.ToString("0.#", CultureInfo.InvariantCulture)
                        + " / " + turret.heatComp.Props.maxRange.ToString("0.#", CultureInfo.InvariantCulture) + ")";
            }

            if (turret.HeatToFire > 0f
                && turret.heatComp.myNet.StorageCapacity - turret.heatComp.myNet.StorageUsed < turret.HeatToFire)
                return "대기: 열 저장 여유 부족";
            if (turret.EnergyToFire > 0f && turret.powerComp != null && turret.powerComp.PowerNet != null
                && turret.powerComp.PowerNet.CurrentStoredEnergy() < turret.EnergyToFire)
                return "대기: 저장 전력 부족";
            // Native GetInspectString already displays the burst cooldown.
            if (turret.burstCooldownTicksLeft > 0 || IsWarmingUp(turret)
                || (turret.AttackVerb != null && turret.AttackVerb.Bursting)) return null;
            if (!turret.CurrentTarget.IsValid) return "대기: 공격 표적 없음";
            return null;
        }

        private static bool IsWarmingUp(Building_ShipTurret turret)
        {
            // SOS2 keeps this getter private; use its native read-only result.
            return WarmingUpGetter != null && (bool)WarmingUpGetter.Invoke(turret, null);
        }

        internal static string Pilgrim(CrimsonPilgrimVehicle vehicle)
        {
            if (vehicle == null || !vehicle.Spawned || vehicle.CompVehicleTurrets == null) return null;
            if (vehicle.CompFueledTravel == null || vehicle.CompFueledTravel.Fuel < 1f) return "대기: 무장용 연료 부족";
            bool found = false;
            bool hasTarget = false;
            bool anyAutoTarget = false;
            bool anyReady = false;
            int minimumCooldown = int.MaxValue;
            foreach (VehicleTurret turret in vehicle.CompVehicleTurrets.Turrets)
            {
                if (!(turret is CrimsonPilgrimBit)) continue;
                found = true;
                hasTarget |= turret.targetInfo.IsValid;
                anyAutoTarget |= turret.AutoTarget;
                anyReady |= turret.ReadyToFire;
                int burst = BurstTicks == null ? 0 : (int)BurstTicks.GetValue(turret);
                int remaining = Math.Max(burst, turret.ReloadTicks);
                minimumCooldown = Math.Min(minimumCooldown, remaining);
            }
            if (!found) return null;
            if (!anyReady && minimumCooldown > 0 && minimumCooldown != int.MaxValue)
                return "대기: 다음 연사까지 " + (minimumCooldown / 60f).ToString("0.#", CultureInfo.InvariantCulture) + "초";
            if (!hasTarget)
                return anyAutoTarget ? "대기: 공격 표적 없음" : "대기: 표적 지정 필요 (자동 조준 꺼짐)";
            return null;
        }
    }
}
