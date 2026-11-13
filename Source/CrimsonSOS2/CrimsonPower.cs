using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonPowerArt : DefModExtension
    {
        public string activePath;
        public string inactivePath;
        public bool directional;
        public bool engine;
        public bool battery;
        public Vector2 drawSize = Vector2.one;
    }

    public sealed class CrimsonCapacitorBodyGraphic : Graphic_Multi
    {
        public override bool ShouldDrawRotated { get { return false; } }

        public override Mesh MeshAt(Rot4 rot)
        {
            Vector2 size = rot.IsHorizontal ? new Vector2(drawSize.y, drawSize.x) : drawSize;
            CrimsonPowerRendering.FitCapacitorToFootprint(ref size, rot);
            return MeshPool.GridPlane(size);
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef,
            Thing thing, float extraRotation)
        {
            Vector2 size = rot.IsHorizontal ? new Vector2(drawSize.y, drawSize.x) : drawSize;
            loc += CrimsonPowerRendering.FitCapacitorToFootprint(ref size, rot);
            base.DrawWorker(loc, rot, thingDef, thing, extraRotation);
        }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonCapacitorBodyGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }
    }

    // Map the unchanged engine body pixels into the existing 5x7/7x5 plane.
    // Each state maps into the same world body plane. Former active images
    // are retained as the inactive state; generated active artwork has its
    // own source registration. No PNG is resized here.
    public sealed class CrimsonEngineBodyGraphic : Graphic_Single
    {
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            Rect crop;
            if (!CrimsonPowerRendering.TryGetEngineBodyCrop(request.path, out crop)) return;
            Material registered = new Material(mat);
            Vector2 cropScale = new Vector2(crop.width, crop.height);
            registered.mainTextureScale = Vector2.Scale(mat.mainTextureScale, cropScale);
            registered.mainTextureOffset = mat.mainTextureOffset
                + Vector2.Scale(mat.mainTextureScale, new Vector2(crop.x, crop.y));
            mat = registered;
        }
    }

    public sealed class CrimsonTrailLayout
    {
        public Vector2 drawSize;
        public Vector2 offset;
    }

    public sealed class CompProperties_CrimsonEngine : CompProps_EngineTrail
    {
        public string flamePath;
        public List<CrimsonTrailLayout> flameLayouts;
        public float displayThrustMultiplier = 500f;
        public bool reactionlessExhaustDamage = false;

        public CompProperties_CrimsonEngine()
        {
            compClass = typeof(CompCrimsonEngine);
        }
    }

    public sealed class CompCrimsonEngine : CompEngineTrail
    {
        public new CompProperties_CrimsonEngine Props
        {
            get { return (CompProperties_CrimsonEngine)props; }
        }

        private bool UsesReactionlessExhaustDamage
        {
            get
            {
                return Props.reactionless && Props.reactionlessExhaustDamage
                    && parent != null && parent.def != null
                    && parent.def.defName == "Crimson_JohnsonTanakaLarge";
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!UsesReactionlessExhaustDamage || !parent.Spawned || parent.Map == null) return;

            // Native reactionless engines skip area construction. This opt-in
            // keeps their propulsion rules and uses the native exhaust geometry.
            ExhaustArea.Clear();
            CellRect area = GenAdjExtension.GetDirectAdjacentRect(parent.Position.ToIntVec2,
                (byte)parent.Rotation.Rotated(RotationDirection.Opposite).AsInt,
                parent.OccupiedRect(), Props.killZoneWidth, Props.killZoneLength,
                Props.killZoneExtraOffset);
            foreach (IntVec3 cell in area)
                if (cell.InBounds(parent.Map)) ExhaustArea.Add(cell);
        }

        public override void CompTick()
        {
            // Native code retains its switch, power and fuel checks. Its own
            // damage remains the only path for non-reactionless nuclear engines.
            base.CompTick();
            if (!UsesReactionlessExhaustDamage || !active || !parent.Spawned || parent.Map == null) return;

            Map map = parent.Map;
            HashSet<Thing> targets = new HashSet<Thing>();
            foreach (IntVec3 cell in ExhaustArea)
            {
                if (!cell.InBounds(map)) continue;
                foreach (Thing thing in new List<Thing>(cell.GetThingList(map)))
                {
                    if ((thing.def.useHitPoints || thing is Pawn)
                        && thing.def.altitudeLayer != AltitudeLayer.Terrain)
                        targets.Add(thing);
                }
            }
            foreach (Thing thing in targets)
            {
                // These optional constructor defaults match the native 14-arg
                // call, including a null instigator rather than the engine parent.
                thing.TakeDamage(new DamageInfo(DamageDefOf.Bomb, 100f, 0f, -1f));
            }
        }

        public override void PostDraw()
        {
            // Only rendering is replaced. Native On/Off, fuel consumption,
            // CanFire, ship registration and the configured ExhaustArea remain native.
            if (!active || Props.reactionless || string.IsNullOrEmpty(Props.flamePath)
                || Props.flameLayouts == null || Props.flameLayouts.Count != 4)
            {
                return;
            }

            CrimsonTrailLayout layout = Props.flameLayouts[parent.Rotation.AsInt];
            string path = Props.flamePath + "_" + CrimsonPowerRendering.Direction(parent.Rotation);
            Vector3 position = parent.DrawPos + new Vector3(layout.offset.x, -0.01f, layout.offset.y);
            CrimsonPowerRendering.GraphicFor(path, layout.drawSize, true).Draw(position, Rot4.North, parent);
        }

        public override string CompInspectStringExtra()
        {
            string result = "추력: " + (Thrust * Props.displayThrustMultiplier).ToString("N0");
            if (refuelComp != null)
            {
                result += "\n연료 소모: " + FuelUse + "/s";
            }
            return result;
        }
    }

    public sealed class CompProperties_CrimsonBattery : CompProperties_Battery
    {
        public CompProperties_CrimsonBattery()
        {
            compClass = typeof(CompCrimsonBattery);
        }
    }

    public sealed class CompCrimsonBattery : CompPowerBattery
    {
        private double pendingSelfDischargeWd;

        public override void CompTick()
        {
            float before = StoredEnergy;
            if (before <= 0f)
            {
                pendingSelfDischargeWd = 0d;
                return;
            }

            // Native CompPowerBattery.CompTick only calls ThingComp's empty
            // tick and draws 5 W. Do not call it again: accumulate that same
            // discharge here so a 250000 Wd float cannot round every tick away.
            pendingSelfDischargeWd += Math.Min((double)before,
                5d * (double)CompPower.WattsToWattDaysPerTick);
            if (pendingSelfDischargeWd > 0d)
            {
                DrawPower((float)Math.Min(pendingSelfDischargeWd, (double)before));
                // Keep a signed residual: float rounding can remove slightly
                // more than requested, which subsequent ticks must repay.
                pendingSelfDischargeWd -= (double)before - (double)StoredEnergy;
            }
            if (StoredEnergy <= 0f) pendingSelfDischargeWd = 0d;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref pendingSelfDischargeWd, "crimsonPendingSelfDischargeWd", 0d);
        }
    }

    [StaticConstructorOnStartup]
    public static class CrimsonPowerPatches
    {
        static CrimsonPowerPatches()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.power");
            harmony.Patch(AccessTools.Method(typeof(PowerNet), "DistributeEnergyAmongBatteries"),
                prefix: new HarmonyMethod(typeof(CrimsonPowerPatches), "DistributeEnergyPrefix"));
        }

        private static bool DistributeEnergyPrefix(PowerNet __instance, float energy)
        {
            if (energy <= 0f || __instance == null || __instance.batteryComps == null)
            {
                return true;
            }

            bool hasCrimson = false;
            foreach (CompPowerBattery battery in __instance.batteryComps)
            {
                if (IsCrimson(battery))
                {
                    hasCrimson = true;
                    break;
                }
            }
            if (!hasCrimson)
            {
                return true;
            }

            // Weighted water filling consumes exactly the supplied surplus.
            // AmountCanAccept retains native efficiency, EMP and breakdown
            // handling. Saturated batteries leave the next redistribution round.
            List<CompPowerBattery> waiting = new List<CompPowerBattery>();
            foreach (CompPowerBattery battery in __instance.batteryComps)
            {
                if (battery != null && battery.AmountCanAccept > 0f)
                {
                    waiting.Add(battery);
                }
            }

            int maximumRounds = waiting.Count + 1;
            for (int round = 0; round < maximumRounds && energy > 0.0000001f && waiting.Count > 0; round++)
            {
                float totalWeight = 0f;
                foreach (CompPowerBattery battery in waiting)
                {
                    totalWeight += IsCrimson(battery) ? 25f : 1f;
                }

                float budget = energy;
                float spent = 0f;
                foreach (CompPowerBattery battery in waiting)
                {
                    float share = budget * (IsCrimson(battery) ? 25f : 1f) / totalWeight;
                    float accepted = Mathf.Min(share, Mathf.Max(0f, battery.AmountCanAccept));
                    if (accepted > 0f)
                    {
                        battery.AddEnergy(accepted);
                        spent += accepted;
                    }
                }

                energy = Mathf.Max(0f, energy - spent);
                for (int i = waiting.Count - 1; i >= 0; i--)
                {
                    if (waiting[i].AmountCanAccept <= 0.0000001f)
                    {
                        waiting.RemoveAt(i);
                    }
                }
                if (spent <= 0.0000001f)
                {
                    break;
                }
            }
            return false;
        }

        private static bool IsCrimson(CompPowerBattery battery)
        {
            return battery is CompCrimsonBattery && battery.parent != null
                && battery.parent.def.defName == "Crimson_Capacitor";
        }
    }

    public sealed class CrimsonPowerBuilding : Building
    {
        private static readonly System.Reflection.MethodInfo PreventDrawingMethod =
            AccessTools.Method(typeof(ThingWithComps), "CompsPreventDrawing");
        private Mesh fillMesh;
        private float lastFill = -1f;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            CrimsonPowerArt art = def.GetModExtension<CrimsonPowerArt>();
            if (art == null)
            {
                base.DrawAt(drawLoc, flip);
                return;
            }

            bool on;
            CompEngineTrail engineComp = this.TryGetComp<CompEngineTrail>();
            if (art.engine)
            {
                on = engineComp != null && engineComp.active;
            }
            else
            {
                CompPowerTrader power = this.TryGetComp<CompPowerTrader>();
                CompFlickable flick = this.TryGetComp<CompFlickable>();
                CompRefuelable fuel = this.TryGetComp<CompRefuelable>();
                on = (power == null || (power.PowerOn && power.PowerOutput > 0f))
                    && (flick == null || flick.SwitchIsOn)
                    && (fuel == null || fuel.HasFuel)
                    && !this.IsBrokenDown();
            }

            string path = !on && !string.IsNullOrEmpty(art.inactivePath) ? art.inactivePath : art.activePath;
            Vector2 size = art.drawSize;
            if (art.directional)
            {
                path += "_" + CrimsonPowerRendering.Direction(Rotation);
                if (Rotation.IsHorizontal)
                {
                    size = new Vector2(size.y, size.x);
                }
            }
            Vector3 bodyLoc = drawLoc;
            if (art.battery && def.defName == "Crimson_Capacitor")
            {
                bodyLoc += CrimsonPowerRendering.FitCapacitorToFootprint(ref size, Rotation);
            }
            bool preventsDrawing = (bool)PreventDrawingMethod.Invoke(this, null);
            if (!preventsDrawing)
            {
                CrimsonPowerRendering.GraphicFor(path, size, false).Draw(bodyLoc, Rot4.North, this);
            }
            Comps_DrawAt(drawLoc, flip);
            Comps_PostDraw();

            if (art.battery && !preventsDrawing)
            {
                DrawChargeGauge(bodyLoc, size);
            }
        }

        private void DrawChargeGauge(Vector3 drawLoc, Vector2 bodySize)
        {
            CompPowerBattery battery = this.TryGetComp<CompPowerBattery>();
            if (battery == null)
            {
                return;
            }

            int direction = Rotation.AsInt;
            CrimsonPowerRendering.GaugeRegistration r = CrimsonPowerRendering.Gauges[direction];
            float sx = bodySize.x / r.canvas.x;
            float sz = bodySize.y / r.canvas.y;
            bool horizontal = Rotation.IsHorizontal;
            Vector3 center = drawLoc + new Vector3((r.backCenter.x - r.canvas.x / 2f) * sx,
                0.02f, (r.canvas.y / 2f - r.backCenter.y) * sz);
            Vector2 backSize = horizontal
                ? new Vector2(r.backSize.x * sz, r.backSize.y * sx)
                : new Vector2(r.backSize.x * sx, r.backSize.y * sz);

            const string emptyPath = "Crimson/Power/Battery/GaugeEmpty";
            const string fillPath = "Crimson/Power/Battery/ChargeFill";
            Texture2D emptyTexture = ContentFinder<Texture2D>.Get(emptyPath);
            Mesh emptyMesh = CrimsonPowerRendering.EmptyGaugeMesh(emptyTexture);
            CrimsonPowerRendering.DrawQuad(emptyMesh, emptyPath, center, backSize, direction * 90f);

            float fill = Mathf.Clamp01(battery.StoredEnergyPct);
            if (fill <= 0f)
            {
                return;
            }

            Texture2D fillTexture = ContentFinder<Texture2D>.Get(fillPath);
            if (fillMesh == null)
            {
                fillMesh = CrimsonPowerRendering.QuadMesh();
            }
            if (Mathf.Abs(lastFill - fill) > 0.000001f)
            {
                CrimsonPowerRendering.SetCropUv(fillMesh, fillTexture,
                    new Rect(724f, 308f, 724f * fill, 108f));
                lastFill = fill;
            }

            float length = horizontal ? r.slot.height * sz : r.slot.width * sx;
            float thickness = horizontal ? r.slot.width * sx : r.slot.height * sz;
            Vector3 slotCenter = drawLoc + new Vector3(
                (r.slot.center.x - r.canvas.x / 2f) * sx, 0.021f,
                (r.canvas.y / 2f - r.slot.center.y) * sz);
            Vector3 flow = Rotation.AsQuat * Vector3.right;
            slotCenter += flow * (length * (fill - 1f) / 2f);
            CrimsonPowerRendering.DrawQuad(fillMesh, fillPath, slotCenter,
                new Vector2(length * fill, thickness), direction * 90f);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            if (fillMesh != null)
            {
                UnityEngine.Object.Destroy(fillMesh);
                fillMesh = null;
                lastFill = -1f;
            }
        }
    }

    internal static class CrimsonPowerRendering
    {
        private static readonly Dictionary<string, Graphic> GraphicsCache = new Dictionary<string, Graphic>();
        private static Mesh emptyGauge;

        // Alpha16 bounds plus two source pixels of padding. Fit the complete
        // body, including connectors, inside the occupied cells in every view.
        // Preview, realtime body and charge gauge share this exact transform.
        private static readonly Rect[] CapacitorBodyBounds =
        {
            new Rect(167f, 59f, 690f, 1387f), new Rect(115f, 165f, 1357f, 693f),
            new Rect(178f, 126f, 670f, 1293f), new Rect(101f, 183f, 1246f, 650f)
        };

        internal static Vector3 FitCapacitorToFootprint(ref Vector2 size, Rot4 rotation)
        {
            Rect bounds = CapacitorBodyBounds[rotation.AsInt];
            Vector2 canvas = Gauges[rotation.AsInt].canvas;
            ThingDef def = DefDatabase<ThingDef>.GetNamed("Crimson_Capacitor");
            Vector2 footprint = rotation.IsHorizontal
                ? new Vector2(def.size.z, def.size.x) : new Vector2(def.size.x, def.size.z);
            float scale = Mathf.Min(1f, Mathf.Min(
                (footprint.x - 0.1f) / (bounds.width * size.x / canvas.x),
                (footprint.y - 0.1f) / (bounds.height * size.y / canvas.y)));
            size *= scale;
            return new Vector3((canvas.x / 2f - bounds.center.x) * size.x / canvas.x,
                0f, (bounds.center.y - canvas.y / 2f) * size.y / canvas.y);
        }

        internal sealed class GaugeRegistration
        {
            public Vector2 canvas;
            public Vector2 backCenter;
            public Vector2 backSize;
            public Rect slot;

            public GaugeRegistration(Vector2 canvas, Vector2 center, Vector2 size, Rect slot)
            {
                this.canvas = canvas;
                backCenter = center;
                backSize = size;
                this.slot = slot;
            }
        }

        // Source pixels and UV crops from design-v8-battery-registration.json.
        internal static readonly GaugeRegistration[] Gauges =
        {
            new GaugeRegistration(new Vector2(1024f, 1536f), new Vector2(512f, 1383f),
                new Vector2(189.1f, 54f), new Rect(437.92727f, 1376.0973f, 148.04385f, 14.24324f)),
            new GaugeRegistration(new Vector2(1536f, 1024f), new Vector2(174.5f, 514.5f),
                new Vector2(184.45f, 63f), new Rect(165.60270f, 442.32250f, 17.28378f, 144.25588f)),
            new GaugeRegistration(new Vector2(1024f, 1536f), new Vector2(513f, 182.5f),
                new Vector2(182.9f, 57f), new Rect(441.55253f, 174.64054f, 142.99323f, 15.25676f)),
            new GaugeRegistration(new Vector2(1536f, 1024f), new Vector2(1297f, 508f),
                new Vector2(182.9f, 60f), new Rect(1289.10811f, 436.55253f, 16.27027f, 142.99323f))
        };

        internal static string Direction(Rot4 rotation)
        {
            return new[] { "north", "east", "south", "west" }[rotation.AsInt];
        }

        internal static Graphic GraphicFor(string path, Vector2 size, bool light)
        {
            Graphic graphic;
            if (!GraphicsCache.TryGetValue(path, out graphic))
            {
                Rect crop;
                if (!light && TryGetEngineBodyCrop(path, out crop))
                {
                    graphic = GraphicDatabase.Get<CrimsonEngineBodyGraphic>(path,
                        ShaderDatabase.Cutout, size, Color.white);
                }
                else
                {
                    graphic = GraphicDatabase.Get<Graphic_Single>(path,
                        light ? ShaderDatabase.TransparentPostLight : ShaderDatabase.Cutout, size, Color.white);
                }
                GraphicsCache.Add(path, graphic);
            }
            return graphic;
        }

        // Lower-left normalized UVs from the direction's alpha16 union of the
        // locked source body states plus a conservative 2-pixel margin.
        // Exact paths only: flames, batteries and other devices stay full UV.
        internal static bool TryGetEngineBodyCrop(string texturePath, out Rect crop)
        {
            switch (texturePath)
            {
                case "Crimson/Power/Nuclear/Active_north":
                    crop = new Rect(167f / 1060f, 0f / 1484f, 727f / 1060f, 1450f / 1484f);
                    return true;
                case "Crimson/Power/Nuclear/Inactive_north":
                    crop = new Rect(166f / 1060f, 0f, 730f / 1060f, 1450f / 1484f);
                    return true;
                case "Crimson/Power/Nuclear/Active_east":
                    crop = new Rect(24f / 1484f, 175f / 1060f, 1424f / 1484f, 730f / 1060f);
                    return true;
                case "Crimson/Power/Nuclear/Inactive_east":
                    crop = new Rect(24f / 1484f, 175f / 1060f, 1423f / 1484f, 730f / 1060f);
                    return true;
                case "Crimson/Power/Nuclear/Active_south":
                    crop = new Rect(163f / 1042f, 20f / 1510f, 722f / 1042f, 1482f / 1510f);
                    return true;
                case "Crimson/Power/Nuclear/Inactive_south":
                    crop = new Rect(172f / 1060f, 26f / 1484f, 717f / 1060f, 1449f / 1484f);
                    return true;
                case "Crimson/Power/Nuclear/Active_west":
                    crop = new Rect(34f / 1484f, 198f / 1060f, 1439f / 1484f, 662f / 1060f);
                    return true;
                case "Crimson/Power/Nuclear/Inactive_west":
                    crop = new Rect(33f / 1484f, 197f / 1060f, 1431f / 1484f, 664f / 1060f);
                    return true;
                case "Crimson/Power/Tanaka/Body_north":
                    crop = new Rect(95f / 1060f, 63f / 1484f, 870f / 1060f, 1358f / 1484f);
                    return true;
                case "Crimson/Power/Tanaka/Body_east":
                    crop = new Rect(48f / 1484f, 103f / 1060f, 1372f / 1484f, 824f / 1060f);
                    return true;
                case "Crimson/Power/Tanaka/Body_south":
                    crop = new Rect(92f / 1060f, 100f / 1484f, 876f / 1060f, 1323f / 1484f);
                    return true;
                case "Crimson/Power/Tanaka/Body_west":
                    crop = new Rect(67f / 1484f, 122f / 1060f, 1351f / 1484f, 789f / 1060f);
                    return true;
                case "Crimson/Power/Tanaka/Active_north":
                    crop = new Rect(97f / 1042f, 65f / 1510f, 874f / 1042f, 1380f / 1510f);
                    return true;
                case "Crimson/Power/Tanaka/Inactive_north":
                    crop = new Rect(96f / 1060f, 64f / 1484f, 869f / 1060f, 1357f / 1484f);
                    return true;
                case "Crimson/Power/Tanaka/Active_east":
                    crop = new Rect(65f / 1484f, 93f / 1060f, 1373f / 1484f, 870f / 1060f);
                    return true;
                case "Crimson/Power/Tanaka/Inactive_east":
                    crop = new Rect(64f / 1484f, 95f / 1060f, 1357f / 1484f, 869f / 1060f);
                    return true;
                case "Crimson/Power/Tanaka/Active_south":
                    crop = new Rect(95f / 1060f, 64f / 1484f, 871f / 1060f, 1356f / 1484f);
                    return true;
                case "Crimson/Power/Tanaka/Inactive_south":
                    crop = new Rect(95f / 1060f, 63f / 1484f, 869f / 1060f, 1357f / 1484f);
                    return true;
                case "Crimson/Power/Tanaka/Active_west":
                    crop = new Rect(63f / 1484f, 95f / 1060f, 1363f / 1484f, 870f / 1060f);
                    return true;
                case "Crimson/Power/Tanaka/Inactive_west":
                    crop = new Rect(63f / 1484f, 96f / 1060f, 1357f / 1484f, 869f / 1060f);
                    return true;
                default:
                    crop = new Rect(0f, 0f, 1f, 1f);
                    return false;
            }
        }

        internal static Mesh QuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, -0.5f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            return mesh;
        }

        internal static void SetCropUv(Mesh mesh, Texture2D texture, Rect pixels)
        {
            float left = pixels.xMin / texture.width;
            float right = pixels.xMax / texture.width;
            float top = 1f - pixels.yMin / texture.height;
            float bottom = 1f - pixels.yMax / texture.height;
            mesh.uv = new[] { new Vector2(left, top), new Vector2(right, top),
                new Vector2(right, bottom), new Vector2(left, bottom) };
        }

        internal static Mesh EmptyGaugeMesh(Texture2D texture)
        {
            if (emptyGauge == null)
            {
                emptyGauge = QuadMesh();
                SetCropUv(emptyGauge, texture, new Rect(156f, 176f, 1861f, 370f));
            }
            return emptyGauge;
        }

        internal static void DrawQuad(Mesh mesh, string path, Vector3 center, Vector2 size, float angle)
        {
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(center, Quaternion.AngleAxis(angle, Vector3.up),
                new Vector3(size.x, 1f, size.y)), MaterialPool.MatFrom(path, ShaderDatabase.Transparent), 0);
        }
    }
}
