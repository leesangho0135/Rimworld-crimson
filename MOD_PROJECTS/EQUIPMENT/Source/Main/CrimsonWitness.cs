using SaveOurShip2;
using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CompProperties_CrimsonWitness : CompProps_BuildingConsciousness
    {
        public CompProperties_CrimsonWitness() { compClass = typeof(CrimsonWitnessConsciousness); }
    }

    public sealed class CrimsonWitnessConsciousness : CompBuildingConsciousness
    {
        private bool witnessInstalled;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureWitness();
            EnforceName();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!witnessInstalled && parent.IsHashIntervalTick(250)) EnsureWitness();
            if (parent.IsHashIntervalTick(250)) EnforceName();
        }

        private void EnsureWitness()
        {
            if (witnessInstalled || parent.Faction != Faction.OfPlayer) return;
            // A loaded or previously merged consciousness remains the same pawn.
            // Empty legacy buildings and newly built ones receive the resident.
            if (!witnessInstalled && Consciousness == null)
            {
                AIName = "목격자";
                GenerateAIPawn();
                Consciousness.Name = new NameSingle("목격자");
            }
            witnessInstalled = true;
        }

        public void EnforceName()
        {
            AIName = "목격자";
            if (Consciousness != null && (Consciousness.Name == null || Consciousness.Name.ToStringShort != "목격자"))
                Consciousness.Name = new NameSingle("목격자");
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref witnessInstalled, "crimsonWitnessInstalled", false);
        }

        public override string CompInspectStringExtra()
        {
            return base.CompInspectStringExtra() + "\n목격자 내장 · 함선 통제 기능 통합";
        }
    }
}
