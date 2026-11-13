using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonWorkEquipmentExtension : DefModExtension
    {
        public float carryFactor = 1f;
        public float workFactor = 1f;
        public float miningFactor = 1f;
        public float constructionFactor = 1f;

        internal float Factor(StatDef stat)
        {
            if (stat == StatDefOf.CarryingCapacity) return carryFactor;
            if (stat == StatDefOf.WorkSpeedGlobal) return workFactor;
            if (stat == StatDefOf.MiningSpeed) return miningFactor;
            if (stat == StatDefOf.ConstructionSpeed) return constructionFactor;
            return 1f;
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonWorkEquipmentRegistration
    {
        static CrimsonWorkEquipmentRegistration()
        {
            foreach (StatDef stat in new[] { StatDefOf.CarryingCapacity, StatDefOf.WorkSpeedGlobal,
                StatDefOf.MiningSpeed, StatDefOf.ConstructionSpeed })
            {
                if (stat.parts == null) stat.parts = new List<StatPart>();
                bool exists = false;
                foreach (StatPart part in stat.parts)
                    if (part is StatPart_CrimsonWorkEquipment) { exists = true; break; }
                if (!exists) stat.parts.Add(new StatPart_CrimsonWorkEquipment { parentStat = stat });
            }
        }
    }

    // Native stat parts preserve existing skills, health and other equipment.
    // Mining/Construction already factor WorkSpeedGlobal in Core: do not apply
    // the drone's global bonus again in either of those specialized parts.
    public sealed class StatPart_CrimsonWorkEquipment : StatPart
    {
        public override void TransformValue(StatRequest req, ref float value)
        {
            Pawn pawn = req.HasThing ? req.Thing as Pawn : null;
            if (pawn == null) return;
            if (pawn.apparel != null)
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                    value *= Factor(apparel.def);
            if (pawn.equipment != null && pawn.equipment.Primary != null)
                value *= Factor(pawn.equipment.Primary.def);
        }

        public override string ExplanationPart(StatRequest req)
        {
            Pawn pawn = req.HasThing ? req.Thing as Pawn : null;
            if (pawn == null) return null;
            StringBuilder result = new StringBuilder();
            if (pawn.apparel != null)
                foreach (Apparel apparel in pawn.apparel.WornApparel) Explain(result, apparel);
            if (pawn.equipment != null && pawn.equipment.Primary != null) Explain(result, pawn.equipment.Primary);
            return result.Length == 0 ? null : result.ToString().TrimEnd();
        }

        private float Factor(ThingDef def)
        {
            CrimsonWorkEquipmentExtension bonus = def.GetModExtension<CrimsonWorkEquipmentExtension>();
            return bonus == null ? 1f : bonus.Factor(parentStat);
        }

        private void Explain(StringBuilder result, Thing thing)
        {
            float factor = Factor(thing.def);
            if (factor != 1f) result.AppendLine(thing.LabelCap + ": ×" + factor.ToString("0.##"));
        }
    }
}
