using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonSettings : ModSettings
    {
        public const int InitialMaximumShieldRadius = 150;
        public int maximumShieldRadius = InitialMaximumShieldRadius;
        public int defaultShieldRadius = 40;
        public const int MinimumShieldRadius = 20;
        private int shieldRadiusSettingsVersion = 1;

        public void Normalize()
        {
            maximumShieldRadius = System.Math.Max(MinimumShieldRadius, maximumShieldRadius);
            defaultShieldRadius = System.Math.Max(MinimumShieldRadius,
                System.Math.Min(maximumShieldRadius, defaultShieldRadius));
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref maximumShieldRadius, "crimsonMaximumShieldRadius", InitialMaximumShieldRadius);
            Scribe_Values.Look(ref defaultShieldRadius, "crimsonDefaultShieldRadius", 40);
            Scribe_Values.Look(ref shieldRadiusSettingsVersion, "crimsonShieldRadiusSettingsVersion", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars && shieldRadiusSettingsVersion < 1)
            {
                // Upgrade the old 60-cell limit without resetting a generator's
                // saved working/default radius or an already larger custom limit.
                maximumShieldRadius = System.Math.Max(InitialMaximumShieldRadius, maximumShieldRadius);
                shieldRadiusSettingsVersion = 1;
            }
            Normalize();
        }
    }

    // The properties object belongs exclusively to Crimson_ShieldGenerator.
    // SOS2's own +1/+10/-1/-10/reset actions and saved radius remain authoritative.
    public sealed class CrimsonShieldComp : CompShipHeatShield
    {
        private Material fieldMaterial;
        private Material hitMaterial;
        private MaterialPropertyBlock propertyBlock;
        // The square membrane artwork's main outer rim is at normalized
        // radius0.46. Match that rim to the native collision radius uniformly.
        private const float FieldArtRadiusUv = 0.46f;

        internal static void ApplyDefinitionSettings()
        {
            ThingDef definition = DefDatabase<ThingDef>.GetNamedSilentFail("Crimson_ShieldGenerator");
            if (definition == null || definition.comps == null) return;
            foreach (CompProperties properties in definition.comps)
            {
                CompProps_ShipHeat heat = properties as CompProps_ShipHeat;
                if (heat != null && heat.compClass == typeof(CrimsonShieldComp)) Configure(heat);
            }
        }

        private static void Configure(CompProps_ShipHeat properties)
        {
            CrimsonSettings settings = CrimsonMod.Settings;
            settings.Normalize();
            properties.shieldMin = CrimsonSettings.MinimumShieldRadius;
            properties.shieldMax = settings.maximumShieldRadius;
            properties.shieldDefault = settings.defaultShieldRadius;
        }

        private void ApplySettings()
        {
            Configure(Props);
            if (radiusSet >= 0f)
                radiusSet = Mathf.Clamp(radiusSet, Props.shieldMin, Props.shieldMax);
            if (radius > Props.shieldMax)
                radius = Props.shieldMax;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            ApplySettings();
            base.PostSpawnSetup(respawningAfterLoad);
        }

        public override void CompTick()
        {
            ApplySettings();
            // Preserves the original growth/shrink cadence and radius * -50 W.
            base.CompTick();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            ApplySettings();
        }

        public override void PostDraw()
        {
            if (fieldMaterial == null)
            {
                fieldMaterial = MaterialPool.MatFrom("Crimson/Shield/Membrane", ShaderDatabase.Transparent);
                hitMaterial = (Material)HarmonyLib.AccessTools.Field(typeof(CompShipHeatShield), "ConeMaterial").GetValue(null);
                propertyBlock = new MaterialPropertyBlock();
            }

            Vector3 center = parent.DrawPos;
            center.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            float alpha = Alpha();
            if (alpha > 0f)
            {
                // Artwork carries its own crimson hue; white avoids double tinting.
                Color color = Color.white;
                color.a = alpha * 0.32f;
                propertyBlock.SetColor(ShaderPropertyIDs.Color, color);
                Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.identity,
                    new Vector3(radius / FieldArtRadiusUv, 1f, radius / FieldArtRadiusUv));
                Graphics.DrawMesh(MeshPool.plane10, matrix, fieldMaterial, 0, null, 0, propertyBlock);
            }

            // Native hit-cone timing/angle/material; only the color is Crimson.
            float hitAlpha = Mathf.Clamp01(1f - (Find.TickManager.TicksGame - lastIntercepted) / 42f) * 0.99f;
            if (hitAlpha > 0f)
            {
                propertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(1f, 0.08f, 0.12f, hitAlpha));
                Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, lastInterceptAngle - 90f, 0f),
                    new Vector3(radius * 2.3f, 1f, radius * 2.3f));
                Graphics.DrawMesh(MeshPool.plane10, matrix, hitMaterial, 0, null, 0, propertyBlock);
            }
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonShieldInitialization
    {
        static CrimsonShieldInitialization()
        {
            CrimsonShieldComp.ApplyDefinitionSettings();
        }
    }
}
