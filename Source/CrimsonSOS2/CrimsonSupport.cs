using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // The original OnOff graphic keeps power/fuel/flick signals and _off names.
    // Only this three-cell wall attachment rotates its single selected artwork.
    public sealed class CrimsonWallStateGraphic : Graphic_SingleOnOff
    {
        public override bool ShouldDrawRotated { get { return true; } }
    }

    public sealed class CrimsonLifeSupportWallPlaceWorker : Placeworker_AttachedToWall
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            AcceptanceReport original = base.AllowsPlacing(checkingDef, loc, rot, map, thingToIgnore, thing);
            if (!original.Accepted) return original;
            ThingDef buildingDef = checkingDef as ThingDef;
            if (buildingDef == null) return false;
            IntVec3 wallDirection = IntVec3.North.RotatedBy(rot);
            // Unlike a one-cell lamp, the complete three-cell module needs backing.
            foreach (IntVec3 cell in GenAdj.OccupiedRect(loc, rot, buildingDef.size))
            {
                IntVec3 wallCell = cell + wallDirection;
                if (!wallCell.InBounds(map)) return "MustPlaceOnWall".Translate();
                Building wall = wallCell.GetEdifice(map);
                if (wall == null || wall.def.building == null
                    || !wall.def.building.supportsWallAttachments || wall.def.Fillage != FillCategory.Full)
                    return "MustPlaceOnWall".Translate();
            }
            return true;
        }
    }

    // Registration comes from design-v15-monolith-registration.json. The source
    // PNGs are unchanged: body 971x1620 -> logical 971x1619, eye draw rectangle
    // (3.4478435, 5.0239050, 971, 1535.8746239) on that same logical canvas.
    public sealed class CrimsonObserverEyeGraphic : Graphic_Single
    {
        public override Graphic GetColoredVersion(Shader shader, Color originalMoodColor, Color colorTwo)
        {
            // SOS2's original DrawAt still computes Mood (0..2) and its original
            // cosine alpha, and still suppresses the eye without consciousness.
            // Preserve that feedback in intensity, without tinting red art blue.
            float mood = originalMoodColor.b > 0f ? 1f + originalMoodColor.b : originalMoodColor.g;
            float intensity = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(mood / 2f));
            Color tint = new Color(intensity, intensity, intensity, originalMoodColor.a);
            return GraphicDatabase.Get<CrimsonObserverEyeGraphic>(path, shader, drawSize, tint, tint);
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            const float logicalWidth = 971f;
            const float logicalHeight = 1619f;
            float x = 3.44784353059174f / logicalWidth * 3f;
            float z = (0.5f - (5.023905048478753f + 1535.874623871615f / 2f) / logicalHeight) * 5f;
            if (thingDef != null && thingDef.graphicData != null)
            {
                x += thingDef.graphicData.drawOffset.x;
                z += thingDef.graphicData.drawOffset.z;
            }
            base.DrawWorker(loc + new Vector3(x, 0f, z), rot, thingDef, thing, extraRotation);
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonSupportPatches
    {
        private const string ObserverDefName = "Crimson_Observer";
        private static readonly FieldInfo OriginalEyeField = AccessTools.Field(typeof(Building_ArchotechSpore), "eyeGraphic");
        private static readonly Graphic OriginalEye = (Graphic)OriginalEyeField.GetValue(null);
        private static readonly Graphic ObserverEye = GraphicDatabase.Get<CrimsonObserverEyeGraphic>(
            "Crimson/Support/ObserverEye", ShaderDatabase.MetaOverlay,
            new Vector2(3f, 1535.874623871615f / 1619f * 5f), Color.white, Color.white);

        static CrimsonSupportPatches()
        {
            // Do not swap SOS2's shared static field, even transiently. Only the
            // chosen Crimson instance receives the registered replacement eye.
            Harmony harmony = new Harmony("rimod.crimson.sos2.support");
            harmony.Patch(AccessTools.Method(typeof(Building_ArchotechSpore), "DrawAt"),
                transpiler: new HarmonyMethod(typeof(CrimsonSupportPatches), "ObserverDrawTranspiler"));
        }

        private static Graphic EyeFor(Building_ArchotechSpore spore)
        {
            return spore.def.defName == ObserverDefName ? ObserverEye : OriginalEye;
        }

        private static IEnumerable<CodeInstruction> ObserverDrawTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo replacement = AccessTools.Method(typeof(CrimsonSupportPatches), "EyeFor");
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldsfld && Equals(instruction.operand, OriginalEyeField))
                {
                    CodeInstruction instance = new CodeInstruction(OpCodes.Ldarg_0);
                    instance.labels.AddRange(instruction.labels);
                    instance.blocks.AddRange(instruction.blocks);
                    yield return instance;
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                }
                else yield return instruction;
            }
        }
    }
}
