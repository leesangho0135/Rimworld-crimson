using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Vehicles;
using Verse;

namespace CrimsonSOS2
{
    // A turret owned and persisted by CompVehicleTurrets, not a separate pawn.
    // The native SoS2VehicleTurret is internal. Its non-torpedo fuel gate is
    // reproduced here; targeting, burst scheduling, cooldown and projectiles
    // continue through the installed public VehicleTurret implementation.
    public sealed class CrimsonPilgrimBit : VehicleTurret
    {
        public int side;
        public Vector2 mountOffset = new Vector2(2.1f, 0.7f);
        public Vector2 baseSize = new Vector2(0.9f, 0.9f);
        public Vector2 barrelSize = new Vector2(0.9f, 0.9f);
        // Normalized image coordinates measured from the upper-left corner.
        public Vector2 basePivot = new Vector2(0.5f, 0.435f);
        public Vector2 barrelPivot = new Vector2(0.5f, 0.46f);
        public float hoverAmplitude = 0.06f;
        public float hoverPeriodTicks = 180f;
        public float phaseOffset;
        public bool embedded;
        public string basePath = "Crimson/Vehicles/Pilgrim/Bits/PointedBody";
        public string barrelPath = "Crimson/Vehicles/Pilgrim/Bits/PointedEmitter";

        private Graphic fixedBaseGraphic;
        private Graphic rotatingBarrelGraphic;
        private Mesh fixedBaseMesh;
        private Mesh rotatingBarrelMesh;
        private bool initialAutoTargetApplied;

        public CrimsonPilgrimBit() : base() { }

        public CrimsonPilgrimBit(VehiclePawn owner) : base(owner) { }

        // VF's CopyAndAddTurret activates this exact (owner, template) signature.
        public CrimsonPilgrimBit(VehiclePawn owner, VehicleTurret template)
            : base(owner, template)
        {
            CopyConfiguration(template as CrimsonPilgrimBit);
            if (def != null) SetMagazineCount(def.magazineCapacity);
        }

        private void CopyConfiguration(CrimsonPilgrimBit template)
        {
            if (template == null) return;
            side = template.side;
            mountOffset = template.mountOffset;
            baseSize = template.baseSize;
            barrelSize = template.barrelSize;
            basePivot = template.basePivot;
            barrelPivot = template.barrelPivot;
            hoverAmplitude = template.hoverAmplitude;
            hoverPeriodTicks = template.hoverPeriodTicks;
            phaseOffset = template.phaseOffset;
            embedded = template.embedded;
            basePath = template.basePath;
            barrelPath = template.barrelPath;
        }

        // VF rebinds reference to the XML turret template during load. Reading
        // authoring values from that reference avoids saving duplicated Def data.
        private CrimsonPilgrimBit Configuration
        {
            get { return reference as CrimsonPilgrimBit ?? this; }
        }

        internal bool BelongsToPilgrim
        {
            get { return vehicle != null && vehicle.def != null
                && (vehicle.def.defName == "Crimson_Pilgrim"
                    || vehicle.def.defName == "Crimson_PilgrimTransport"); }
        }

        internal float VehicleHeading
        {
            get { return vehicle == null ? 0f : vehicle.Rotation.AsAngle + vehicle.Angle; }
        }

        // One deterministic tick-based pose is shared by drawing, targeting,
        // shooting and the native range/line-of-sight queries. No independent
        // movement, hit points, command, deployment or return radius is created.
        internal Vector3 FloatingPosition
        {
            get
            {
                if (vehicle == null) return Vector3.zero;
                CrimsonPilgrimBit settings = Configuration;
                int ticks = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
                float[] offset = PilgrimBitGeometry.Calculate(VehicleHeading,
                    settings.mountOffset.x, settings.mountOffset.y, settings.side,
                    ticks, settings.hoverPeriodTicks, settings.embedded ? 0f : settings.hoverAmplitude,
                    settings.phaseOffset);
                return vehicle.DrawPos + new Vector3(offset[0], 0f, offset[1]);
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!BelongsToPilgrim) return;
            // Native VF defaults to manned unless an assigned turret role is
            // unfulfilled. Pilgrim's Movement-only pilot does not impose one.
            RecacheMannedStatus();
            if (!initialAutoTargetApplied)
            {
                // Capability (autoTargeting) and activation (AutoTarget) are
                // separate native values. Enable only a newly created bit;
                // keep the user's saved switch on load or later map transfers.
                if (!respawningAfterLoad)
                {
                    AutoTarget = true;
                    StartTicking();
                }
                initialAutoTargetApplied = true;
            }
            if (!respawningAfterLoad && vehicle != null && !targetInfo.IsValid)
            {
                UpdateRotationLock();
                TurretRotation = VehicleHeading;
                TurretRotationTargeted = VehicleHeading;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref initialAutoTargetApplied, "crimsonPilgrimInitialAutoTarget", false);
        }

