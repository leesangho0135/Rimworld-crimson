using SaveOurShip2;
using Verse;

namespace CrimsonSOS2
{
    // Opted in by the two independent weapon definitions only. Keep SOS2's
    // existing bay restriction while removing its plating requirement.
    public sealed class CrimsonWeaponGroundPlaceWorker : PlaceWorker_OnShipHull
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef def, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            bool hasPlating;
            bool hasRestrictedBay;
            HasPlatingAndRestrictedBayFor(def, loc, map, out hasPlating, out hasRestrictedBay);
            return !hasRestrictedBay;
        }
    }
}
