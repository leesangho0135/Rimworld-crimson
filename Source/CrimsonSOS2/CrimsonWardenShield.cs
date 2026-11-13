using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CompProperties_WardenShield : CompProperties_Shield
    {
        public CompProperties_WardenShield() { compClass = typeof(CrimsonWardenShield); }
    }

    // Native shield mechanics, with a palette and two small rim arcs exclusive to this cloak.
    [StaticConstructorOnStartup]
    public sealed class CrimsonWardenShield : CompShield
    {
        private bool enabled = true;
        private static Material bubble;
        private static Material arc;
        private static readonly FieldInfo lastImpact = AccessTools.Field(typeof(CompShield), "lastAbsorbDamageTick");
        private static readonly FieldInfo impactDirection = AccessTools.Field(typeof(CompShield), "impactAngleVect");
        public bool Enabled { get { return enabled; } set { enabled = value; } }

        public override void PostExposeData()
        { base.PostExposeData(); Scribe_Values.Look(ref enabled, "crimsonShieldEnabled", true); }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            bool active = ShieldState == ShieldState.Active;
            float size = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, Energy) * 1.25f;
            if (enabled || dinfo.Def == DamageDefOf.EMP) base.PostPreApplyDamage(ref dinfo, out absorbed);
            // The native Break path skips attached effects for an unspawned held
            // apparel item. Attach this cloak's native-shaped tint to its wearer.
            if (active && ShieldState == ShieldState.Resetting && !parent.Spawned
                && PawnOwner != null && PawnOwner.Spawned)
                BreakEffectFor(EffecterDefOf.Shield_Break, this).SpawnAttached(PawnOwner, PawnOwner.Map, size);
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetWornGizmosExtra()) yield return gizmo;
            if (PawnOwner == null || !PawnOwner.IsColonistPlayerControlled) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "심홍 보호막",
                defaultDesc = "투사체를 차단하는 심홍 보호막을 켜거나 끕니다. 전용 보호막은 켜진 상태에서도 원거리 무기를 발사할 수 있습니다.",
                icon = CrimsonWardenArt.Icon("Cloak"),
                isActive = delegate { return enabled; },
                toggleAction = delegate { enabled = !enabled; }
            };
        }

        public override void CompDrawWornExtras()
        {
            if (!enabled || ShieldState != ShieldState.Active || !ShouldDisplay || PawnOwner == null) return;
            if (bubble == null)
            {
                Material native = (Material)typeof(CompShield).GetField("BubbleMat", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                bubble = new Material(native);
                bubble.color = new Color(1f, 0.055f, 0.15f, 1f);
                arc = new Material(ShaderDatabase.TransparentPostLight);
                arc.mainTexture = BaseContent.WhiteTex;
                arc.color = new Color(1f, 0.13f, 0.22f, 0.75f);
            }
            float size = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, Energy) * 1.25f;
            Vector3 center = PawnOwner.DrawPos;
            center.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            int tick = Find.TickManager.TicksGame;
            int elapsed = tick - (int)lastImpact.GetValue(this);
            if (elapsed < 8)
            {
                float jitter = (8 - elapsed) / 8f * .05f;
                center += (Vector3)impactDirection.GetValue(this) * jitter;
                size -= jitter;
            }
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(center, Quaternion.AngleAxis(Rand.Range(0, 360), Vector3.up), new Vector3(size, 1f, size)), bubble, 0);
            for (int i = 0; i < 2; i++)
            {
                float angle = (tick / 12 * 47 + parent.thingIDNumber * 13 + i * 177) * Mathf.Deg2Rad;
                Vector3 prior = ArcPoint(center, angle, size * .455f);
                for (int segment = 1; segment <= 3; segment++)
                {
                    float radius = size * (segment == 2 ? .426f : .455f);
                    Vector3 next = ArcPoint(center, angle + segment * .105f, radius);
                    Vector3 delta = next - prior;
                    Vector3 middle = (prior + next) * .5f; middle.y += .001f;
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(middle, Quaternion.LookRotation(delta),
                        new Vector3(.012f, 1f, delta.magnitude)), arc, 0);
                    prior = next;
                }
            }
        }

        private static Vector3 ArcPoint(Vector3 center, float angle, float radius)
        { return center + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius); }

        public static EffecterDef BreakEffectFor(EffecterDef original, CompShield shield)
        {
            return shield is CrimsonWardenShield
                ? DefDatabase<EffecterDef>.GetNamed("Crimson_WardenShieldBreak") : original;
        }
    }

}
