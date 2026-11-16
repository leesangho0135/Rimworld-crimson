using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonMedicalFloor
    {
        private static readonly TerrainDef Floor =
            DefDatabase<TerrainDef>.GetNamed("Crimson_MedicalFloor");
        private static readonly TerrainDef InteriorTile =
            DefDatabase<TerrainDef>.GetNamed("Crimson_ShipInteriorTile");
        private static readonly TerrainDef CommanderTile =
            DefDatabase<TerrainDef>.GetNamed("Crimson_CommanderTile");

        static CrimsonMedicalFloor()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.medicalfloor");
            harmony.Patch(AccessTools.Method(typeof(Thing), "Print",
                new Type[] { typeof(SectionLayer) }),
                prefix: new HarmonyMethod(typeof(CrimsonMedicalFloor), "DeckPrintPrefix"));
            harmony.Patch(AccessTools.Method(typeof(TerrainGrid), "DoTerrainChangedEffects"),
                postfix: new HarmonyMethod(typeof(CrimsonMedicalFloor), "TerrainChanged"));
        }

        // Keep the structural deck, its roof and ship registration. Only its
        // opaque surface yields to the three opted-in Crimson finishes.
        private static bool DeckPrintPrefix(Thing __instance)
        {
            return __instance == null || !__instance.Spawned
                || __instance.def.defName != "Crimson_Deck"
                || !IsCrimsonFinish(__instance.Map.terrainGrid.TerrainAt(__instance.Position));
        }

        private static bool IsCrimsonFinish(TerrainDef terrain)
        {
            return terrain == Floor || terrain == InteriorTile || terrain == CommanderTile;
        }

        private static void TerrainChanged(IntVec3 __0, TerrainDef __1,
            TerrainDef __2, Map ___map)
        {
            if (___map != null && (IsCrimsonFinish(__1) || IsCrimsonFinish(__2)))
                ___map.mapDrawer.MapMeshDirty(__0, MapMeshFlagDefOf.Buildings);
        }
    }
}
