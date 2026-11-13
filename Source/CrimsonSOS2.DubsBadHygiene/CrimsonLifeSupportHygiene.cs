using System.Collections.Generic;
using DubsBadHygiene;
using HarmonyLib;
using Verse;

namespace CrimsonSOS2
{
    // Reuse the native SOS2/DBH septic tank's network registration, capacity,
    // power gating and save fields; the integrated processor leaves no sludge.
    public sealed class CrimsonLifeSupportSewageProcessor : CompSepticTank
    {
        public override void CompTick()
        {
            if (WorkingNow)
            {
                sewageBuffer = 0f;
                Blocked = false;
            }
            else Blocked = sewageBuffer >= Props.capacity;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // No manual emptying job or sludge removal is needed.
            yield break;
        }

        public override string CompInspectStringExtra()
        {
            return "오수 수용: " + sewageBuffer.ToString("0.#") + " / "
                + Props.capacity.ToString("0") + "L\n"
                + (WorkingNow ? "내장 멸균 처리: 유입 오수 즉시 제거" : "오수 처리 정지");
        }
    }

    // Native filtration membership still supplies DBH's HasFilter behavior.
    // Sterilize before consumption as well as while ticking: native filtration
    // alone waits 5000 ticks and leaves a window for contaminated water use.
    public sealed class CrimsonLifeSupportSterilizer : CompWaterFiltration
    {
        public override void CompTick()
        {
            if (WorkingNow && PipeComp != null)
            {
                Sterilize(PipeComp.pipeNet);
                CrimsonLifeSupportHotWater.Maintain(PipeComp.pipeNet);
            }
        }

        internal static void Sterilize(PlumbingNet network)
        {
            if (network == null) return;
            foreach (CompWaterStorage tank in network.WaterTowers)
                tank.WaterQuality = ContaminationLevel.Treated;
        }

        internal static void SterilizeIfActive(PlumbingNet network)
        {
            foreach (CompWaterFiltration filter in network.Filters)
            {
                CrimsonLifeSupportSterilizer sterilizer = filter as CrimsonLifeSupportSterilizer;
                if (sterilizer != null && sterilizer.WorkingNow)
                {
                    Sterilize(network);
                    return;
                }
            }
        }
    }

    internal static class CrimsonLifeSupportHotWater
    {
        internal static bool Active(PlumbingNet network)
        {
            if (network == null || network.Filters == null) return false;
            foreach (CompWaterFiltration filter in network.Filters)
            {
                CrimsonLifeSupportSterilizer unit = filter as CrimsonLifeSupportSterilizer;
                if (unit != null && unit.parent != null
                    && unit.parent.def.defName == "Crimson_LifeSupport" && unit.WorkingNow)
                    return true;
            }
            return false;
        }

        internal static bool Maintains(CompHeatStore store)
        {
            return store != null && store.ForRunningWater && store.PipeComp != null
                && Active(store.PipeComp.pipeNet);
        }

        internal static void Maintain(PlumbingNet network)
        {
            if (!Active(network) || network.HotWaterTanks == null) return;
            foreach (var tank in network.HotWaterTanks)
                if (tank != null) tank.HeaterTemp = 1f;
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonLifeSupportHygienePatches
    {
        static CrimsonLifeSupportHygienePatches()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.dbh.lifesupport");
            HarmonyMethod sterilize = new HarmonyMethod(typeof(CrimsonLifeSupportHygienePatches), "BeforeWaterUse");
            harmony.Patch(AccessTools.Method(typeof(PlumbingNet), "PullWater"), prefix: sterilize);
            harmony.Patch(AccessTools.Method(typeof(PlumbingNet), "IsNetContaminated"), prefix: sterilize);
            HarmonyMethod hotWater = new HarmonyMethod(typeof(CrimsonLifeSupportHygienePatches), "BeforeHotWaterUse");
            harmony.Patch(AccessTools.Method(typeof(PlumbingNet), "HotWaterBingo"), prefix: hotWater);
            harmony.Patch(AccessTools.Method(typeof(PlumbingNet), "PullHotWater"), prefix: hotWater, postfix: hotWater);
            harmony.Patch(AccessTools.PropertyGetter(typeof(CompHeatStore), "HeaterTemp"),
                prefix: new HarmonyMethod(typeof(CrimsonLifeSupportHygienePatches), "BeforeHeatRead"));
            harmony.Patch(AccessTools.PropertySetter(typeof(CompHeatStore), "HeaterTemp"),
                prefix: new HarmonyMethod(typeof(CrimsonLifeSupportHygienePatches), "BeforeHeatWrite"));
        }

        private static void BeforeWaterUse(PlumbingNet __instance)
        {
            CrimsonLifeSupportSterilizer.SterilizeIfActive(__instance);
        }

        private static void BeforeHotWaterUse(PlumbingNet __instance)
        {
            CrimsonLifeSupportHotWater.Maintain(__instance);
        }

        private static void BeforeHeatRead(CompHeatStore __instance, ref float ___HeaterTempInt)
        {
            // Update actual native stored heat, including a cold save loaded
            // while paused. This is not an inspect-string percentage override.
            if (CrimsonLifeSupportHotWater.Maintains(__instance)) ___HeaterTempInt = 1f;
        }

        private static void BeforeHeatWrite(CompHeatStore __instance, ref float value)
        {
            // Native cooling and hot-water withdrawals use this same setter.
            // Keep room-heating stores and inactive/unconnected networks native.
            if (CrimsonLifeSupportHotWater.Maintains(__instance)) value = 1f;
        }
    }
}
