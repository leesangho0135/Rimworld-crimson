using System.Collections.Generic;
using HarmonyLib;
using PipeSystem;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    internal static class CrimsonHydroponicsGrowth
    {
        internal const float SpeedMultiplier = 2.5f;

        static CrimsonHydroponicsGrowth()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.hydroponics-growth");
            harmony.Patch(AccessTools.Method(typeof(Process), "ResetProcess",
                new[] { typeof(bool) }), postfix: new HarmonyMethod(
                    typeof(CrimsonHydroponicsGrowth), "KeepAcceleratedReset"));
            harmony.Patch(AccessTools.Method(typeof(ProcessDef.Result), "GetCount",
                new[] { typeof(Process) }), postfix: new HarmonyMethod(
                    typeof(CrimsonHydroponicsGrowth), "ScaleCropHarvest"));
        }

        private static void ScaleCropHarvest(Process __0, ref int __result)
        {
            CompCrimsonAutomaticHydroponics comp = __0 == null ? null
                : __0.advancedProcessor as CompCrimsonAutomaticHydroponics;
            if (comp == null) return;
            CompProperties_CrimsonAutomaticHydroponics visual =
                (CompProperties_CrimsonAutomaticHydroponics)comp.props;
            __result *= visual.plantsPerPod;
        }

        private static void KeepAcceleratedReset(Process __instance, bool finished)
        {
            if (finished || !(__instance.advancedProcessor
                is CompCrimsonAutomaticHydroponics)) return;
            // Native failed/cancelled resets restore the raw cached duration,
            // before its overclock division. Keep the same 40% duration here;
            // successful cycles already reuse the accelerated native duration.
            __instance.tickLeft = (int)(__instance.tickLeft / SpeedMultiplier);
        }
    }

    // Loaded only with Automatic Hydroponics. Production, power checks, bills,
    // extraction, network output and saving remain the native VEF processor.
    public sealed class CompProperties_CrimsonAutomaticHydroponics
        : CompProperties_AdvancedResourceProcessor
    {
        public string nativeDefName;
        public int cropColumns = 2;
        public int cropRows = 2;
        public bool skipCenter;
        public float cropSpacing = 0.7f;
        public float cropDrawScale = 0.55f;
        public Vector3 cropDrawOffset = Vector3.zero;
        public int plantsPerPod = 1;
        public float plantClusterSpacing = 0.13f;
        public List<Vector3> cropPositionsNorth;
        public List<Vector3> cropPositionsEast;
        public List<Vector3> cropPositionsSouth;
        public List<Vector3> cropPositionsWest;
        private Vector3[][] cachedPlantPositions;

        public IEnumerable<Vector3> CropPositions(Rot4 rotation)
        {
            List<Vector3> measured = rotation == Rot4.North ? cropPositionsNorth
                : rotation == Rot4.East ? cropPositionsEast
                : rotation == Rot4.South ? cropPositionsSouth : cropPositionsWest;
            if (measured != null && measured.Count > 0)
            {
                foreach (Vector3 position in measured) yield return position;
                yield break;
            }
            for (int row = 0; row < cropRows; row++)
                for (int column = 0; column < cropColumns; column++)
                {
                    if (skipCenter && column == cropColumns / 2 && row == cropRows / 2) continue;
                    Vector3 local = new Vector3((column - (cropColumns - 1) * 0.5f) * cropSpacing,
                        0f, (row - (cropRows - 1) * 0.5f) * cropSpacing);
                    yield return cropDrawOffset + Quaternion.AngleAxis(rotation.AsAngle, Vector3.up) * local;
                }
        }

        public IEnumerable<Vector3> PlantPositions(Rot4 rotation)
        {
            Vector3[] positions = CachedPlantPositions(rotation);
            for (int i = 0; i < positions.Length; i++) yield return positions[i];
        }

        // Geometry belongs to the definition, not to growth progress or power.
        // Keep the public enumerable API while the per-frame renderer uses the array directly.
        internal Vector3[] CachedPlantPositions(Rot4 rotation)
        {
            int direction = rotation.AsInt;
            if (direction < 0 || direction > 3) return BuildPlantPositions(rotation);
            if (cachedPlantPositions == null) cachedPlantPositions = new Vector3[4][];
            if (cachedPlantPositions[direction] == null)
                cachedPlantPositions[direction] = BuildPlantPositions(rotation);
            return cachedPlantPositions[direction];
        }

        private Vector3[] BuildPlantPositions(Rot4 rotation)
        {
            List<Vector3> positions = new List<Vector3>();
            foreach (Vector3 pod in CropPositions(rotation))
                for (int plant = 0; plant < plantsPerPod; plant++)
                {
                    Vector3 offset = Vector3.zero;
                    if (plantsPerPod == 4)
                        offset = new Vector3((plant % 2 == 0 ? -1f : 1f) * plantClusterSpacing,
                            0f, (plant / 2 == 0 ? -1f : 1f) * plantClusterSpacing);
                    else if (plantsPerPod == 3)
                        offset = plant == 2 ? new Vector3(0f, 0f, plantClusterSpacing)
                            : new Vector3((plant == 0 ? -1f : 1f) * plantClusterSpacing, 0f, -plantClusterSpacing);
                    positions.Add(pod + offset);
                }
            return positions.ToArray();
        }

        public CompProperties_CrimsonAutomaticHydroponics()
        {
            compClass = typeof(CompCrimsonAutomaticHydroponics);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            cachedPlantPositions = null;
            base.ResolveReferences(parentDef);
            // XML patches from other plant mods may extend the original bay.
            // Read its final list into this new definition without changing it.
            ThingDef native = DefDatabase<ThingDef>.GetNamedSilentFail(nativeDefName);
            CompProperties_AdvancedResourceProcessor nativeProperties = native == null
                ? null : native.GetCompProperties<CompProperties_AdvancedResourceProcessor>();
            if (nativeProperties != null && nativeProperties.processes != null)
                processes = new List<ProcessDef>(nativeProperties.processes);
        }
    }

    public sealed class CompCrimsonAutomaticHydroponics : CompAdvancedResourceProcessor
    {
        private static readonly Dictionary<ThingDef, ThingDef> PlantForOutput
            = new Dictionary<ThingDef, ThingDef>();
        private static readonly Dictionary<Material, Material> StationaryCropMaterials
            = new Dictionary<Material, Material>();

        private CompProperties_CrimsonAutomaticHydroponics VisualProps
        {
            get { return (CompProperties_CrimsonAutomaticHydroponics)props; }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            float previousMultiplier = overclockMultiplier;
            if (previousMultiplier == CrimsonHydroponicsGrowth.SpeedMultiplier)
                return;

            // Use VEF's per-instance duration multiplier, never edit shared
            // ProcessDefs or scale Tick's argument (which also ages spoilage).
            overclockMultiplier = CrimsonHydroponicsGrowth.SpeedMultiplier;
            if (previousMultiplier <= 0f) previousMultiplier = 1f;
            if (ProcessStack == null) return;
            foreach (Process process in ProcessStack.Processes)
                process.Notify_OverclockChange(previousMultiplier);
            // VEF saves overclockMultiplier and process durations itself.
            // Subsequent loads return above and do not accelerate them again.
        }

        public override void PostDraw()
        {
            // Empty native texture fields disable only the original combined
            // machine-and-crop sprite. The native progress bar still draws.
            base.PostDraw();
            Process current = Process;
            if (current == null || current.Progress <= 0f || current.Progress >= 1f)
                return;

            ProcessDef processDef = current.Def;
            if (processDef == null || processDef.results == null
                || processDef.results.Count == 0 || processDef.results[0] == null)
                return;
            // Read the declared visual result. GetOutput dispatches a virtual
            // production worker and lazily creates it; rendering must not call
            // that path or consume a custom worker's random output roll.
            ThingDef output = processDef.results[0].thing;
            if (output == null) return;

            ThingDef plant = FindPlant(output);
            Material material = SafeCropMaterial(plant);
            if (material == null)
            {
                // Unknown mod graphics may require an actual Plant instance.
                // A declared output's UI texture is a safe visual fallback.
                plant = null;
                if (output.uiIcon == null) return;
                material = MaterialPool.MatFrom(output.uiIcon,
                    ShaderDatabase.Cutout, Color.white);
            }
            if (material == null) return;

            CompProperties_CrimsonAutomaticHydroponics visual = VisualProps;
            float growthScale = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(current.Progress));
            float size = visual.cropDrawScale * (plant == null ? 0.65f : growthScale);
            Vector3 origin = parent.DrawPos;
            origin.y += 0.075f;
            Vector3[] positions = visual.CachedPlantPositions(parent.Rotation);
            for (int i = 0; i < positions.Length; i++)
            {
                    Vector3 position = origin + positions[i];
                    // These are visual crop quads, never spawned plants or a
                    // second growing simulation. The native process is sole owner.
                    Graphics.DrawMesh(MeshPool.plane10,
                        Matrix4x4.TRS(position, Quaternion.identity,
                            new Vector3(size, 1f, size)), material, 0);
            }
        }

        private Material SafeCropMaterial(ThingDef plant)
        {
            if (plant == null || plant.graphicData == null) return null;
            System.Type declared = plant.graphicData.graphicClass;
            if (declared != typeof(Graphic_Random) && declared != typeof(Graphic_Multi)
                && declared != typeof(Graphic_Single)) return null;
            Graphic graphic = plant.graphicData.Graphic;
            if (graphic == null) return null;
            System.Type actual = graphic.GetType();
            if (actual != typeof(Graphic_Random) && actual != typeof(Graphic_Multi)
                && actual != typeof(Graphic_Single)) return null;
            // Core Random uses the building's stable thingIDNumber, not Rand.
            // Multi/Single ignore the supplied Thing and perform no Plant cast.
            // Random.MatSingle would consume Rand, so keep MatSingleFor here.
            Material original = graphic.MatSingleFor(parent);
            if (original == null) return null;
            Material stationary;
            if (!StationaryCropMaterials.TryGetValue(original, out stationary))
            {
                // Outdoor plant shaders bend vertices using the wind clock.
                // Enclosed crops keep the same texture/UV/color without wind.
                stationary = new Material(original);
                stationary.shader = ShaderDatabase.Cutout;
                StationaryCropMaterials.Add(original, stationary);
            }
            return stationary;
        }

        private static ThingDef FindPlant(ThingDef output)
        {
            ThingDef found;
            if (PlantForOutput.TryGetValue(output, out found)) return found;
            // Wood has many producing trees; this bay's native wood crop is
            // fibercorn. Prefer it before considering other harvestable plants.
            if (output.defName == "WoodLog")
                found = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Fibercorn");
            if (found == null)
            {
                List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
                for (int i = 0; i < defs.Count; i++)
                {
                    ThingDef candidate = defs[i];
                    if (candidate.plant != null && candidate.graphicData != null
                        && candidate.plant.harvestedThingDef == output)
                    {
                        found = candidate;
                        break;
                    }
                }
            }
            PlantForOutput[output] = found;
            return found;
        }
    }
}
