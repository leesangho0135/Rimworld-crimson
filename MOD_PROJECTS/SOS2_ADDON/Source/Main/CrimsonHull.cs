using System;
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
    // Only the independent Crimson deck opts into the original archotech tile
    // lifecycle and the scoped roof surface. RoofShip data remains untouched.
    public sealed class CrimsonHullExtension : DefModExtension
    {
        public bool archotechDeck;
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonHullPatches
    {
        private static Graphic roofGraphic;
        private static Graphic wallFloorGraphic;

        static CrimsonHullPatches()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.hull");
            harmony.Patch(AccessTools.Method(typeof(CompShipCachePart), "PostSpawnSetup"),
                transpiler: new HarmonyMethod(typeof(CrimsonHullPatches), "RegisterDeckTranspiler"));
            harmony.Patch(AccessTools.Method(typeof(CompShipCachePart), "DrawRoof"),
                transpiler: new HarmonyMethod(typeof(CrimsonHullPatches), "RoofGraphicTranspiler"));
            harmony.Patch(AccessTools.Method(typeof(CompShipBay), "PostDraw"),
                prefix: new HarmonyMethod(typeof(CrimsonHullPatches), "DockRoofPrefix"));
            harmony.Patch(AccessTools.Method(typeof(Thing), "Print", new Type[] { typeof(SectionLayer) }),
                prefix: new HarmonyMethod(typeof(CrimsonHullPatches), "WallFloorPrintPrefix"));
            harmony.Patch(AccessTools.PropertyGetter(typeof(CompFlickable), "CurrentGraphic"),
                prefix: new HarmonyMethod(typeof(CrimsonHullPatches), "PassiveVentGraphicPrefix"));
            harmony.Patch(AccessTools.Method(typeof(GenConstruct), "CanPlaceBlueprintOver",
                new Type[] { typeof(BuildableDef), typeof(ThingDef), typeof(ThingDef), typeof(ThingDef) }),
                transpiler: new HarmonyMethod(typeof(CrimsonHullPatches), "PassiveVentPlacementTranspiler"));
        }

        private static void WallFloorPrintPrefix(Thing __instance, SectionLayer layer)
        {
            // ThingWithComps.Print has already honored CompsPreventDrawing.
            // Keep the saved native Building class, linked wall atlas and all
            // SOS2 support-terrain/ship-network lifecycle behavior unchanged.
            if (__instance == null || !__instance.Spawned || layer == null ||
                __instance.def == null || __instance.def.dontPrint ||
                (__instance.def.defName != "Crimson_HullWall" &&
                 __instance.def.defName != "Crimson_HullWall_Unpowered"))
                return;
            TerrainDef terrain = __instance.Map.terrainGrid.TerrainAt(__instance.Position);
            if (terrain == null || terrain.defName != "FakeFloorInsideShipArchotech")
                return;

            // A full opaque cell hides the native archotech texture through
            // this wall's transparent atlas corners. It is a render plane,
            // not a terrain replacement or an additional spawned floor Thing.
            if (wallFloorGraphic == null)
                wallFloorGraphic = GraphicDatabase.Get<Graphic_Single>("Crimson/Hull/DeckSeamless",
                    ShaderDatabase.Cutout, Vector2.one, Color.white);
            Vector3 floorLocation = __instance.TrueCenter();
            floorLocation.y = AltitudeLayer.FloorEmplacement.AltitudeFor();
            Printer_Plane.PrintPlane(layer, floorLocation, Vector2.one, wallFloorGraphic.MatSingle);
        }

        private static bool PassiveVentGraphicPrefix(CompFlickable __instance, ref Graphic __result)
        {
            // Saved vents retain their native Building_Vent class. Select the
            // linked frame by Def so existing and newly built vents both work.
            if (__instance == null || __instance.parent == null ||
                __instance.parent.def == null || __instance.parent.def.defName != "Crimson_PassiveVent")
                return true;
            __result = __instance.parent.DefaultGraphic;
            return false;
        }

        private static bool PassiveVentWallFlag(bool original, BuildableDef newDef, ThingDef oldDef)
        {
            // Preserve all originally replaceable walls, including vanilla ones.
            if (original) return true;
            ThingDef vent = newDef as ThingDef;
            if (vent == null || vent.defName != "Crimson_PassiveVent" || oldDef == null)
                return false;

            // Native solar panels also have isHull and linked wall graphics.
            // Limit the extension to the installed ordinary wall families.
            switch (oldDef.defName)
            {
                case "Ship_Beam":
                case "Ship_Beam_Unpowered":
                case "Ship_BeamMech":
                case "Ship_BeamMech_Unpowered":
                case "Ship_BeamArchotech":
                case "Ship_BeamArchotech_Unpowered":
                case "Ship_Beam_Wrecked":
                case "Crimson_HullWall":
                case "Crimson_HullWall_Unpowered":
                    break;
                default:
                    return false;
            }

            if (oldDef.thingClass != typeof(Building) || oldDef.IsDoor ||
                oldDef.passability != Traversability.Impassable || oldDef.size.x != 1 || oldDef.size.z != 1 ||
                oldDef.graphicData == null || oldDef.graphicData.linkType != LinkDrawerType.Basic ||
                (oldDef.graphicData.linkFlags & LinkFlags.Wall) == 0 || oldDef.comps == null)
                return false;
            foreach (CompProperties properties in oldDef.comps)
            {
                CompProps_ShipCachePart part = properties as CompProps_ShipCachePart;
                if (part != null && part.isHull && !part.isPlating && !part.isHardpoint)
                    return true;
            }
            return false;
        }

        private static IEnumerable<CodeInstruction> PassiveVentPlacementTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            FieldInfo wallFlag = AccessTools.Field(typeof(BuildingProperties), "isPlaceOverableWall");
            FieldInfo building = AccessTools.Field(typeof(ThingDef), "building");
            MethodInfo smoothed = AccessTools.PropertyGetter(typeof(ThingDef), "IsSmoothed");
            int matches = 0;
            int matchIndex = -1;
            for (int i = 2; i + 3 < codes.Count; i++)
            {
                // Installed native IL: normalized old ThingDef is local2.
                // Replace only its wall flag result; retain the smoothed-wall,
                // new canPlaceOverWall and every other native branch.
                if (codes[i - 2].opcode == OpCodes.Ldloc_2 &&
                    codes[i - 1].opcode == OpCodes.Ldfld && Equals(codes[i - 1].operand, building) &&
                    codes[i].opcode == OpCodes.Ldfld && Equals(codes[i].operand, wallFlag) &&
                    (codes[i + 1].opcode == OpCodes.Brtrue || codes[i + 1].opcode == OpCodes.Brtrue_S) &&
                    codes[i + 2].opcode == OpCodes.Ldloc_2 &&
                    codes[i + 3].opcode == OpCodes.Callvirt && Equals(codes[i + 3].operand, smoothed))
                {
                    matches++;
                    matchIndex = i;
                }
            }
            if (matches != 1)
            {
                Log.Error("[CrimsonSOS2] Passive vent placement expected one native wall-flag pattern; found " + matches + ". Native instructions preserved.");
                return codes;
            }
            codes.InsertRange(matchIndex + 1, new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_2),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(CrimsonHullPatches), "PassiveVentWallFlag"))
            });
            return codes;
        }

        private static bool IsCrimsonDeck(ThingDef definition)
        {
            CrimsonHullExtension extension = definition == null ? null : definition.GetModExtension<CrimsonHullExtension>();
            return extension != null && extension.archotechDeck;
        }

        private static bool DeckEquals(ThingDef candidate, ThingDef originalArchotechDeck)
        {
            return candidate == originalArchotechDeck || IsCrimsonDeck(candidate);
        }

        private static IEnumerable<CodeInstruction> RegisterDeckTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int changed = 0;
            for (int i = 0; i + 1 < codes.Count; i++)
            {
                FieldInfo field = codes[i].operand as FieldInfo;
                if (codes[i].opcode == OpCodes.Ldsfld && field != null &&
                    field.Name == "ShipHullTileArchotech" &&
                    field.DeclaringType.FullName == "SaveOurShip2.ResourceBank+ThingDefOf" &&
                    codes[i + 1].opcode == OpCodes.Ceq)
                {
                    // Keep both original operands. Only extend this exact
                    // equality to the opt-in Crimson deck; no Def substitution.
                    CodeInstruction equality = codes[i + 1];
                    equality.opcode = OpCodes.Call;
                    equality.operand = AccessTools.Method(typeof(CrimsonHullPatches), "DeckEquals");
                    changed++;
                }
            }
            if (changed != 1)
                Log.Error("[CrimsonSOS2] Hull tile registration expected one archotech equality; found " + changed + ".");
            return codes;
        }

        private static Graphic SelectRoofGraphic(Graphic original, CompShipCachePart part)
        {
            if (part == null || part.parent == null || !IsCrimsonDeck(part.parent.def))
                return original;
            if (roofGraphic == null)
                roofGraphic = new CrimsonRoofGraphic(original);
            return roofGraphic;
        }

        private static bool IsCrimsonRoofHiddenDock(Thing thing)
        {
            return thing != null && thing.def != null &&
                (thing.def.defName == "Crimson_ShuttleDock" ||
                 thing.def.defName == "Crimson_ShuttleDockLarge" ||
                 thing.def.defName == "Crimson_SalvageDock" ||
                 thing.def.defName == "Crimson_SalvageDockAdvanced");
        }

        private static bool DockRoofPrefix(CompShipBay __instance)
        {
            // The floor graphic already sits below the hull roof. Suppress only
            // the bay's separately drawn roof cover; no landing/salvage changes.
            Thing parent = __instance.parent;
            return !(IsCrimsonRoofHiddenDock(parent) && parent.Spawned &&
                Find.PlaySettings.showRoofOverlay && parent.Position.Roofed(parent.Map));
        }

        private static CompShipHeat RoofOpeningHeat(CompShipHeat original)
        {
            // This result is used only by DrawRoof's local hole test. Keep the
            // real heat comp and showOnRoof flag intact for turret drawing/firing.
            if (original != null && original.parent is Building_ShipTurret &&
                original.parent.def.defName.StartsWith("Crimson_", StringComparison.Ordinal) &&
                original.Props.showOnRoof && Find.PlaySettings.showRoofOverlay)
                return null;
            return original;
        }

        private static CompShipBay RoofOpeningBay(CompShipBay original)
        {
            return original != null && IsCrimsonRoofHiddenDock(original.parent) &&
                Find.PlaySettings.showRoofOverlay ? null : original;
        }

        private static IEnumerable<CodeInstruction> RoofGraphicTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int graphics = 0, heat = 0, bays = 0;
            // Resolve every expected native anchor before modifying any IL.
            foreach (CodeInstruction code in codes)
            {
                FieldInfo field = code.operand as FieldInfo;
                if (code.opcode == OpCodes.Ldsfld && field != null &&
                    field.DeclaringType == typeof(CompShipCachePart) && field.Name == "roofedGraphicTileMech")
                    graphics++;
                MethodInfo method = code.operand as MethodInfo;
                if (code.opcode == OpCodes.Call && method != null && method.IsGenericMethod && method.Name == "TryGetComp")
                {
                    if (method.ReturnType == typeof(CompShipHeat)) heat++;
                    if (method.ReturnType == typeof(CompShipBay)) bays++;
                }
            }
            if (graphics != 1 || heat != 1 || bays != 1)
            {
                Log.Error("[CrimsonSOS2] Scoped roof expected one graphic/heat/bay anchor; found " +
                    graphics + "/" + heat + "/" + bays + ". Native instructions preserved.");
                return codes;
            }
            List<CodeInstruction> result = new List<CodeInstruction>();
            foreach (CodeInstruction code in codes)
            {
                result.Add(code);
                FieldInfo field = code.operand as FieldInfo;
                if (code.opcode == OpCodes.Ldsfld && field != null &&
                    field.DeclaringType == typeof(CompShipCachePart) && field.Name == "roofedGraphicTileMech")
                {
                    result.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    result.Add(new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(CrimsonHullPatches), "SelectRoofGraphic")));
                }
                MethodInfo method = code.operand as MethodInfo;
                if (code.opcode != OpCodes.Call || method == null || !method.IsGenericMethod || method.Name != "TryGetComp")
                    continue;
                string helper = method.ReturnType == typeof(CompShipHeat) ? "RoofOpeningHeat" :
                    method.ReturnType == typeof(CompShipBay) ? "RoofOpeningBay" : null;
                if (helper != null)
                    result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CrimsonHullPatches), helper)));
            }
            return result;
        }
    }

    // The existing Graphic_256 keeps its room/world 16x16 sampling behavior.
    // Registration removes the authored transparent outer canvas in material
    // UVs; the copied PNG itself is never cropped or repainted.
    public sealed class CrimsonRoofGraphic : Graphic_Single
    {
        private readonly Graphic nativeGraphic;
        private readonly Dictionary<Material, Material> registered = new Dictionary<Material, Material>();

        public CrimsonRoofGraphic(Graphic original)
        {
            // Graphic_256 is internal. Use its existing public Graphic interface
            // for world UV selection; never inherit it or change its shared data.
            nativeGraphic = original;
            path = "Crimson/Hull/HullRoofPanel";
            drawSize = original.drawSize;
            color = Color.white;
        }

        private Material Registered(Material original)
        {
            Material result;
            if (!registered.TryGetValue(original, out result))
            {
                result = CrimsonHullGraphics.CropMaterial(original,
                    new Rect(20f / 1254f, 18f / 1254f, 1216f / 1254f, 1217f / 1254f));
                result.mainTexture = CrimsonSurfacePalette.Surface(ContentFinder<Texture2D>.Get(path), path);
                result.color = Color.white;
                // Native lighting is an overlay above the ship roof plane.
                // Keep Cutout depth/alpha behavior, but render this independent
                // roof after that overlay so cabin light cannot tint its skin.
                result.renderQueue = ShaderDatabase.TransparentPostLight.renderQueue;
                registered.Add(original, result);
            }
            return result;
        }

        public override Material MatSingle { get { return Registered(nativeGraphic.MatSingle); } }
        public override Material MatSingleFor(Thing thing) { return Registered(nativeGraphic.MatSingleFor(thing)); }
        public override Material MatAt(Rot4 rot, Thing thing = null) { return Registered(nativeGraphic.MatAt(rot, thing)); }
    }

    public sealed class CrimsonDeckGraphic : Graphic_Single
    {
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            mat = CrimsonHullGraphics.CropMaterial(mat,
                new Rect(48f / 1254f, 49f / 1254f, 1158f / 1254f, 1155f / 1254f));
        }
    }

    // Register each unchanged source outline into its original 1xn footprint.
    // Rectangles are Unity lower-left UVs converted from the measured alpha
    // bounds once. No sprite reflection, added rotation or geometry scale.
    public sealed class CrimsonSlopeGraphic : Graphic_Single
    {
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            Rect crop;
            if (TryGetCrop(request.path, out crop))
                mat = CrimsonHullGraphics.CropMaterial(mat, crop);
        }

        private static bool TryGetCrop(string texturePath, out Rect crop)
        {
            switch (texturePath)
            {
                case "Crimson/Hull/Slope1x1":
                    crop = new Rect(40f / 1254f, 37f / 1254f, 1178f / 1254f, 1177f / 1254f);
                    return true;
                case "Crimson/Hull/Slope1x1Flip":
                    crop = new Rect(37f / 1254f, 40f / 1254f, 1180f / 1254f, 1173f / 1254f);
                    return true;
                case "Crimson/Hull/Slope1x2":
                    crop = new Rect(37f / 887f, 47f / 1774f, 811f / 887f, 1681f / 1774f);
                    return true;
                case "Crimson/Hull/Slope1x2Flip":
                    crop = new Rect(31f / 887f, 49f / 1774f, 849f / 887f, 1677f / 1774f);
                    return true;
                case "Crimson/Hull/Slope1x3":
                    crop = new Rect(8f / 724f, 15f / 2172f, 711f / 724f, 2144f / 2172f);
                    return true;
                case "Crimson/Hull/Slope1x3Flip":
                    crop = new Rect(80f / 724f, 256f / 2172f, 607f / 724f, 1786f / 2172f);
                    return true;
                default:
                    crop = new Rect(0f, 0f, 1f, 1f);
                    return false;
            }
        }
    }

    public sealed class CompProperties_CrimsonVentOverlay : CompProperties
    {
        public string texPath = "Crimson/Hull/VentInset";
        public Vector2 drawSize = Vector2.one;

        public CompProperties_CrimsonVentOverlay()
        {
            compClass = typeof(CompCrimsonVentOverlay);
        }
    }

    // Native linked graphics render the hull frame and register link flags.
    // This foreground is independent of Building_Vent's temperature/flick logic.
    public sealed class CompCrimsonVentOverlay : ThingComp
    {
        private Graphic overlay;

        public CompProperties_CrimsonVentOverlay Props
        {
            get { return (CompProperties_CrimsonVentOverlay)props; }
        }

        public override void PostDraw()
        {
            if (parent == null || !parent.Spawned || string.IsNullOrEmpty(Props.texPath)) return;
            if (overlay == null)
                overlay = GraphicDatabase.Get<Graphic_Single>(Props.texPath,
                    ShaderDatabase.Cutout, Props.drawSize, Color.white);
            overlay.Draw(parent.DrawPos + new Vector3(0f, 0.01f, 0f), parent.Rotation, parent);
        }
    }

    internal static class CrimsonHullGraphics
    {
        private static Mesh doorLeft;
        private static Mesh doorRight;
        private static Mesh tube;
        private static Mesh solar;

        internal static Material CropMaterial(Material original, Rect uv)
        {
            Material result = new Material(original);
            Vector2 cropScale = new Vector2(uv.width, uv.height);
            result.mainTextureScale = Vector2.Scale(original.mainTextureScale, cropScale);
            result.mainTextureOffset = new Vector2(uv.x, uv.y) +
                Vector2.Scale(original.mainTextureOffset, cropScale);
            return result;
        }

        internal static Mesh Door(bool flip)
        {
            if (flip && doorRight != null) return doorRight;
            if (!flip && doorLeft != null) return doorLeft;
            Mesh result = RegisteredQuad(
                new Rect(6f / 1254f, 3f / 1254f, 649f / 1254f, 1245f / 1254f),
                new Rect(0f, 0f, 34f / 64f, 1f), flip);
            if (flip) doorRight = result; else doorLeft = result;
            return result;
        }

        internal static Mesh Unfold(bool isSolar)
        {
            if (isSolar)
            {
                if (solar == null)
                    solar = RegisteredQuad(
                        new Rect(0.0925414365f, 0.102670350f, 0.814917127f, 0.755985267f),
                        new Rect(0.09375f, 0f, 0.796875f, 0.869791667f), false);
                return solar;
            }
            if (tube == null)
                tube = RegisteredQuad(
                    new Rect(151f / 1254f, 0f, 952f / 1254f, 1f),
                    new Rect(19f / 256f, 0f, 218f / 256f, 1f), false);
            return tube;
        }

        // Source/target registration uses top-left image coordinates. Convert
        // exactly once to Unity's lower-left UV and the original unit plane.
        private static Mesh RegisteredQuad(Rect source, Rect target, bool flip)
        {
            float left = target.x;
            float right = target.xMax;
            float uLeft = source.x;
            float uRight = source.xMax;
            if (flip)
            {
                left = 1f - target.xMax;
                right = 1f - target.x;
                uLeft = source.xMax;
                uRight = source.x;
            }
            float bottom = 0.5f - target.yMax;
            float top = 0.5f - target.y;
            Mesh mesh = new Mesh();
            mesh.name = "Crimson registered hull layer";
            mesh.vertices = new Vector3[] {
                new Vector3(left - 0.5f, 0f, bottom), new Vector3(left - 0.5f, 0f, top),
                new Vector3(right - 0.5f, 0f, top), new Vector3(right - 0.5f, 0f, bottom)
            };
            mesh.uv = new Vector2[] {
                new Vector2(uLeft, 1f - source.yMax), new Vector2(uLeft, 1f - source.y),
                new Vector2(uRight, 1f - source.y), new Vector2(uRight, 1f - source.yMax)
            };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // All door opening, sealing, docking, power and save logic remains inherited.
    // This substitutes only the registered moving-panel meshes in the vanilla
    // DrawMovers layout (the original uses plane10/plane10Flip directly).
    [StaticConstructorOnStartup]
    public sealed class CrimsonAirlock : Building_ShipAirlock
    {
        private static Graphic floorGraphic;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            DoorPreDraw();
            // Keep SOS2's actual support terrain and ship identity. Cover only
            // this airlock cell visually, below its moving panels and pawns.
            if (floorGraphic == null)
                floorGraphic = GraphicDatabase.Get<Graphic_Single>("Crimson/Hull/DeckSeamless",
                    ShaderDatabase.Cutout, Vector2.one, Color.white);
            Vector3 floorLocation = drawLoc;
            floorLocation.y = AltitudeLayer.FloorEmplacement.AltitudeFor();
            floorGraphic.Draw(floorLocation, Rotation, this);
            if (CanDrawMovers)
            {
                float offset = 0.45f * OpenPct;
                Rot4 transverse = Rotation;
                transverse.Rotate(RotationDirection.Clockwise);
                for (int half = 0; half < 2; half++)
                {
                    Vector3 direction = new Vector3(0f, 0f, half == 0 ? -def.size.x : def.size.x);
                    Vector3 position = drawLoc + transverse.AsQuat * direction * offset;
                    position.y = AltitudeLayer.DoorMoveable.AltitudeFor();
                    Matrix4x4 transform = Matrix4x4.TRS(position, Rotation.AsQuat,
                        new Vector3(def.size.x, 1f, def.size.z));
                    Graphics.DrawMesh(CrimsonHullGraphics.Door(half == 1), transform,
                        Graphic.MatAt(Rotation, this), 0);
                }
            }
            Comps_PostDraw();
        }
    }

    public sealed class CrimsonUnfoldProperties : CompProps_Unfold
    {
        public bool isSolar;
        public CrimsonUnfoldProperties()
        {
            compClass = typeof(CrimsonUnfold);
        }
    }

    // Inherit extension/retraction state, ticking and serialization. Match the
    // original PostDraw matrix and alternate-path rule with registered UVs.
    public sealed class CrimsonUnfold : CompUnfold
    {
        public override void PostDraw()
        {
            if (extension == 0f) return;
            CrimsonUnfoldProperties properties = (CrimsonUnfoldProperties)props;
            Rot4 rotation = parent.Rotation;
            Building_ShipAirlock airlock = parent as Building_ShipAirlock;
            if (airlock != null)
            {
                if (airlock.firstRot == -1) return;
                rotation = new Rot4(airlock.firstRot);
            }
            Vector3 direction = properties.extendDirection.RotatedBy(rotation).ToVector3();
            Vector3 position = parent.DrawPos + direction * properties.startOffset +
                direction * properties.length * 0.5f * extension + Altitudes.AltIncVect;
            Matrix4x4 transform = Matrix4x4.TRS(position, rotation.AsQuat,
                new Vector3(properties.width, 1f, properties.length * extension));
            bool alternate = properties.width != 1f && (rotation == Rot4.North || rotation == Rot4.South);
            Material material = alternate ? properties.unfoldGraphicAlt : properties.unfoldGraphic;
            Graphics.DrawMesh(CrimsonHullGraphics.Unfold(properties.isSolar), transform, material, 0);
        }
    }
}

