using RimWorld;

namespace CrimsonSOS2
{
    public sealed class CompProperties_CrimsonVitalsFacility : CompProperties_Facility
    {
        public CompProperties_CrimsonVitalsFacility()
        {
            compClass = typeof(CrimsonVitalsFacility);
        }
    }

    // Native maxSimultaneous limits facilities per bed. This independent
    // native extension point limits beds per Crimson monitor instead.
    public sealed class CrimsonVitalsFacility : CompFacility
    {
        public override bool CanLink()
        {
            return base.CanLink() && LinkedBuildings.Count < 1;
        }
    }
}
