using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonWitnessBodyGraphic : Graphic_Single
    {
        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        { return GraphicDatabase.Get<CrimsonWitnessBodyGraphic>(path, shader, drawSize, color, colorTwo, data, null); }
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonWitnessDepth.Draw(this, loc, rot, thing, extraRotation); }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            Matrix4x4 matrix = CrimsonWitnessDepth.Transform(this, GenThing.TrueCenter(thing), thing.Rotation, thing, extraRotation);
            CrimsonFurnitureDepth.PrintQuad(layer.GetSubMesh(MatAt(thing.Rotation, thing)), matrix, drawSize);
        }
    }

    public static class CrimsonWitnessDepth
    {
        public static Matrix4x4 Transform(Graphic graphic, Vector3 location, Rot4 rotation, Thing thing, float extraRotation)
        {
            Matrix4x4 matrix = CrimsonFurnitureDepth.Transform(graphic, location, rotation, thing, extraRotation);
            float previous = Math.Abs(matrix.m22) > .001f ? matrix.m12 / matrix.m22 : matrix.m10 / matrix.m20;
            float floorZ = thing == null ? location.z : GenThing.TrueCenter(thing).z;
            matrix.m13 += (.5f - previous) * (matrix.m23 - floorZ);
            matrix.m10 = .5f * matrix.m20;
            matrix.m11 = 1f + .5f * matrix.m21;
            matrix.m12 = .5f * matrix.m22;
            return matrix;
        }
        public static void Draw(Graphic graphic, Vector3 location, Rot4 rotation, Thing thing, float extraRotation)
        { Graphics.DrawMesh(graphic.MeshAt(rotation), Transform(graphic, location, rotation, thing, extraRotation), graphic.MatAt(rotation, thing), 0); }
    }

    public sealed class Building_CrimsonWitness : Building_ArchotechSpore
    {
        private static readonly string[] RemovedLabels = {
            "SoS.RenameAI", "SoS.HologramColor", "SoS.SpawnHologram",
            "SoS.InstallConsciousness",
            "SoS.ArchotechSoulLink", "SoS.ArchotechGiftParticles", "SoS.ArchotechEvolve"
        };
        private static readonly string[] RemovedDescriptions = {
            "SoS.RenameAIDesc", "SoS.HologramColorDesc", "SoS.SpawnHologramDesc",
            "SoS.InstallConsciousnessDesc", "SoS.ArchotechSoulLinkDesc", "SoS.ArchotechGiftParticlesDesc",
            "SoS.ArchotechEvolveDesc"
        };
        public static IEnumerable<Gizmo> Filter(ThingWithComps witness, IEnumerable<Gizmo> gizmos)
        {
            CrimsonWitnessConsciousness resident = witness.TryGetComp<CrimsonWitnessConsciousness>();
            if (resident != null) resident.EnforceName();
            foreach (Gizmo gizmo in gizmos)
            {
                Command command = gizmo as Command;
                if (command != null && (RemovedLabels.Any(key => command.defaultLabel == key.Translate().ToString())
                    || RemovedDescriptions.Any(key => command.defaultDesc == key.Translate().ToString()))) continue;
                yield return gizmo;
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class CrimsonWitnessFeatureRules
    {
        public static bool GiftBranchPatched;
        public static bool IsWitness(Thing thing)
        { return thing != null && thing.def.defName == "Crimson_Observer"; }
        static CrimsonWitnessFeatureRules()
        {
            Harmony harmony = new Harmony("rimod.crimson.witness-features");
            harmony.Patch(AccessTools.Method(typeof(Building_ArchotechSpore), "TickRare"),
                transpiler: new HarmonyMethod(typeof(CrimsonWitnessFeatureRules), "SporeTickTranspiler"));
            harmony.Patch(AccessTools.Method(typeof(Building_ArchotechSpore), "SpawnSetup"),
                transpiler: new HarmonyMethod(typeof(CrimsonWitnessFeatureRules), "SporeConstructionTranspiler"));
            foreach (string name in new[] { "MakeGift", "MeditationTick", "AbsorbMind", "doSoSCredits" })
                harmony.Patch(AccessTools.Method(typeof(Building_ArchotechSpore), name),
                    prefix: new HarmonyMethod(typeof(CrimsonWitnessFeatureRules), "AllowSporeAction"));
            harmony.Patch(AccessTools.Method(typeof(CompBuildingConsciousness), "SpawnHologram"),
                prefix: new HarmonyMethod(typeof(CrimsonWitnessFeatureRules), "AllowHologram"));
            harmony.Patch(AccessTools.Method(typeof(Building_ArchotechSpore), "GetGizmos"),
                postfix: new HarmonyMethod(typeof(CrimsonWitnessFeatureRules), "FilterGizmos"));
            harmony.Patch(AccessTools.Method(typeof(Building_ShipBridge), "UpdateUI"),
                prefix: new HarmonyMethod(typeof(CrimsonWitnessFeatureRules), "CanUpdateBridgeUI"));
        }
        public static bool CanUpdateBridgeUI(Building_ShipBridge __instance)
        { return !IsWitness(__instance) || __instance.Ship != null; }
        private static void FilterGizmos(Building_ArchotechSpore __instance, ref IEnumerable<Gizmo> __result)
        {
            // Existing saves retain the serialized native runtime type. Filter
            // that base method as well as newly built Crimson instances.
            if (IsWitness(__instance)) __result = Building_CrimsonWitness.Filter(__instance, __result);
        }
        private static bool AllowSporeAction(Building_ArchotechSpore __instance)
        { return !IsWitness(__instance); }
        private static bool AllowHologram(CompBuildingConsciousness __instance)
        { return !IsWitness(__instance.parent); }
        public static bool AllowGiftRoll(bool original, Building_ArchotechSpore spore)
        { return original && AllowSporeAction(spore); }

        private static IEnumerable<CodeInstruction> SporeTickTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            int gifts = code.FindIndex(c => c.operand is MethodInfo
                && ((MethodInfo)c.operand).Name == "get_AllDefs"
                && ((MethodInfo)c.operand).DeclaringType.FullName.Contains("ArchoGiftDef"));
            GuardBranch(code, FindGuardBefore(code, gifts), "gift");
            FieldInfo startedEndgame = AccessTools.Field(typeof(ShipWorldComp), "startedEndgame");
            int field = code.FindIndex(c => c.opcode == OpCodes.Ldfld && Equals(c.operand, startedEndgame));
            int endgame = field < 0 ? -1 : code.FindIndex(field + 1, IsFalseBranch);
            GuardBranch(code, endgame, "endgame");
            GiftBranchPatched = true;
            return code;
        }

        private static IEnumerable<CodeInstruction> SporeConstructionTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            int notice = code.FindIndex(c => c.opcode == OpCodes.Ldstr && Equals(c.operand, "SoS.SporeBuilt"));
            GuardBranch(code, FindGuardBefore(code, notice), "spore construction notice");
            return code;
        }

        private static bool IsFalseBranch(CodeInstruction instruction)
        { return instruction.opcode == OpCodes.Brfalse || instruction.opcode == OpCodes.Brfalse_S; }

        private static int FindGuardBefore(List<CodeInstruction> code, int anchor)
        {
            for (int i = anchor - 1; i >= 0; i--)
                if (IsFalseBranch(code[i])) return i;
            return -1;
        }

        private static void GuardBranch(List<CodeInstruction> code, int branch, string name)
        {
            if (branch < 0) throw new InvalidOperationException("Crimson Witness: native " + name + " branch not found");
            CodeInstruction load = new CodeInstruction(OpCodes.Ldarg_0);
            load.labels.AddRange(code[branch].labels); code[branch].labels.Clear();
            code.InsertRange(branch, new[] { load,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CrimsonWitnessFeatureRules), "AllowGiftRoll")) });
        }
    }
}
