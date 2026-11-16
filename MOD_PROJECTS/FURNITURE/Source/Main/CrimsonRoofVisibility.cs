using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // A view-only gate: tall artwork, stored contents and emission all stay
    // inside the roof view. Physical placement and the interior view are intact.
    [StaticConstructorOnStartup]
    internal static class CrimsonRoofVisibility
    {
        private static readonly List<ThingDef> targets = new List<ThingDef>();
        private static Map lastMap;
        private static bool lastRoofView;

        static CrimsonRoofVisibility()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.roof-visibility");
            HashSet<MethodInfo> methods = new HashSet<MethodInfo>();
            foreach (ThingDef definition in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (definition.category == ThingCategory.Item)
                {
                    MethodInfo overlay = DeclaredImplementation(definition.thingClass, "DrawGUIOverlay");
                    if (overlay != null && methods.Add(overlay))
                        harmony.Patch(overlay, prefix: new HarmonyMethod(typeof(CrimsonRoofVisibility), "DrawPrefix"));
                }
                if (!IsTarget(definition)) continue;
                targets.Add(definition);
                foreach (string name in new string[] { "DrawAt", "Print", "DrawGUIOverlay" })
                {
                    MethodInfo method = DeclaredImplementation(definition.thingClass, name);
                    if (method != null && methods.Add(method))
                        harmony.Patch(method, prefix: new HarmonyMethod(typeof(CrimsonRoofVisibility), "DrawPrefix"));
                }
            }
            harmony.Patch(AccessTools.Method(typeof(Thing), "DrawNowAt", new Type[] { typeof(Vector3), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(CrimsonRoofVisibility), "DrawPrefix"));
            harmony.Patch(AccessTools.Method(typeof(Thing), "DynamicDrawPhase", new Type[] { typeof(DrawPhase) }),
                prefix: new HarmonyMethod(typeof(CrimsonRoofVisibility), "DrawPrefix"));
            harmony.Patch(AccessTools.Method(typeof(SectionLayer_ThingsGeneral), "TakePrintFrom", new Type[] { typeof(Thing) }),
                prefix: new HarmonyMethod(typeof(CrimsonRoofVisibility), "StaticPrefix"));
            harmony.Patch(AccessTools.Method(typeof(RoofGrid), "SetRoof"),
                postfix: new HarmonyMethod(typeof(CrimsonRoofVisibility), "RoofChanged"));
            UnityEngine.Object.DontDestroyOnLoad(new GameObject("CrimsonRoofVisibility").AddComponent<CrimsonRoofVisibilityWatcher>());
        }

        private static MethodInfo DeclaredImplementation(Type type, string name)
        {
            MethodInfo inherited = AccessTools.Method(type, name);
            if (inherited == null) return null;
            // Harmony needs the declaring type's method, rather than the
            // reflected subclass descriptor for an inherited implementation.
            return AccessTools.DeclaredMethod(inherited.DeclaringType, name,
                Array.ConvertAll(inherited.GetParameters(), parameter => parameter.ParameterType));
        }

        internal static bool IsTarget(ThingDef definition)
        {
            if (definition == null || definition.entityDefToBuild != null || definition.category != ThingCategory.Building
                || !definition.defName.StartsWith("Crimson_", StringComparison.Ordinal)) return false;
            switch (definition.defName)
            {
                case "Crimson_StorageShelf":
                case "Crimson_StorageShelfLarge":
                case "Crimson_OpenStorageCabinetSmall":
                case "Crimson_MedicalSupplyCabinet":
                    return true;
            }
            string path = definition.graphicData == null ? "" : definition.graphicData.texPath ?? "";
            return IsWallMounted(definition)
                && (path.StartsWith("Crimson/Decor/", StringComparison.Ordinal)
                    || definition.defName.StartsWith("Crimson_Wall", StringComparison.Ordinal));
        }

        internal static bool IsWallMounted(ThingDef definition)
        {
            if (definition.building != null && definition.building.isAttachment) return true;
            if (definition.placeWorkers != null)
                foreach (Type worker in definition.placeWorkers)
                    if (worker != null && (typeof(Placeworker_AttachedToWall).IsAssignableFrom(worker)
                        || typeof(CrimsonFurnitureWallPlaceWorker).IsAssignableFrom(worker))) return true;
            return false;
        }

        internal static bool ShouldHide(Thing thing)
        {
            if (thing == null || !thing.Spawned || Current.ProgramState != ProgramState.Playing
                || Find.PlaySettings == null || !Find.PlaySettings.showRoofOverlay) return false;
            if (IsTarget(thing.def)) return Covered(thing);
            if (thing.def.category == ThingCategory.Item)
                foreach (Thing storage in thing.Position.GetThingList(thing.Map))
                    if (IsTarget(storage.def) && Covered(storage)) return true;
            return false;
        }

        private static bool Covered(Thing thing)
        {
            foreach (IntVec3 cell in thing.OccupiedRect())
            {
                if (cell.Roofed(thing.Map)) return true;
                // Wall attachments can occupy an unroofed backing wall cell.
                // Its adjacent roof still covers the room-facing artwork.
                if (!IsWallMounted(thing.def)) continue;
                foreach (IntVec3 offset in GenAdj.CardinalDirections)
                {
                    IntVec3 adjacent = cell + offset;
                    if (adjacent.InBounds(thing.Map) && adjacent.Roofed(thing.Map)) return true;
                }
            }
            return false;
        }

        private static bool DrawPrefix(Thing __instance) { return !ShouldHide(__instance); }
        private static bool StaticPrefix([HarmonyArgument(0)] Thing thing) { return !ShouldHide(thing); }

        internal static void RefreshView()
        {
            Map map = Current.ProgramState == ProgramState.Playing ? Find.CurrentMap : null;
            if (map == null) { lastMap = null; return; }
            bool roofView = Find.PlaySettings.showRoofOverlay;
            if (map == lastMap && roofView == lastRoofView) return;
            lastMap = map; lastRoofView = roofView;
            foreach (ThingDef definition in targets)
                foreach (Thing thing in map.listerThings.ThingsOfDef(definition)) Dirty(thing, map);
        }

        private static void RoofChanged([HarmonyArgument(0)] IntVec3 cell, Map ___map)
        {
            if (Current.ProgramState != ProgramState.Playing || ___map == null || ___map.thingGrid == null
                || ___map.mapDrawer == null || Find.PlaySettings == null || !Find.PlaySettings.showRoofOverlay) return;
            DirtyAt(cell, ___map);
            foreach (IntVec3 offset in GenAdj.CardinalDirections) DirtyAt(cell + offset, ___map);
        }

        private static void DirtyAt(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map)) return;
            foreach (Thing thing in cell.GetThingList(map)) if (IsTarget(thing.def)) Dirty(thing, map);
        }

        private static void Dirty(Thing thing, Map map)
        {
            thing.DirtyMapMesh(map);
            foreach (IntVec3 cell in thing.OccupiedRect())
                foreach (Thing item in cell.GetThingList(map)) if (item.def.category == ThingCategory.Item) item.DirtyMapMesh(map);
        }
    }

    // Runs while paused and holds no saved state or per-tick building logic.
    public sealed class CrimsonRoofVisibilityWatcher : MonoBehaviour
    {
        private void Update() { CrimsonRoofVisibility.RefreshView(); }
    }
}
