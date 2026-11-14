using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Closed storage keeps the native slot group, item stacks and hauling.
    // Only the world rendering of items occupying its cells is suppressed.
    [StaticConstructorOnStartup]
    internal static class CrimsonClosedCargoVisibility
    {
        private static readonly MethodInfo OverlayMethod = AccessTools.Method(
            typeof(Thing), "DrawGUIOverlay", Type.EmptyTypes);

        internal static int OverlayCallSiteCount { get; private set; }

        static CrimsonClosedCargoVisibility()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.closedcargo");
            harmony.Patch(AccessTools.Method(typeof(SectionLayer_ThingsGeneral),
                "TakePrintFrom", new Type[] { typeof(Thing) }),
                prefix: new HarmonyMethod(typeof(CrimsonClosedCargoVisibility), "BeforeStaticPrint"));
            harmony.Patch(AccessTools.Method(typeof(Thing), "DynamicDrawPhase",
                new Type[] { typeof(DrawPhase) }),
                prefix: new HarmonyMethod(typeof(CrimsonClosedCargoVisibility), "BeforeDynamicDraw"));
            harmony.Patch(AccessTools.Method(typeof(Thing), "DrawNowAt",
                new Type[] { typeof(Vector3), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(CrimsonClosedCargoVisibility), "BeforeDynamicDraw"));
            harmony.Patch(AccessTools.Method(typeof(ThingOverlays), "ThingOverlaysOnGUI", Type.EmptyTypes),
                transpiler: new HarmonyMethod(typeof(CrimsonClosedCargoVisibility), "OverlayTranspiler"));
            harmony.Patch(AccessTools.Method(typeof(Thing), "DirtyMapMesh", new Type[] { typeof(Map) }),
                postfix: new HarmonyMethod(typeof(CrimsonClosedCargoVisibility), "AfterDirtyMapMesh"));
        }

        private static bool IsClosedStorage(Thing thing)
        {
            return thing is Building_Storage && thing.def != null
                && (thing.def.defName == "Crimson_StorageCargoContainer"
                    || thing.def.defName == "Crimson_StorageCabinetSmall"
                    || thing.TryGetComp<CompCrimsonLargeShelfDisplay>() != null
                    // Refrigerator displays draw the real stacks in their
                    // shelves; suppress the original floor-level item pass.
                    || thing.TryGetComp<CompCrimsonRefrigerator>() != null);
        }

        internal static bool ShouldHide(Thing item)
        {
            if (item == null || item.def == null || item.def.category != ThingCategory.Item
                || !item.Spawned || item.Destroyed)
                return false;

            Map map = item.Map;
            IntVec3 cell = item.Position;
            if (map == null || !cell.InBounds(map)) return false;
            List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
            for (int i = 0; i < things.Count; i++)
            {
                Thing storage = things[i];
                if (IsClosedStorage(storage) && storage.Spawned && !storage.Destroyed)
                    return true;
            }
            return false;
        }

        private static bool BeforeStaticPrint(Thing t)
        {
            // The section entry point covers ordinary and minified item Print
            // overrides, including their shadows and component prints.
            return !ShouldHide(t);
        }

        private static bool BeforeDynamicDraw(Thing __instance)
        {
            // These non-virtual native entry points dispatch all drawing phases
            // to the actual item's overrides. Carried/unspawned items stay visible.
            return !ShouldHide(__instance);
        }

        private static IEnumerable<CodeInstruction> OverlayTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo replacement = AccessTools.Method(typeof(CrimsonClosedCargoVisibility), "DrawVisibleOverlay");
            List<CodeInstruction> result = new List<CodeInstruction>(instructions);
            int replacements = 0;
            foreach (CodeInstruction instruction in result)
            {
                if ((instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Call)
                    && Equals(instruction.operand, OverlayMethod))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    replacements++;
                }
            }
            OverlayCallSiteCount = replacements;
            if (replacements != 1)
                Log.Warning("Crimson closed-storage overlay hook expected one native draw call, found " + replacements + ".");
            return result;
        }

        private static void DrawVisibleOverlay(Thing thing)
        {
            // Keep the virtual dispatch, including third-party item overlays,
            // for every Thing outside the opted-in Crimson storage types.
            if (!ShouldHide(thing)) thing.DrawGUIOverlay();
        }

        private static void AfterDirtyMapMesh(Thing __instance, Map map)
        {
            if (map == null || !IsClosedStorage(__instance)) return;
            // Native DirtyMapMesh ignores RealtimeOnly furniture. Its spawn,
            // despawn and move calls still need to rebuild the item section so
            // existing stacks disappear/reappear when the closed shell changes.
            foreach (IntVec3 cell in __instance.OccupiedRect())
                if (cell.InBounds(map)) map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Things);
        }
    }
}
