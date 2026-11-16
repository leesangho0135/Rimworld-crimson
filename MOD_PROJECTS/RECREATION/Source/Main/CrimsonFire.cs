using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CrimsonSOS2
{
    // MechaniteFire and its worker are internal SOS2 types. Their exact original
    // implementations are selected by the new XML defs, not copied/publicized.
    [StaticConstructorOnStartup]
    internal static class CrimsonEchoPatches
    {
        internal const string EchoDefName = "Crimson_Echo";
        private const string EchoSparkDefName = "Crimson_EchoSpark";
        private const string EchoBurstDefName = "Crimson_EchoBurst";
        private const string EchoWorkGiverDefName = "Crimson_FightEcho";

        internal static ThingDef EchoDef { get { return DefDatabase<ThingDef>.GetNamedSilentFail(EchoDefName); } }
        private static ThingDef NativeFireDef { get { return DefDatabase<ThingDef>.GetNamedSilentFail("MechaniteFire"); } }

        static CrimsonEchoPatches()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.echo");
            Type fireType = AccessTools.TypeByName("SaveOurShip2.MechaniteFire");
            Type workType = AccessTools.TypeByName("SaveOurShip2.WorkGiver_FightMechanites");
            harmony.Patch(AccessTools.PropertyGetter(fireType, "Label"),
                postfix: Patch("EchoLabelPostfix"));
            harmony.Patch(AccessTools.PropertyGetter(fireType, "InspectStringAddon"),
                postfix: Patch("EchoInspectPostfix"));
            harmony.Patch(AccessTools.PropertyGetter(workType, "PotentialWorkThingRequest"),
                postfix: Patch("EchoWorkRequestPostfix"));
            harmony.Patch(AccessTools.Method(typeof(AttachmentUtility), "GetAttachment", new Type[] { typeof(Thing), typeof(ThingDef) }),
                postfix: Patch("GetAttachmentPostfix"));
            harmony.Patch(AccessTools.Method(typeof(AttachmentUtility), "HasAttachment", new Type[] { typeof(Thing), typeof(ThingDef) }),
                postfix: Patch("HasAttachmentPostfix"));

            // The original damage routine is reached through ComplexFlammability,
            // which uses isinst/castclass MechaniteFire. XML's native class keeps
            // that behavior, the damage formula, timing, growth and spread veto.
            // Its destruction cascade needs a distinct, otherwise identical damage
            // def so delayed explosion cells retain which fire family to create.
            harmony.Patch(AccessTools.Method(AccessTools.TypeByName("SaveOurShip2.ComplexFlammability"), "Postfix"),
                transpiler: Patch("ComplexFireTranspiler"));
            harmony.Patch(AccessTools.Method(AccessTools.TypeByName("SaveOurShip2.DamageWorker_Mechanite"), "ExplosionAffectCell"),
                transpiler: Patch("BurstFireTranspiler"));
            harmony.Patch(AccessTools.Method(AccessTools.TypeByName("SaveOurShip2.SpreadMechanites"), "Postfix"),
                transpiler: Patch("EchoSpreadTranspiler"));
            harmony.Patch(AccessTools.Method(AccessTools.TypeByName("SaveOurShip2.Projectile_MechaniteSpark"), "Impact"),
                transpiler: Patch("EchoSparkImpactTranspiler"));

            // Two original AI paths search exactly the original Def. Preserve
            // their original result first; only an otherwise empty search may
            // find Echo with the exact same distance/reachability/validator.
            harmony.Patch(AccessTools.Method(AccessTools.TypeByName("SaveOurShip2.JobGiver_FightMechanitesNearPoint"), "TryGiveJob"),
                transpiler: Patch("EchoSearchTranspiler"));
            harmony.Patch(AccessTools.Method(AccessTools.TypeByName("SaveOurShip2.FixFireBugC"), "Postfix"),
                transpiler: Patch("EchoSearchTranspiler"));
        }

        private static HarmonyMethod Patch(string name)
        {
            return new HarmonyMethod(typeof(CrimsonEchoPatches), name);
        }

        private static bool IsEcho(Fire fire)
        {
            return fire != null && fire.def != null && fire.def.defName == EchoDefName;
        }

        private static void EchoLabelPostfix(Fire __instance, ref string __result)
        {
            if (!IsEcho(__instance)) return;
            __result = __instance.parent == null ? __instance.def.label
                : "CrimsonEchoOn".Translate(__instance.parent.LabelCap).ToString();
        }

        private static void EchoInspectPostfix(Fire __instance, ref string __result)
        {
            if (!IsEcho(__instance)) return;
            __result = string.Format("{0} ({1})", "CrimsonEchoDisassembling".Translate(),
                "CrimsonEchoSizeLower".Translate((__instance.fireSize * 100f).ToString("F0")));
        }

        private static void EchoWorkRequestPostfix(WorkGiver_Scanner __instance, ref ThingRequest __result)
        {
            if (__instance.def != null && __instance.def.defName == EchoWorkGiverDefName)
                __result = ThingRequest.ForDef(EchoDef);
        }

        private static void GetAttachmentPostfix(Thing __0, ThingDef __1, ref Thing __result)
        {
            if (__result != null || __0 == null || __1 != NativeFireDef || EchoDef == null) return;
            // The nested query is for Echo, so it cannot re-enter this fallback.
            __result = AttachmentUtility.GetAttachment(__0, EchoDef);
        }

        private static void HasAttachmentPostfix(Thing __0, ThingDef __1, ref bool __result)
        {
            if (__result || __0 == null || __1 != NativeFireDef || EchoDef == null) return;
            __result = AttachmentUtility.GetAttachment(__0, EchoDef) != null;
        }

        private static string BurstNameFor(Fire source)
        {
            return IsEcho(source) ? EchoBurstDefName : "BombMechanite";
        }

        private static string FireNameForExplosion(Explosion explosion)
        {
            return explosion != null && explosion.damType != null && explosion.damType.defName == EchoBurstDefName
                ? EchoDefName : "MechaniteFire";
        }

        private static ThingDef FireDefForSpread(ThingDef nativeDefinition, Fire source)
        {
            return IsEcho(source) ? EchoDef : nativeDefinition;
        }

        private static string SparkNameForSpread(Fire source)
        {
            return IsEcho(source) ? EchoSparkDefName : "MechaniteSpark";
        }

        private static string FireNameForSpark(Projectile source)
        {
            return source != null && source.def != null && source.def.defName == EchoSparkDefName
                ? EchoDefName : "MechaniteFire";
        }

        private static IEnumerable<CodeInstruction> EchoSpreadTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int fireLoads = 0, sparkNames = 0;
            foreach (CodeInstruction code in codes)
            {
                if (IsNativeSpreadFireLoad(code)) fireLoads++;
                if (code.opcode == OpCodes.Ldstr && Equals(code.operand, "MechaniteSpark")) sparkNames++;
            }
            if (fireLoads != 1 || sparkNames != 1)
            {
                Log.Error("[CrimsonSOS2] Echo spread expected one native fire load and one spark name; found "
                    + fireLoads + "/" + sparkNames + ". Native instructions preserved.");
                return codes;
            }
            List<CodeInstruction> result = new List<CodeInstruction>();
            foreach (CodeInstruction code in ReplaceNamedDef(codes, "MechaniteSpark", OpCodes.Ldarg_0, "SparkNameForSpread"))
            {
                result.Add(code);
                if (!IsNativeSpreadFireLoad(code)) continue;
                // Keep the native field value on the stack and preserve it for
                // every source other than Echo. Timing, random choices, line of
                // sight, fire growth and existing-fire merging remain native.
                result.Add(new CodeInstruction(OpCodes.Ldarg_0));
                result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CrimsonEchoPatches), "FireDefForSpread")));
            }
            return result;
        }

        private static bool IsNativeSpreadFireLoad(CodeInstruction instruction)
        {
            FieldInfo field = instruction.operand as FieldInfo;
            return instruction.opcode == OpCodes.Ldsfld && field != null && field.Name == "MechaniteFire"
                && field.DeclaringType.FullName == "SaveOurShip2.ResourceBank+ThingDefOf";
        }

        private static IEnumerable<CodeInstruction> EchoSparkImpactTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int names = 0;
            foreach (CodeInstruction code in codes)
                if (code.opcode == OpCodes.Ldstr && Equals(code.operand, "MechaniteFire")) names++;
            if (names != 1)
            {
                Log.Error("[CrimsonSOS2] Echo spark impact expected one native fire name; found " + names
                    + ". Native instructions preserved.");
                return codes;
            }
            return ReplaceNamedDef(codes, "MechaniteFire", OpCodes.Ldarg_0, "FireNameForSpark");
        }

        private static IEnumerable<CodeInstruction> ComplexFireTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceNamedDef(instructions, "BombMechanite", OpCodes.Ldarg_0, "BurstNameFor");
        }

        private static IEnumerable<CodeInstruction> BurstFireTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceNamedDef(instructions, "MechaniteFire", OpCodes.Ldarg_1, "FireNameForExplosion");
        }

        private static IEnumerable<CodeInstruction> ReplaceNamedDef(IEnumerable<CodeInstruction> instructions,
            string originalName, OpCode argument, string helper)
        {
            MethodInfo replacement = AccessTools.Method(typeof(CrimsonEchoPatches), helper);
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, originalName))
                {
                    CodeInstruction load = new CodeInstruction(argument);
                    load.labels.AddRange(instruction.labels);
                    load.blocks.AddRange(instruction.blocks);
                    yield return load;
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                }
                else yield return instruction;
            }
        }

        private static IEnumerable<CodeInstruction> EchoSearchTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo replacement = AccessTools.Method(typeof(CrimsonEchoPatches), "ClosestWithEcho");
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo method = instruction.operand as MethodInfo;
                if (instruction.opcode == OpCodes.Call && method != null
                    && method.DeclaringType == typeof(GenClosest) && method.Name == "ClosestThingReachable"
                    && method.GetParameters().Length == 14)
                {
                    CodeInstruction call = new CodeInstruction(OpCodes.Call, replacement);
                    call.labels.AddRange(instruction.labels);
                    call.blocks.AddRange(instruction.blocks);
                    yield return call;
                }
                else yield return instruction;
            }
        }

        private static Thing ClosestWithEcho(IntVec3 center, Map map, ThingRequest request,
            PathEndMode pathEndMode, TraverseParms traverseParms, float maxDistance,
            Predicate<Thing> validator, IEnumerable<Thing> customGlobalSearchSet,
            int searchRegionsMin, int searchRegionsMax, bool forceAllowGlobalSearch,
            RegionType traversableRegionTypes, bool ignoreEntirelyForbiddenRegions, bool lookInHaulSources)
        {
            Thing native = GenClosest.ClosestThingReachable(center, map, request, pathEndMode, traverseParms,
                maxDistance, validator, customGlobalSearchSet, searchRegionsMin, searchRegionsMax,
                forceAllowGlobalSearch, traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources);
            if (native != null || map == null || request.singleDef != NativeFireDef || EchoDef == null
                || map.listerThings.ThingsOfDef(EchoDef).Count == 0) return native;
            return GenClosest.ClosestThingReachable(center, map, ThingRequest.ForDef(EchoDef), pathEndMode,
                traverseParms, maxDistance, validator, customGlobalSearchSet, searchRegionsMin, searchRegionsMax,
                forceAllowGlobalSearch, traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources);
        }
    }

    // Same Alert_Critical base and Home+unfogged first-culprit condition as SOS2;
    // the original alert and its original Def search are not modified.
    public sealed class Alert_CrimsonEchoInHomeArea : Alert_Critical
    {
        public Alert_CrimsonEchoInHomeArea()
        {
            defaultLabel = "CrimsonEchoAlertLabel".Translate();
            defaultExplanation = "CrimsonEchoAlertDescription".Translate();
        }

        public override AlertReport GetReport()
        {
            ThingDef echo = CrimsonEchoPatches.EchoDef;
            if (echo == null) return false;
            foreach (Map map in Find.Maps)
            {
                foreach (Thing fire in map.listerThings.ThingsOfDef(echo))
                {
                    if (map.areaManager.Home[fire.Position] && !fire.Position.Fogged(map)) return AlertReport.CulpritIs(fire);
                }
            }
            return false;
        }
    }
}
