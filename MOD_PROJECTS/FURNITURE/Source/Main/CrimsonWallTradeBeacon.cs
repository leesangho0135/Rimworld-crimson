using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Native trade discovery deliberately enumerates this base class. Keep its
    // power, faction, stockpile button and trade-launch behavior intact.
    public sealed class CrimsonWallTradeBeacon : Building_OrbitalTradeBeacon
    {
        // The installed Core 1.6 constant is private: 7.9 cells (native CIL).
        public const float Radius = 7.9f * 5f;

        public static List<IntVec3> CoveredCells(IntVec3 center, Map map)
        {
            List<IntVec3> cells = new List<IntVec3>();
            if (map == null || !center.InBounds(map)) return cells;
            Region root = center.GetRegion(map, RegionType.Set_Passable);
            if (root == null || root.door != null) return cells;

            // Use native region boundaries, including the native door barrier.
            // The original sixteen-region cap suits 7.9 cells, not 39.5 cells.
            // Limit traversal spatially instead; no radial scan through walls,
            // persistent cache, or per-tick map scan is needed.
            RegionTraverser.BreadthFirstTraverse(root,
                delegate(Region from, Region next)
                {
                    return next.door == null
                        && next.extentsClose.ClosestDistSquaredTo(center) <= Radius * Radius;
                },
                delegate(Region region)
                {
                    foreach (IntVec3 cell in region.Cells)
                        if (cell.InHorDistOf(center, Radius)) cells.Add(cell);
                    return false;
                }, int.MaxValue, RegionType.Set_Passable);
            return cells;
        }

        public override string GetInspectString()
        {
            string original = base.GetInspectString();
            return (original.NullOrEmpty() ? "" : original + "\n")
                + "무역 탐지 반경: " + Radius.ToString("0.#") + "칸 (기본의 5배)\n벽·문 너머 탐지 불가";
        }
    }

    public sealed class CrimsonTradeBeaconRadiusPlaceWorker : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot,
            Color ghostCol, Thing thing = null)
        {
            Map map = thing != null && thing.Spawned ? thing.Map : Find.CurrentMap;
            GenDraw.DrawFieldEdges(CrimsonWallTradeBeacon.CoveredCells(center, map));
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonWallTradeBeaconRegistration
    {
        static CrimsonWallTradeBeaconRegistration()
        {
            // TradeableCells is non-virtual in 1.6. Patch its getter, only for
            // this subclass, so every native selling/launching caller sees the
            // same expanded cells. Vanilla beacons and static helpers keep
            // their original radius and behavior.
            new Harmony("rimod.crimson.sos2.wall-trade-beacon").Patch(
                AccessTools.PropertyGetter(typeof(Building_OrbitalTradeBeacon), "TradeableCells"),
                prefix: new HarmonyMethod(typeof(CrimsonWallTradeBeaconRegistration), "TradeCells"));
        }

        private static bool TradeCells(Building_OrbitalTradeBeacon __instance,
            ref IEnumerable<IntVec3> __result)
        {
            if (!(__instance is CrimsonWallTradeBeacon)) return true;
            __result = CrimsonWallTradeBeacon.CoveredCells(__instance.Position, __instance.Map);
            return false;
        }
    }
}
