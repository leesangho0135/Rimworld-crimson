using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CompProperties_WardenBlade : CompProperties_BladelinkWeapon
    {
        public List<WeaponTraitDef> fixedTraits;
        public CompProperties_WardenBlade() { compClass = typeof(CrimsonWardenBladeTraits); }
    }

    public sealed class CrimsonWardenBladeTraits : CompBladelinkWeapon
    {
        private static readonly FieldInfo traitsField = AccessTools.Field(typeof(CompBladelinkWeapon), "traits");
        public override void PostPostMake() { traitsField.SetValue(this, new List<WeaponTraitDef>()); }
        protected override void OnCodedFor(Pawn pawn)
        {
            SetFixedTraits(true);
            base.OnCodedFor(pawn);
        }
        public override void UnCode()
        {
            Pawn owner = CodedPawn; base.UnCode(); SetFixedTraits(false);
            if (owner != null) StatDefOf.MeleeHitChance.Worker.ClearCacheForThing(owner);
        }
        public override void Notify_Equipped(Pawn pawn)
        { base.Notify_Equipped(pawn); StatDefOf.MeleeHitChance.Worker.ClearCacheForThing(pawn); }
        public override void Notify_Unequipped(Pawn pawn)
        { base.Notify_Unequipped(pawn); StatDefOf.MeleeHitChance.Worker.ClearCacheForThing(pawn); }
        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) SetFixedTraits(Biocoded);
        }
        private void SetFixedTraits(bool bonded)
        {
            List<WeaponTraitDef> list = TraitsListForReading;
            if (list == null) { list = new List<WeaponTraitDef>(); traitsField.SetValue(this, list); }
            list.Clear();
            if (bonded) list.AddRange(((CompProperties_WardenBlade)props).fixedTraits);
            StatDefOf.MeleeWeapon_CooldownMultiplier.Worker.ClearCacheForThing(parent);
        }
        public override float GetStatFactor(StatDef stat)
        {
            float result = base.GetStatFactor(stat);
            if (Biocoded && TraitsListForReading != null)
                foreach (WeaponTraitDef trait in TraitsListForReading)
                    result *= StatUtility.GetStatFactorFromList(trait.statFactors, stat);
            return result;
        }
    }

    public sealed class CompProperties_WardenGunTraits : CompProperties
    {
        public List<WeaponTraitDef> fixedTraits;
        public CompProperties_WardenGunTraits() { compClass = typeof(CrimsonWardenGunTraits); }
    }

    public sealed class CrimsonWardenGunTraits : ThingComp
    {
        public List<WeaponTraitDef> TraitsListForReading { get { return ((CompProperties_WardenGunTraits)props).fixedTraits; } }
        public override void Notify_Equipped(Pawn pawn)
        { base.Notify_Equipped(pawn); ClearTimingCache(); }
        public override void Notify_Unequipped(Pawn pawn)
        { base.Notify_Unequipped(pawn); ClearTimingCache(); }
        public override void PostExposeData()
        { if (Scribe.mode == LoadSaveMode.PostLoadInit) ClearTimingCache(); }
        private void ClearTimingCache()
        {
            StatDefOf.RangedWeapon_WarmupMultiplier.Worker.ClearCacheForThing(parent);
            StatDefOf.RangedWeapon_Cooldown.Worker.ClearCacheForThing(parent);
        }
        public override float GetStatFactor(StatDef stat)
        {
            float result = 1f;
            foreach (WeaponTraitDef trait in TraitsListForReading)
                result *= StatUtility.GetStatFactorFromList(trait.statFactors, stat);
            Pawn_EquipmentTracker holder = parent.ParentHolder as Pawn_EquipmentTracker;
            if ((stat == StatDefOf.RangedWeapon_WarmupMultiplier || stat == StatDefOf.RangedWeapon_Cooldown) && holder != null)
                result *= 1f - .15f * CrimsonCombatResonance.Stacks(holder.pawn);
            return result;
        }
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            int priority = 4500;
            foreach (WeaponTraitDef trait in TraitsListForReading)
                yield return new StatDrawEntry(DefDatabase<StatCategoryDef>.GetNamed("Weapon_Ranged"), trait.LabelCap, "고정 특성", trait.description, priority--);
        }
        public override string GetDescriptionPart()
        {
            string text = "";
            foreach (WeaponTraitDef trait in TraitsListForReading) text += trait.LabelCap + ": " + trait.description + "\n";
            return text.TrimEnd();
        }
    }

    public static class CrimsonCombatResonance
    {
        public static bool Active(Pawn pawn)
        {
            if (pawn == null || pawn.health == null) return false;
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Crimson_CombatResonance"));
            HediffComp_Disappears timer = hediff == null ? null : hediff.TryGetComp<HediffComp_Disappears>();
            return timer != null && timer.ticksToDisappear > 0;
        }
        public static int Stacks(Pawn pawn)
        {
            if (!Active(pawn)) return 0;
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Crimson_CombatResonance"));
            return Mathf.Clamp(Mathf.RoundToInt(hediff.Severity), 1, 5);
        }
        public static void RegisterHit(Pawn pawn)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamed("Crimson_CombatResonance");
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff == null) { hediff = HediffMaker.MakeHediff(def, pawn); pawn.health.AddHediff(hediff); }
            else hediff.Severity = Mathf.Min(5f, hediff.Severity + 1f);
            hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear = 360;
            ClearTimingCache(pawn);
        }
        public static void ClearTimingCache(Pawn pawn)
        {
            ThingWithComps weapon = pawn == null || pawn.equipment == null ? null : pawn.equipment.Primary;
            if (weapon != null && weapon.def.defName == "Crimson_WardenPistol")
            {
                StatDefOf.RangedWeapon_WarmupMultiplier.Worker.ClearCacheForThing(weapon);
                StatDefOf.RangedWeapon_Cooldown.Worker.ClearCacheForThing(weapon);
            }
        }
    }

    public sealed class CrimsonCombatResonanceHediff : HediffWithComps
    {
        public override string LabelInBrackets
        {
            get { string timer = base.LabelInBrackets; return Mathf.RoundToInt(Severity) + "/5" + (string.IsNullOrEmpty(timer) ? "" : ", " + timer); }
        }
        public override void PostAdd(DamageInfo? dinfo)
        { base.PostAdd(dinfo); CrimsonCombatResonance.ClearTimingCache(pawn); }
        public override void PostRemoved()
        { base.PostRemoved(); CrimsonCombatResonance.ClearTimingCache(pawn); }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonWardenTraitsRegistration
    {
        static CrimsonWardenTraitsRegistration()
        {
            Harmony harmony = new Harmony("rimod.crimson.warden.weapon-traits");
            harmony.CreateClassProcessor(typeof(CrimsonWardenCertainEnd)).Patch();
            harmony.CreateClassProcessor(typeof(CrimsonWardenResonanceDamage)).Patch();
        }
    }

    [HarmonyPatch(typeof(StatWorker), "FinalizeValue")]
    internal static class CrimsonWardenCertainEnd
    {
        private static void Postfix(StatDef ___stat, StatRequest req, ref float val, bool applyPostProcess)
        {
            if (___stat != StatDefOf.MeleeHitChance || !applyPostProcess) return;
            Pawn pawn = req.Thing as Pawn;
            ThingWithComps weapon = pawn == null || pawn.equipment == null ? null : pawn.equipment.Primary;
            CrimsonWardenBladeTraits blade = weapon == null ? null : weapon.TryGetComp<CrimsonWardenBladeTraits>();
            if (blade != null && blade.Biocoded && blade.CodedPawn == pawn && blade.TraitsListForReading.Count == 2)
                val = Mathf.Min(1f, val + .15f);
        }
    }

    // Hostility is captured before damage, so a lethal hit still earns one stack.
    [HarmonyPatch(typeof(Thing), "TakeDamage")]
    internal static class CrimsonWardenResonanceDamage
    {
        private static Pawn Source(DamageInfo dinfo)
        {
            if (dinfo.Weapon == null || dinfo.Weapon.defName != "Crimson_WardenPistol") return null;
            Pawn pawn = dinfo.Instigator as Pawn;
            ThingWithComps weapon = pawn == null || pawn.equipment == null ? null : pawn.equipment.Primary;
            return pawn == null || pawn.Dead || weapon == null || weapon.def.defName != "Crimson_WardenPistol"
                || weapon.TryGetComp<CrimsonWardenGunTraits>() == null ? null : pawn;
        }
        private static void Prefix(Thing __instance, DamageInfo dinfo, out bool __state)
        {
            Pawn pawn = Source(dinfo);
            __state = pawn != null && __instance.HostileTo(pawn);
        }
        private static void Postfix(DamageInfo dinfo, DamageWorker.DamageResult __result, bool __state)
        {
            if (!__state || __result == null || __result.totalDamageDealt <= 0) return;
            Pawn pawn = Source(dinfo);
            if (pawn != null) CrimsonCombatResonance.RegisterHit(pawn);
        }
    }
}
