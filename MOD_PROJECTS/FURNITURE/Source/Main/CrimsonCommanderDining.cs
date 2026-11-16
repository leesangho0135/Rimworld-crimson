using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CrimsonSOS2
{
    public sealed class CompProperties_CrimsonCommanderDining : CompProperties
    {
        public CompProperties_CrimsonCommanderDining()
        {
            compClass = typeof(CompCrimsonCommanderDining);
        }
    }

    public sealed class CompCrimsonCommanderDining : ThingComp
    {
        private Building_Bed linkedBed;
        private Pawn observedOwnerOne;
        private Pawn observedOwnerTwo;

        public Building_Bed LinkedBed { get { return linkedBed; } }
        private List<Pawn> AssignedBedOwners
        {
            get
            {
                if (linkedBed == null || linkedBed.Destroyed || !linkedBed.Spawned
                    || !parent.Spawned || linkedBed.Map != parent.Map) return null;
                CompAssignableToPawn owners = linkedBed.CompAssignableToPawn;
                return owners != null && SupportsOwnerCount(owners.AssignedPawnsForReading.Count)
                    ? owners.AssignedPawnsForReading : null;
            }
        }

        internal static bool SupportsOwnerCount(int count) { return count >= 1 && count <= 2; }
        private static bool EligibleOwner(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && pawn.Faction == Faction.OfPlayer;
        }
        public IEnumerable<Pawn> BedOwners
        {
            get
            {
                List<Pawn> owners = AssignedBedOwners;
                return owners == null ? Enumerable.Empty<Pawn>() : owners.Where(EligibleOwner);
            }
        }
        public Pawn BedOwner { get { return BedOwners.FirstOrDefault(); } }

        public bool Allows(Pawn pawn)
        {
            List<Pawn> owners = AssignedBedOwners;
            return EligibleOwner(pawn) && owners != null && owners.Contains(pawn);
        }

        public bool AllowsAt(Pawn pawn, IntVec3 spot)
        {
            return Allows(pawn) && (spot == SeatCell(parent) || spot == OppositeSeatCell(parent));
        }

        public AcceptanceReport TryLinkBed(Building_Bed bed)
        {
            if (bed == null || !bed.Spawned || !parent.Spawned || bed.Map != parent.Map)
                return "연결할 침대는 식탁과 같은 지도에 설치되어 있어야 합니다.";
            CompAssignableToPawn owners = bed.CompAssignableToPawn;
            if (owners == null || !SupportsOwnerCount(owners.AssignedPawnsForReading.Count))
                return "주인이 한 명 또는 두 명인 침대만 연결할 수 있습니다.";
            if (!owners.AssignedPawnsForReading.Any(EligibleOwner))
                return "살아 있는 정착민이 소유한 침대가 필요합니다.";
            linkedBed = bed;
            ObserveOwners();
            StopUnauthorizedSitting();
            return true;
        }

        public void UnlinkBed()
        {
            linkedBed = null;
            observedOwnerOne = null;
            observedOwnerTwo = null;
            StopUnauthorizedSitting();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref linkedBed, "crimsonCommanderDiningBed");
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            Pawn[] owners = BedOwners.ToArray();
            Pawn first = owners.Length > 0 ? owners[0] : null;
            Pawn second = owners.Length > 1 ? owners[1] : null;
            if (first == observedOwnerOne && second == observedOwnerTwo) return;
            observedOwnerOne = first;
            observedOwnerTwo = second;
            StopUnauthorizedSitting();
        }

        private void ObserveOwners()
        {
            Pawn[] owners = BedOwners.ToArray();
            observedOwnerOne = owners.Length > 0 ? owners[0] : null;
            observedOwnerTwo = owners.Length > 1 ? owners[1] : null;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Target
            {
                defaultLabel = "침대 연결",
                defaultDesc = "주인이 한 명 또는 두 명인 침대를 선택합니다. 연결된 침대의 주인들이 식탁 양쪽 중앙 좌석을 사용할 수 있습니다.",
                icon = TexCommand.Install,
                targetingParams = new TargetingParameters
                {
                    canTargetBuildings = true,
                    canTargetItems = false,
                    validator = target => target.Thing is Building_Bed
                },
                action = target =>
                {
                    AcceptanceReport report = TryLinkBed(target.Thing as Building_Bed);
                    if (!report.Accepted) Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                }
            };
            if (linkedBed != null)
                yield return new Command_Action
                {
                    defaultLabel = "침대 연결 해제",
                    defaultDesc = "침대 연결을 해제합니다. 새 침대를 연결할 때까지 식탁을 사용할 수 없습니다.",
                    icon = TexCommand.Install,
                    action = UnlinkBed
                };
        }

        public override string CompInspectStringExtra()
        {
            Pawn[] owners = BedOwners.ToArray();
            string state = linkedBed == null ? "침대 미연결" : owners.Length == 0
                ? "사용 불가: 같은 지도에 설치된 침대의 주인이 한 명 또는 두 명이어야 합니다."
                : "사용자: " + string.Join(", ", owners.Select(owner => owner.LabelShortCap.ToString()).ToArray());
            return state + "\n전용 좌석: 양쪽 긴 변 중앙 두 칸";
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (parent.Spawned)
            {
                GenDraw.DrawFieldEdges(new List<IntVec3> { SeatCell(parent), OppositeSeatCell(parent) }, new Color(.8f,.12f,.12f));
                if (linkedBed != null && linkedBed.Spawned && linkedBed.Map == parent.Map)
                    GenDraw.DrawLineBetween(parent.TrueCenter(), linkedBed.TrueCenter());
            }
        }

        public static IntVec3 SeatCell(Thing table)
        {
            return SeatCell(table, table.Rotation);
        }

        public static IntVec3 OppositeSeatCell(Thing table)
        {
            return SeatCell(table, table.Rotation.Opposite);
        }

        public static IEnumerable<IntVec3> SeatCells(Thing table)
        {
            yield return SeatCell(table);
            yield return OppositeSeatCell(table);
        }

        private static IntVec3 SeatCell(Thing table, Rot4 facing)
        {
            CellRect rect = table.OccupiedRect();
            switch (facing.AsInt)
            {
                case 0: return new IntVec3(rect.minX + rect.Width / 2, 0, rect.minZ - 1);
                case 1: return new IntVec3(rect.minX - 1, 0, rect.minZ + rect.Height / 2);
                case 2: return new IntVec3(rect.minX + rect.Width / 2, 0, rect.maxZ + 1);
                default: return new IntVec3(rect.maxX + 1, 0, rect.minZ + rect.Height / 2);
            }
        }

        private void StopUnauthorizedSitting()
        {
            if (!parent.Spawned) return;
            foreach (IntVec3 cell in GenAdj.CellsAdjacentCardinal(parent))
            {
                if (!cell.InBounds(parent.Map)) continue;
                foreach (Pawn pawn in cell.GetThingList(parent.Map).OfType<Pawn>().ToArray())
                    if (!AllowsAt(pawn, cell) && pawn.CurJob != null
                        && (pawn.CurJob.def == JobDefOf.Ingest || pawn.CurJob.def.driverClass == typeof(JobDriver_SocialRelax)))
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }

    // Native eating and social-relax searches both validate sitting cells here.
    // Only the perimeter of an opted-in commander table is private; generic
    // reservations for building, repairing, cleaning and hauling stay unchanged.
    internal static class CrimsonCommanderDiningAccess
    {
        internal static bool CanSit(Pawn pawn, IntVec3 spot)
        {
            if (pawn == null || !pawn.Spawned || !spot.InBounds(pawn.Map)) return true;
            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 neighbor = spot + direction;
                if (!neighbor.InBounds(pawn.Map)) continue;
                Thing table = neighbor.GetEdifice(pawn.Map);
                CompCrimsonCommanderDining dining = table == null ? null : table.TryGetComp<CompCrimsonCommanderDining>();
                if (dining != null && !dining.AllowsAt(pawn, spot))
                    return false;
            }
            return true;
        }

        internal static void CanSitPostfix([HarmonyArgument(0)] Pawn pawn,
            [HarmonyArgument(1)] IntVec3 spot, ref bool __result)
        {
            if (__result) __result = CanSit(pawn, spot);
        }

        internal static bool ReservePrefix([HarmonyArgument(0)] Pawn pawn,
            [HarmonyArgument(1)] IntVec3 spot, ref bool __result)
        {
            if (CanSit(pawn, spot)) return true;
            __result = false;
            return false;
        }

        internal static bool AdjacentSpotPrefix([HarmonyArgument(0)] IntVec3 surface,
            [HarmonyArgument(2)] Pawn pawn, [HarmonyArgument(3)] ref IntVec3 spot, ref bool __result)
        {
            if (pawn == null || !pawn.Spawned || !surface.InBounds(pawn.Map)) return true;
            Thing table = surface.GetEdifice(pawn.Map);
            CompCrimsonCommanderDining dining = table == null ? null : table.TryGetComp<CompCrimsonCommanderDining>();
            if (dining == null) return true;
            spot = IntVec3.Invalid;
            __result = false;
            if (!dining.Allows(pawn)) return false;
            foreach (IntVec3 candidate in CompCrimsonCommanderDining.SeatCells(table)
                .OrderBy(cell => cell.DistanceToSquared(pawn.Position)))
                if (candidate.InBounds(pawn.Map) && candidate.Standable(pawn.Map)
                    && !candidate.IsForbidden(pawn) && pawn.CanReserveSittableOrSpot(candidate)
                    && pawn.CanReach(candidate, PathEndMode.OnCell, Danger.Deadly))
                {
                    spot = candidate;
                    __result = true;
                    break;
                }
            return false;
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonCommanderDiningPatches
    {
        static CrimsonCommanderDiningPatches()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.commander-dining");
            harmony.Patch(AccessTools.Method(typeof(ReservationUtility), "CanReserveSittableOrSpot",
                new[] { typeof(Pawn), typeof(IntVec3), typeof(Thing), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(CrimsonCommanderDiningAccess), "CanSitPostfix"));
            harmony.Patch(AccessTools.Method(typeof(ReservationUtility), "ReserveSittableOrSpot"),
                prefix: new HarmonyMethod(typeof(CrimsonCommanderDiningAccess), "ReservePrefix"));
            harmony.Patch(AccessTools.Method(typeof(Toils_Ingest), "TryFindAdjacentIngestionPlaceSpot"),
                prefix: new HarmonyMethod(typeof(CrimsonCommanderDiningAccess), "AdjacentSpotPrefix"));
        }
    }
}
