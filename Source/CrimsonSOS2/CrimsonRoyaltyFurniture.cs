using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Stuff and paint still determine the native costs and stats. Normal
    // furniture retains its artwork palette; blueprint/ghost shaders and tints
    // must still be passed through to their own cached graphic instance.
    public sealed class CrimsonRoyaltyMultiGraphic : Graphic_Multi
    {
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation); }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation)));
            if (ShadowGraphic != null) ShadowGraphic.Print(layer, thing, extraRotation);
        }
        public override Graphic GetColoredVersion(Shader shader, Color color,
            Color colorTwo)
        {
            if (shader == ShaderDatabase.Cutout || shader == ShaderDatabase.CutoutComplex)
            {
                color = Color.white;
                colorTwo = Color.white;
            }
            return GraphicDatabase.Get<CrimsonRoyaltyMultiGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }
    }

    public sealed class CrimsonRoyaltySingleGraphic : Graphic_Single
    {
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation); }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation)));
            if (ShadowGraphic != null) ShadowGraphic.Print(layer, thing, extraRotation);
        }
        public override Graphic GetColoredVersion(Shader shader, Color color,
            Color colorTwo)
        {
            if (shader == ShaderDatabase.Cutout || shader == ShaderDatabase.CutoutComplex)
            {
                color = Color.white;
                colorTwo = Color.white;
            }
            return GraphicDatabase.Get<CrimsonRoyaltySingleGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonRoyaltyFurniture
    {
        private static readonly Dictionary<ThingDef, List<ThingDef>> equivalents =
            new Dictionary<ThingDef, List<ThingDef>>();
        private static readonly MethodInfo countAnyOfThing =
            AccessTools.Method(typeof(RoomRequirement_ThingAnyOfCount), "CountThing");

        static CrimsonRoyaltyFurniture()
        {
            if (!ModsConfig.RoyaltyActive) return;
            foreach (string name in new[] { "Throne", "GrandThrone", "Brazier",
                "Drape", "Harp", "Harpsichord", "Piano", "Column" })
            {
                AddEquivalent(name, "Crimson_" + name);
            }
            AddEquivalent("RoyalBed", "Crimson_CrewBed");
            AddEquivalent("RoyalBed", "Crimson_CrewDoubleBed");
            AddEquivalent("EndTable", "Crimson_BedsideTable");
            AddEquivalent("Dresser", "Crimson_Wardrobe");
            if (equivalents.Count == 0) return;

            Harmony harmony = new Harmony("rimod.crimson.sos2.royalty-furniture");
            Patch(harmony, typeof(RoomRequirement_Thing), "Met", "ThingMetPostfix");
            Patch(harmony, typeof(RoomRequirement_ThingCount), "Count", "ThingCountPostfix");
            Patch(harmony, typeof(RoomRequirement_ThingAnyOf), "Met", "AnyOfMetPostfix");
            Patch(harmony, typeof(RoomRequirement_ThingAnyOfCount), "ThingCount", "AnyOfCountPostfix");
            Patch(harmony, typeof(RoomRequirement_AllThingsAnyOfAreGlowing), "Met", "AllGlowingPostfix");
            Patch(harmony, typeof(RoomRequirement_HasAssignedThroneAnyOf), "Met", "AssignedThronePostfix");
        }

        private static void Patch(Harmony harmony, System.Type type, string method,
            string postfix)
        {
            harmony.Patch(AccessTools.Method(type, method), postfix:
                new HarmonyMethod(typeof(CrimsonRoyaltyFurniture), postfix));
        }

        private static void AddEquivalent(string originalName, string replacementName)
        {
            ThingDef original = DefDatabase<ThingDef>.GetNamedSilentFail(originalName);
            ThingDef replacement = DefDatabase<ThingDef>.GetNamedSilentFail(replacementName);
            if (original == null || replacement == null) return;
            List<ThingDef> replacements;
            if (!equivalents.TryGetValue(original, out replacements))
            {
                replacements = new List<ThingDef>();
                equivalents.Add(original, replacements);
            }
            if (!replacements.Contains(replacement)) replacements.Add(replacement);
        }

        // Only explicit Crimson replacements participate. Original ThingDefs
        // and shared RoyalTitleDef requirements remain unchanged.
        private static IEnumerable<ThingDef> ExtraDefinitions(IEnumerable<ThingDef> originals)
        {
            if (originals == null) yield break;
            HashSet<ThingDef> requested = new HashSet<ThingDef>(originals);
            HashSet<ThingDef> emitted = new HashSet<ThingDef>(requested);
            foreach (ThingDef original in requested)
            {
                List<ThingDef> replacements;
                if (original == null || !equivalents.TryGetValue(original, out replacements)) continue;
                foreach (ThingDef replacement in replacements)
                    if (emitted.Add(replacement)) yield return replacement;
            }
        }

        private static void ThingMetPostfix(RoomRequirement_Thing __instance,
            Room __0, ref bool __result)
        {
            if (__result || __0 == null) return;
            foreach (ThingDef replacement in ExtraDefinitions(new[] { __instance.thingDef }))
            {
                if (!__0.ContainsThing(replacement)) continue;
                __result = true;
                return;
            }
        }

        private static void ThingCountPostfix(RoomRequirement_ThingCount __instance,
            Room __0, ref int __result)
        {
            if (__0 == null) return;
            foreach (ThingDef replacement in ExtraDefinitions(new[] { __instance.thingDef }))
                __result += __0.ThingCount(replacement);
        }

        private static void AnyOfMetPostfix(RoomRequirement_ThingAnyOf __instance,
            Room __0, ref bool __result)
        {
            if (__result || __0 == null) return;
            foreach (ThingDef crimson in ExtraDefinitions(__instance.things))
            {
                if (!__0.ContainsThing(crimson)) continue;
                __result = true;
                return;
            }
        }

        private static void AnyOfCountPostfix(RoomRequirement_ThingAnyOfCount __instance,
            Room __0, ref int __result)
        {
            if (__0 == null) return;
            foreach (ThingDef crimson in ExtraDefinitions(__instance.things))
            {
                foreach (Thing thing in __0.ContainedThings(crimson))
                {
                    // Preserve native subclass filters (for example quality)
                    // rather than treating every mapped object as countable.
                    if ((bool)countAnyOfThing.Invoke(__instance, new object[] { thing }))
                        __result++;
                }
            }
        }

        private static void AllGlowingPostfix(RoomRequirement_AllThingsAnyOfAreGlowing __instance,
            Room __0, ref bool __result)
        {
            if (!__result || __0 == null) return;
            foreach (ThingDef crimson in ExtraDefinitions(__instance.things))
            {
                foreach (Thing thing in __0.ContainedThings(crimson))
                {
                    CompGlower glower = thing.TryGetComp<CompGlower>();
                    if (glower != null && glower.Glows) continue;
                    __result = false;
                    return;
                }
            }
        }

        private static void AssignedThronePostfix(RoomRequirement_HasAssignedThroneAnyOf __instance,
            Room __0, Pawn __1, ref bool __result)
        {
            if (__result || __0 == null || !__0.ProperRoom || __1 == null
                || __1.ownership == null) return;
            Building_Throne throne = __1.ownership.AssignedThrone;
            if (throne == null || !__0.ContainedAndAdjacentThings.Contains(throne)) return;
            foreach (ThingDef crimson in ExtraDefinitions(__instance.things))
            {
                if (throne.def != crimson) continue;
                __result = true;
                return;
            }
        }
    }
}