        protected override bool TurretRotationTick()
        {
            // At rest the barrel faces the carrier's front. With a target the
            // installed aiming/rotation path remains authoritative for each bit.
            if (vehicle != null && !targetInfo.IsValid)
            {
                UpdateRotationLock();
                TurretRotationTargeted = VehicleHeading;
            }
            return base.TurretRotationTick();
        }

        public override void FireTurret()
        {
            if (!BelongsToPilgrim || def == null) return;
            CompFueledTravel fuelComp = vehicle.CompFueledTravel;
            if (fuelComp == null) return;
            // Installed SoS2VehicleTurret.FireTurret: fuel >= 1, then consume
            // 2 / magazineCapacity before delegating to the native VF shot.
            if (fuelComp.Fuel < 1f) return;
            fuelComp.ConsumeFuel(2f / def.magazineCapacity);
            base.FireTurret();
        }

        protected override void Draw()
        {
            if (!BelongsToPilgrim || !vehicle.Spawned || !ShouldDraw) return;
            CrimsonPilgrimBit settings = Configuration;
            // The regular transport's weapon uses the hull emitter, without
            // a floating body/barrel mesh. Keep only native aiming overlays.
            if (settings.embedded)
            {
                DrawTargeter();
                DrawAimPie();
                return;
            }
            if (fixedBaseGraphic == null)
            {
                fixedBaseGraphic = GraphicDatabase.Get<Graphic_Single>(settings.basePath,
                    ShaderDatabase.Transparent, settings.baseSize, Color.white);
                fixedBaseMesh = LayerMesh(settings.basePivot);
            }
            if (rotatingBarrelGraphic == null)
            {
                rotatingBarrelGraphic = GraphicDatabase.Get<Graphic_Single>(settings.barrelPath,
                    ShaderDatabase.Transparent, settings.barrelSize, Color.white);
                rotatingBarrelMesh = LayerMesh(settings.barrelPivot);
            }

            Vector3 position = FloatingPosition;
            position.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            DrawLayer(fixedBaseGraphic, fixedBaseMesh, position, VehicleHeading, settings.baseSize);
            position.y += 0.001f;
            if (!targetInfo.IsValid && !ComponentDisabled)
            {
                UpdateRotationLock();
                TurretRotation = VehicleHeading;
                TurretRotationTargeted = VehicleHeading;
            }
            DrawLayer(rotatingBarrelGraphic, rotatingBarrelMesh, position, TurretRotation, settings.barrelSize);
            DrawTargeter();
            DrawAimPie();
        }

        private static readonly Dictionary<Vector2, Mesh> LayerMeshes = new Dictionary<Vector2, Mesh>();

        private static Mesh LayerMesh(Vector2 pivot)
        {
            Mesh existing;
            if (LayerMeshes.TryGetValue(pivot, out existing)) return existing;
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[] {
                new Vector3(-pivot.x, 0f, pivot.y),
                new Vector3(1f - pivot.x, 0f, pivot.y),
                new Vector3(1f - pivot.x, 0f, pivot.y - 1f),
                new Vector3(-pivot.x, 0f, pivot.y - 1f)
            };
            mesh.uv = new Vector2[] {
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 0f), new Vector2(0f, 0f)
            };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateBounds();
            LayerMeshes.Add(pivot, mesh);
            return mesh;
        }

        private static void DrawLayer(Graphic graphic, Mesh mesh, Vector3 position, float angle, Vector2 size)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.AngleAxis(angle, Vector3.up),
                new Vector3(size.x, 1f, size.y));
            Graphics.DrawMesh(mesh, matrix, graphic.MatSingle, 0);
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonPilgrimBitPatches
    {
        static CrimsonPilgrimBitPatches()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.pilgrim.bits");
            harmony.Patch(AccessTools.PropertyGetter(typeof(VehicleTurret), "TurretLocation"),
                postfix: new HarmonyMethod(typeof(CrimsonPilgrimBitPatches), "TurretLocationPostfix"));
        }

        private static void TurretLocationPostfix(VehicleTurret __instance, ref Vector3 __result)
        {
            CrimsonPilgrimBit bit = __instance as CrimsonPilgrimBit;
            if (bit != null && bit.BelongsToPilgrim) __result = bit.FloatingPosition;
        }
    }
}
