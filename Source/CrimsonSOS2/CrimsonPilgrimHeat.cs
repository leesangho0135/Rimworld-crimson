using System;
using HarmonyLib;
using SaveOurShip2;
using SaveOurShip2.Vehicles;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CompProperties_PilgrimHeatNet : CompProperties
    {
        public CompProperties_PilgrimHeatNet()
        {
            compClass = typeof(CompPilgrimHeatNet);
        }
    }

    // Retains the native vehicle-heat-net type for SOS2's GetComp calls.
    // Heat/depletion totals are authoritative; native sink backing fields are
    // only the transport format consumed by ShipHeatNet.Register.
    public sealed class CompPilgrimHeatNet : CompVehicleHeatNet
    {
        private float savedHeat;
        private float savedDepletion;
        private bool savedStateAvailable;
        private bool networkLive;

        internal bool IsPilgrim
        {
            get { return parent is CrimsonPilgrimVehicle; }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!IsPilgrim)
            {
                base.PostSpawnSetup(respawningAfterLoad);
                return;
            }

            // Native PostSpawnSetup additionally distributes heatStoredLoaded,
            // whose native serializer only writes on save. Rebuild through the
            // public API and restore our heat AND depletion totals instead.
            RebuildHeatNet();
        }

        public override void PostExposeData()
        {
            if (!IsPilgrim)
            {
                base.PostExposeData();
                return;
            }

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                CaptureLiveTotals();
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                networkLive = false;
            }

            // Do not call the native serializer: it never reads the saved total
            // and dereferences myNet even when a vehicle is unspawned/unbuilt.
            Scribe_Values.Look(ref savedHeat, "crimsonPilgrimStoredHeat", 0f);
            Scribe_Values.Look(ref savedDepletion, "crimsonPilgrimDepletion", 0f);
            Scribe_Values.Look(ref savedStateAvailable, "crimsonPilgrimHeatStateAvailable", false);
        }

        // The N01 vehicle calls these immediately before and after its base
        // DeSpawn. This makes state preservation independent of comp ordering,
        // including native sinks later installed by other allowed upgrades.
        public void PrepareForVehicleDespawn()
        {
            if (!IsPilgrim) return;
            CaptureLiveTotals();
            networkLive = false;
        }

        public void CompleteVehicleDespawn()
        {
            if (!IsPilgrim || parent.Destroyed) return;
            // Native CompShipHeat.PostDeSpawn deregisters the vehicle's comps.
            // Recreate the private vehicle network for travel, retaining totals.
            RebuildHeatNet();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            PrepareForVehicleDespawn();
            base.PostDeSpawn(map, mode);
        }

        internal void BeforeNativeRebuild()
        {
            if (IsPilgrim) CaptureLiveTotals();
        }

        internal void AfterNativeRebuild()
        {
            if (!IsPilgrim || myNet == null) return;

            if (savedStateAvailable)
            {
                // Public native APIs mark their ratio caches dirty. Restore
                // depletion before heat: AddDepletion can otherwise vent heat.
                // Do not clamp heat to available capacity: an already hot net
                // must not cool merely because it was saved or moved.
                myNet.RemoveHeat(myNet.StorageUsed);
                myNet.RemoveDepletion(myNet.Depletion);
                myNet.AddDepletion(NonnegativeFinite(savedDepletion));
                myNet.AddHeat(NonnegativeFinite(savedHeat));
            }

            networkLive = true;
            CaptureLiveTotals();
        }

        private void CaptureLiveTotals()
        {
            if (!networkLive || myNet == null) return;
            savedHeat = NonnegativeFinite(myNet.StorageUsed);
            savedDepletion = NonnegativeFinite(myNet.Depletion);
            savedStateAvailable = true;
            heatStoredLoaded = savedHeat;
        }

        private static float NonnegativeFinite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
        }

        public override string CompInspectStringExtra()
        {
            if (myNet != null && networkLive)
            {
                return base.CompInspectStringExtra();
            }

            float capacity = 0f;
            foreach (CompShipHeatSink sink in parent.GetComps<CompShipHeatSink>())
            {
                capacity += sink.Props.heatCapacity;
            }
            return "함내 저장 열: " + savedHeat.ToString("0.##") + "/"
                + Math.Max(0f, capacity - savedDepletion).ToString("0.##") + " Hu";
        }
    }

    // The user selected vehicle self-radiation, not electric generation.
    // Uses the same sink type, capacity and native depletion API as SOS2, but
    // does not create a PowerNet, battery, fuel conversion or power output.
    public sealed class CompPilgrimHeatSink : CompShipHeatSink
    {
        private int lastProcessedTick = -1;

        public override void CompTick()
        {
            if (!(parent is CrimsonPilgrimVehicle))
            {
                base.CompTick();
                return;
            }

            ProcessHeatInterval(false);
        }

        // Called only from the lifted-off combat-mission tick. An idle world
        // vehicle or a not-yet-spawned construction does not receive this tick.
        public void ProcessActiveMissionHeat()
        {
            if (parent is CrimsonPilgrimVehicle && !parent.Spawned)
                ProcessHeatInterval(true);
        }

        private void ProcessHeatInterval(bool activeMission)
        {
            if ((!parent.Spawned && !activeMission) || parent.Destroyed || myNet == null
                || !parent.IsHashIntervalTick(120))
            {
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (lastProcessedTick == tick) return;
            lastProcessedTick = tick;

            // Native anti-entropic recovery is a coefficient per 120-tick
            // processing interval, not a percentage of capacity per second.
            if (Props.antiEntropic && myNet.Depletion > 0f)
            {
                RemoveDepletionFromNetwork(Props.heatVent * Props.antiEntropicRecoveryRate);
            }

            if (Props.heatVent > 0f && myNet.StorageUsed > 0f)
            {
                // 500 Hu / 120 ticks = 250 Hu/s. Remaining heat below 500 Hu
                // is removed partially rather than waiting for another hit.
                RemHeatFromNetwork(Mathf.Min(Props.heatVent, myNet.StorageUsed));
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastProcessedTick, "crimsonPilgrimLastHeatProcessingTick", -1);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            CompPilgrimHeatNet network = parent.TryGetComp<CompPilgrimHeatNet>();
            if (network != null) network.PrepareForVehicleDespawn();
            base.PostDeSpawn(map, mode);
        }

        public override string CompInspectStringExtra()
        {
            return "함내 자체 방열: 최대 250 Hu/s\n고갈 회복 계수: 0.2";
        }
    }

    [StaticConstructorOnStartup]
    internal static class PilgrimHeatInitialization
    {
        static PilgrimHeatInitialization()
        {
            // SOS2 upgrade actions call this nonvirtual native method through
            // CompVehicleHeatNet. Only N01's own subclass receives preservation.
            Harmony harmony = new Harmony("rimod.crimson.sos2.pilgrim.heat");
            harmony.Patch(AccessTools.Method(typeof(CompVehicleHeatNet), "RebuildHeatNet"),
                prefix: new HarmonyMethod(typeof(PilgrimHeatInitialization), "BeforeRebuild"),
                postfix: new HarmonyMethod(typeof(PilgrimHeatInitialization), "AfterRebuild"));
        }

        private static void BeforeRebuild(CompVehicleHeatNet __instance)
        {
            CompPilgrimHeatNet pilgrim = __instance as CompPilgrimHeatNet;
            if (pilgrim != null && pilgrim.IsPilgrim) pilgrim.BeforeNativeRebuild();
        }

        private static void AfterRebuild(CompVehicleHeatNet __instance)
        {
            CompPilgrimHeatNet pilgrim = __instance as CompPilgrimHeatNet;
            if (pilgrim != null && pilgrim.IsPilgrim) pilgrim.AfterNativeRebuild();
        }
    }
}
