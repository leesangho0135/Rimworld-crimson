using HarmonyLib;
using SaveOurShip2;
using Verse;
using Verse.AI;

namespace CrimsonSOS2
{
    // Keep SOS2's torpedo firing, travel, cooldown and damage pipeline. This
    // dedicated component supplies a fixed projectile without a shell inventory.
    public sealed class CompCrimsonTorpedoWarhead : CompChangeableProjectile
    {
        public override void Notify_ProjectileLaunched()
        {
            CompEquippable equipment = parent.GetComp<CompEquippable>();
            CrimsonTorpedoLauncher launcher = equipment == null || equipment.PrimaryVerb == null
                ? null : equipment.PrimaryVerb.Caster as CrimsonTorpedoLauncher;
            if (launcher != null) launcher.NotifyTorpedoFired();
        }
        public override void PostExposeData() { }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonTorpedoAmmoFree
    {
        static CrimsonTorpedoAmmoFree()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.ammofreetorpedo");
            foreach (string property in new[] { "Loaded", "LoadedNotPrevent", "FullyLoaded" })
                harmony.Patch(AccessTools.PropertyGetter(typeof(CompChangeableProjectile), property),
                    prefix: new HarmonyMethod(typeof(CrimsonTorpedoAmmoFree), "Ready"));
            harmony.Patch(AccessTools.PropertyGetter(typeof(CompChangeableProjectile), "Projectile"),
                prefix: new HarmonyMethod(typeof(CrimsonTorpedoAmmoFree), "Projectile"));
            harmony.Patch(AccessTools.PropertyGetter(typeof(CompChangeableProjectile), "StorageTabVisible"),
                prefix: new HarmonyMethod(typeof(CrimsonTorpedoAmmoFree), "StorageVisible"));
            harmony.Patch(AccessTools.PropertyGetter(typeof(Building_ShipTurret), "CanExtractTorpedo"),
                prefix: new HarmonyMethod(typeof(CrimsonTorpedoAmmoFree), "CanExtract"));
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_LoadTorpedoTube), "HasJobOnThing"),
                prefix: new HarmonyMethod(typeof(CrimsonTorpedoAmmoFree), "HasLoadingJob"));
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_LoadTorpedoTube), "JobOnThing"),
                prefix: new HarmonyMethod(typeof(CrimsonTorpedoAmmoFree), "LoadingJob"));
        }

        private static bool Ready(CompChangeableProjectile __instance, ref bool __result)
        {
            if (!(__instance is CompCrimsonTorpedoWarhead)) return true;
            __result = true;
            return false;
        }

        private static bool Projectile(CompChangeableProjectile __instance, ref ThingDef __result)
        {
            if (!(__instance is CompCrimsonTorpedoWarhead)) return true;
            __result = DefDatabase<ThingDef>.GetNamed("Crimson_SingularityFakeProjectile");
            return false;
        }

        private static bool StorageVisible(CompChangeableProjectile __instance, ref bool __result)
        {
            if (!(__instance is CompCrimsonTorpedoWarhead)) return true;
            __result = false;
            return false;
        }

        private static bool CanExtract(Building_ShipTurret __instance, ref bool __result)
        {
            if (!(__instance is CrimsonTorpedoLauncher)) return true;
            __result = false;
            return false;
        }

        private static bool HasLoadingJob(Thing __1, ref bool __result)
        {
            if (!(__1 is CrimsonTorpedoLauncher)) return true;
            __result = false;
            return false;
        }

        private static bool LoadingJob(Thing __1, ref Job __result)
        {
            if (!(__1 is CrimsonTorpedoLauncher)) return true;
            __result = null;
            return false;
        }
    }
}
