using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CompProperties_CrimsonRefrigerator : CompProperties
    {
        public float storageTemperature = -5f;
        public bool displayContents;
        public CompProperties_CrimsonRefrigerator() { compClass = typeof(CompCrimsonRefrigerator); }
    }

    // Storage, hauling, stack limits, eating and rot progress remain native.
    // Only items actually occupying this refrigerator's slot group are cooled.
    public sealed class CompCrimsonRefrigerator : CompCrimsonFurnitureVisuals
    {
        private bool WallDisplay { get { return parent.def.defName == "Crimson_MealDisplayRefrigerator"; } }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (WallDisplay) RefreshWallLinks(parent.Map);
        }
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            if (WallDisplay && map != null) RefreshWallLinks(map);
        }
        private void RefreshWallLinks(Map map)
        {
            map.linkGrid.Notify_LinkerCreatedOrDestroyed(parent);
            foreach (IntVec3 cell in parent.OccupiedRect().ExpandedBy(1))
                if (cell.InBounds(map)) map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Buildings, true, false);
        }
        internal CompProperties_CrimsonRefrigerator Settings
        { get { return (CompProperties_CrimsonRefrigerator)props; } }
        internal bool Cooling
        {
            get
            {
                CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
                CompFlickable flick = parent.TryGetComp<CompFlickable>();
                return parent.Spawned && !parent.Destroyed && power != null
                    && power.PowerOn && power.PowerNet != null && (flick == null || flick.SwitchIsOn);
            }
        }

        public override string CompInspectStringExtra()
        {
            return Cooling ? "냉장 보관 온도: " + Math.Min(parent.AmbientTemperature, Settings.storageTemperature).ToString("0.#") + "°C"
                : "냉장 정지: 주변 온도로 보관 중";
        }

        public override void PostDraw()
        {
            if (!Settings.displayContents || !parent.Spawned) return;
            Building_Storage storage = parent as Building_Storage;
            if (storage == null || storage.GetSlotGroup() == null) return;
            List<Thing> items = storage.GetSlotGroup().HeldThings
                .Where(t => t.Spawned && !t.Destroyed && t.def.category == ThingCategory.Item)
                .OrderBy(t => t.Position.x).ThenBy(t => t.Position.z).ThenBy(t => t.thingIDNumber).ToList();
            bool wide = !parent.Rotation.IsHorizontal;
            int count = Math.Min(items.Count, 6);
            Vector3 origin=parent.DrawPos;
            CrimsonV51FurnitureGraphic graphic=parent.Graphic as CrimsonV51FurnitureGraphic;
            if(graphic!=null)origin.z+=graphic.FrameOffset(parent.Rotation);
            bool wall = WallDisplay;
            Matrix4x4 body = wall ? Matrix4x4.TRS(origin, parent.Rotation.AsQuat, Vector3.one)
                : CrimsonFurnitureDepth.Transform(parent.Graphic, origin, parent.Rotation, parent, 0f);
            int[] rows = new int[2];
            Vector3 center = GenThing.TrueCenter(parent);
            for (int index = 0; index < count; index++)
            {
                Thing item = items[index];
                int lane = wide ? (item.Position.x < center.x ? 0 : 1)
                    : (item.Position.z < center.z ? 0 : 1);
                int row = rows[lane]++;
                if (row >= 3) continue;
                Vector3 local = new Vector3((lane == 0 ? -1f : 1f) * (wide ? 0.43f : 0.16f),
                    0.003f, wide ? 0.50f - row * 0.22f : 0.60f - row * 0.45f);
                if (wall) local = new Vector3((lane == 0 ? -1f : 1f) * .43f, .015f, .23f - row * .23f);
                // Follow the cabinet surface's depth gradient so every pixel
                // of the real food icon stays above its own compartment floor.
                Matrix4x4 matrix = body * Matrix4x4.TRS(local, Quaternion.identity,
                    wall ? new Vector3(.48f, 1f, .17f)
                        : new Vector3(wide ? 0.39f : 0.20f, 1f, wide ? 0.16f : 0.30f));
                // Native source material reflects the real stack's type,
                // quality and count. No decorative food is baked into the atlas.
                Graphics.DrawMesh(MeshPool.plane10, matrix, item.Graphic.MatSingleFor(item), 0);
            }
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonRefrigeratorTemperature
    {
        static CrimsonRefrigeratorTemperature()
        {
            new Harmony("rimod.crimson.sos2.refrigerators").Patch(
                AccessTools.PropertyGetter(typeof(Thing), "AmbientTemperature"),
                postfix: new HarmonyMethod(typeof(CrimsonRefrigeratorTemperature), "StoredTemperature"));
        }

        private static void StoredTemperature(Thing __instance, ref float __result)
        {
            if (__instance == null || !__instance.Spawned || __instance.Destroyed
                || __instance.def.category != ThingCategory.Item) return;
            SlotGroup group = __instance.Position.GetSlotGroup(__instance.Map);
            ThingWithComps storage = group == null ? null : group.parent as ThingWithComps;
            CompCrimsonRefrigerator refrigerator = storage == null ? null
                : storage.TryGetComp<CompCrimsonRefrigerator>();
            if (refrigerator != null && refrigerator.Cooling)
                __result = Math.Min(__result, refrigerator.Settings.storageTemperature);
        }
    }
}
