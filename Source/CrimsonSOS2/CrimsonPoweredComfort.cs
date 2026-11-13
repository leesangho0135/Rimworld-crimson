using System.Globalization;
using System.Text;
using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CompProperties_CrimsonPoweredComfort : CompProperties
    {
        public CompProperties_CrimsonPoweredComfort()
        {
            compClass = typeof(CrimsonPoweredComfort);
        }
    }

    // This comp is attached only to Crimson furniture with an explicit base
    // Comfort. Native facility offsets and quality processing keep their order.
    public sealed class CrimsonPoweredComfort : ThingComp
    {
        private float BaseComfort
        {
            get
            {
                if (parent.def.statBases != null)
                    foreach (StatModifier modifier in parent.def.statBases)
                        if (modifier.stat == StatDefOf.Comfort) return modifier.value;
                return StatDefOf.Comfort.defaultBaseValue;
            }
        }

        private bool Powered
        {
            get
            {
                if (!parent.Spawned || parent.Destroyed) return false;
                CompPowerTrader power = parent.GetComp<CompPowerTrader>();
                if (power == null || !power.PowerOn) return false;
                CompFlickable flick = parent.GetComp<CompFlickable>();
                return flick == null || flick.SwitchIsOn;
            }
        }

        public override float GetStatOffset(StatDef stat)
        {
            return stat == StatDefOf.Comfort && Powered ? BaseComfort * 0.5f : 0f;
        }

        public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace)
        {
            base.GetStatsExplanation(stat, sb, whitespace);
            if (stat != StatDefOf.Comfort) return;
            sb.Append(whitespace).Append("전원 편안함 보정: ");
            if (Powered)
                sb.Append('+').Append((BaseComfort * 50f).ToString("0.##", CultureInfo.InvariantCulture)).Append('%');
            else
                sb.Append("없음");
            sb.AppendLine();
        }

        public override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            if (signal == "PowerTurnedOn" || signal == "PowerTurnedOff"
                || signal == "FlickedOn" || signal == "FlickedOff")
                ClearComfortCache();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            ClearComfortCache();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            base.PostDeSpawn(map, mode);
            ClearComfortCache();
        }

        private void ClearComfortCache()
        {
            StatDefOf.Comfort.Worker.ClearCacheForThing(parent);
        }
    }
}
