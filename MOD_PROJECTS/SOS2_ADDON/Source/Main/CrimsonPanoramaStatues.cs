using Verse;

namespace CrimsonSOS2
{
    // Deserialization shim for the two statues already present in old saves.
    public sealed class Building_CrimsonPanoramaStatue : Building
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                if (Spawned) Destroy(DestroyMode.Vanish);
            });
        }
    }
}
