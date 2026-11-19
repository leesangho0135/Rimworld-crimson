using System.Collections.Generic;
using System.Linq;
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
        public override Material MatAt(Rot4 rot, Thing thing = null)
        { return base.MatAt(new Rot4(CrimsonFurnitureDirections.Source(path, rot.AsInt)), thing); }
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

    public sealed class CrimsonIntegratedThroneFurnishings : DefModExtension
    {
        public List<ThingDefCountClass> furnishings = new List<ThingDefCountClass>();
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonRoyaltyFurniture
    {
        private static readonly Dictionary<ThingDef, List<ThingDef>> equivalents =
            new Dictionary<ThingDef, List<ThingDef>>();
        private static readonly MethodInfo countAnyOfThing =
            AccessTools.Method(typeof(RoomRequirement_ThingAnyOfCount), "CountThing");
        private static readonly HashSet<RoomRequirement> throneRoomRequirements = new HashSet<RoomRequirement>();
        private static readonly Dictionary<RoomRequirement_AllThingsAnyOfAreGlowing, int> minimumGlowingCounts =
            new Dictionary<RoomRequirement_AllThingsAnyOfAreGlowing, int>();
        private static readonly ThingDef integratedThrone;
        private static readonly CrimsonIntegratedThroneFurnishings integratedFurnishings;

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
            integratedThrone = DefDatabase<ThingDef>.GetNamedSilentFail("Crimson_GrandThrone");
            if (integratedThrone != null)
                integratedFurnishings = integratedThrone.GetModExtension<CrimsonIntegratedThroneFurnishings>();
            if (integratedFurnishings != null) RegisterThroneRoomRequirements();
            if (equivalents.Count == 0 && integratedFurnishings == null) return;

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

        private static void RegisterThroneRoomRequirements()
        {
            foreach (RoyalTitleDef title in DefDatabase<RoyalTitleDef>.AllDefsListForReading)
            {
                List<RoomRequirement> requirements = title.throneRoomRequirements;
                if (requirements == null) continue;
                throneRoomRequirements.UnionWith(requirements);
                foreach (RoomRequirement_AllThingsAnyOfAreGlowing glowing in
                    requirements.OfType<RoomRequirement_AllThingsAnyOfAreGlowing>())
                {
                    HashSet<ThingDef> definitions = new HashSet<ThingDef>(glowing.things);
                    foreach (RoomRequirement_ThingAnyOfCount count in requirements.OfType<RoomRequirement_ThingAnyOfCount>())
                    {
                        if (count.GetType() != typeof(RoomRequirement_ThingAnyOfCount)
                            || !definitions.SetEquals(count.things)) continue;
                        int minimum;
                        minimumGlowingCounts.TryGetValue(glowing, out minimum);
                        minimumGlowingCounts[glowing] = System.Math.Max(minimum, count.count);
                    }
                }
            }
        }

        private static IEnumerable<Thing> BuiltInFurnishingThrones(RoomRequirement requirement, Room room)
        {
            if (integratedFurnishings == null || !throneRoomRequirements.Contains(requirement)
                || room == null || !room.ProperRoom) yield break;
            foreach (Thing throne in room.ContainedThings(integratedThrone))
                if (throne.Spawned && throne.Position.GetRoom(throne.Map) == room) yield return throne;
        }

        private static int BuiltInCount(RoomRequirement requirement, Room room, IEnumerable<ThingDef> requested)
        {
            if (!BuiltInFurnishingThrones(requirement, room).Any()) return 0;
            return integratedFurnishings.furnishings.Where(item => requested.Contains(item.thingDef))
                .Sum(item => item.count);
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
            __result = BuiltInCount(__instance, __0, new[] { __instance.thingDef }) > 0;
        }

        private static void ThingCountPostfix(RoomRequirement_ThingCount __instance,
            Room __0, ref int __result)
        {
            if (__0 == null) return;
            foreach (ThingDef replacement in ExtraDefinitions(new[] { __instance.thingDef }))
                __result += __0.ThingCount(replacement);
            __result += BuiltInCount(__instance, __0, new[] { __instance.thingDef });
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
            if (__instance.GetType() == typeof(RoomRequirement_ThingAnyOf))
                __result = BuiltInCount(__instance, __0, __instance.things) > 0;
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
            if (__instance.GetType() == typeof(RoomRequirement_ThingAnyOfCount))
                __result += BuiltInCount(__instance, __0, __instance.things);
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

            int minimum;
            if (!minimumGlowingCounts.TryGetValue(__instance, out minimum)) return;
            int integratedCount = BuiltInCount(__instance, __0, __instance.things);
            if (integratedCount == 0) return;
            IEnumerable<ThingDef> physical = __instance.things.Union(ExtraDefinitions(__instance.things));
            if (physical.Sum(def => __0.ThingCount(def)) >= minimum) return;
            __result = BuiltInFurnishingThrones(__instance, __0).Any(throne =>
            {
                CompGlower glower = throne.TryGetComp<CompGlower>();
                return glower != null && glower.Glows;
            });
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
