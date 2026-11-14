using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonDeckLight : Building
    {
        internal const string SingleDefName = "Crimson_DeckLight";
        internal const string ParallelDefName = "Crimson_DeckLightParallel";

        internal static bool IsLight(ThingDef candidate)
        {
            return candidate != null && (candidate.defName == SingleDefName
                || candidate.defName == ParallelDefName);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!Spawned) return;
            bool parallel = def.defName == ParallelDefName;
            int mask = 0;
            for (int direction = 0; direction < 4; direction++)
                if (HasNeighbor(IntVec3.North.RotatedBy(new Rot4(direction)), parallel))
                    mask |= 1 << direction;
            DeckLightDrawPiece[] pieces = CrimsonDeckLightDrawLayout.Get(parallel, Rotation, mask);
            drawLoc.y = def.altitudeLayer.AltitudeFor();
            CompPowerTrader power = GetComp<CompPowerTrader>();
            CompFlickable flick = GetComp<CompFlickable>();
            bool powered = power != null && power.PowerOn && (flick == null || flick.SwitchIsOn);
            for (int i = 0; i < pieces.Length; i++)
            {
                DeckLightDrawPiece piece = pieces[i];
                Matrix4x4 matrix = piece.Transform;
                matrix.m03 += drawLoc.x;
                matrix.m13 += drawLoc.y;
                matrix.m23 += drawLoc.z;
                Graphics.DrawMesh(MeshPool.plane10, matrix, CrimsonDeckLightArt.For(piece.Sprite, powered), 0);
            }
            Comps_PostDraw();
        }

        private bool HasNeighbor(IntVec3 direction, bool parallel)
        {
            IntVec3 cell = Position + direction;
            if (!cell.InBounds(Map)) return false;
            List<Thing> things = Map.thingGrid.ThingsListAtFast(cell);
            for (int i = 0; i < things.Count; i++)
            {
                ThingDef neighbor = things[i].def.entityDefToBuild as ThingDef ?? things[i].def;
                if (neighbor == def) return true;
            }
            return false;
        }
    }

    // An overlapping building may already have a native transmitter. Keep this light as its
    // ordinary consuming connector there, and restore the rail's own transmitter
    // when that building is removed. Only instance properties are changed.
    public sealed class CrimsonDeckLightPower : CompPowerTrader
    {
        private static readonly MethodInfo cloneProperties = typeof(object).GetMethod(
            "MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        private bool nodeRegistered;

        private bool Scoped
        {
            get { return parent != null && CrimsonDeckLight.IsLight(parent.def); }
        }

        public override void Initialize(CompProperties properties)
        {
            // Clone before touching roles, independently of parent-init order.
            base.Initialize((CompProperties)cloneProperties.Invoke(properties, null));
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!Scoped) { base.PostSpawnSetup(respawningAfterLoad); return; }
            // During load the overlapping building may spawn after this light. Do not
            // register either electrical role until all map things are present.
            Props.transmitsPower = !respawningAfterLoad && SharedTransmitter() == null;
            base.PostSpawnSetup(respawningAfterLoad);
            nodeRegistered = !respawningAfterLoad;
            if (respawningAfterLoad)
            {
                LongEventHandler.ExecuteWhenFinished(RegisterFirstRole);
            }
            else if (!Props.transmitsPower)
            {
                PutBehindTransmitter();
                SetUpPowerVars();
                parent.Map.powerNetManager.Notify_ConnectorWantsConnect(this);
            }
        }

        private void RegisterFirstRole()
        {
            if (!Scoped || !parent.Spawned || nodeRegistered) return;
            // Native save data may have restored a connector parent. Detach it
            // through the public lifecycle before making the one initial node.
            PowerConnectionMaker.DisconnectFromPowerNet(this);
            Props.transmitsPower = SharedTransmitter() == null;
            if (!Props.transmitsPower) PutBehindTransmitter();
            SetUpPowerVars();
            RegisterRole();
            nodeRegistered = true;
        }

        private void RegisterRole()
        {
            if (Props.transmitsPower) parent.Map.powerNetManager.Notify_TransmitterSpawned(this);
            else parent.Map.powerNetManager.Notify_ConnectorWantsConnect(this);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!Scoped || !parent.Spawned) return;
            if (!nodeRegistered) { RegisterFirstRole(); return; }
            bool transmit = SharedTransmitter() == null;
            if (transmit == Props.transmitsPower)
            {
                if (!transmit)
                {
                    PutBehindTransmitter();
                    if (connectParent == null) parent.Map.powerNetManager.Notify_ConnectorWantsConnect(this);
                }
                return;
            }

            Map map = parent.Map;
            if (Props.transmitsPower) map.powerNetManager.Notify_TransmitterDespawned(this);
            else map.powerNetManager.Notify_ConnectorDespawned(this);
            // Retire pending old connector/transmitter actions while this
            // instance is absent from native transmitter lookup. In particular,
            // an old reconnect request must not attach the rail to itself.
            Props.transmitsPower = false;
            if (!transmit) PutBehindTransmitter();
            map.powerNetManager.UpdatePowerNetsAndConnections_First();
            Props.transmitsPower = transmit;
            SetUpPowerVars();
            RegisterRole();
        }

        internal void YieldToBuildingBeforeRegistration()
        {
            if (!Scoped || !parent.Spawned || SharedTransmitter() == null) return;
            if (nodeRegistered && Props.transmitsPower)
                parent.Map.powerNetManager.Notify_TransmitterDespawned(this);
            Props.transmitsPower = false;
            PutBehindTransmitter();
            SetUpPowerVars();
            if (nodeRegistered) parent.Map.powerNetManager.Notify_ConnectorWantsConnect(this);
            // Do not flush here: the building's pending transmitter registration
            // must follow this retirement in the SAME native update batch.
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            // Def.ConnectToPower remains false because this rail can transmit.
            // Native CompPower therefore needs this explicit consumer removal.
            if (Scoped && nodeRegistered && !Props.transmitsPower)
                map.powerNetManager.Notify_ConnectorDespawned(this);
            base.PostDeSpawn(map, mode);
            nodeRegistered = false;
        }

        private CompPower SharedTransmitter()
        {
            if (!parent.Spawned) return null;
            List<Thing> things = parent.Map.thingGrid.ThingsListAtFast(parent.Position);
            for (int i = 0; i < things.Count; i++)
            {
                Thing existing = things[i];
                if (existing == parent || CrimsonDeckLight.IsLight(existing.def) || !existing.Spawned) continue;
                CompPower power = existing.TryGetComp<CompPower>();
                if (power != null && power.TransmitsPowerNow) return power;
            }
            return null;
        }

        private void PutBehindTransmitter()
        {
            // Native GetTransmitter returns the first Def-capable building,
            // before consulting instance Props. Preserve the building-first order
            // of normal construction even if this light was loaded first.
            Map map = parent.Map;
            if (parent.Position.GetTransmitter(map) != parent) return;
            map.thingGrid.Deregister(parent, false);
            map.thingGrid.Register(parent);
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonDeckLightPowerPatches
    {
        static CrimsonDeckLightPowerPatches()
        {
            new Harmony("rimod.crimson.sos2.decklightpower").Patch(
                AccessTools.Method(typeof(CompPower), "PostSpawnSetup"),
                prefix: new HarmonyMethod(typeof(CrimsonDeckLightPowerPatches), "BeforeBuildingPowerSpawn"));
        }

        private static void BeforeBuildingPowerSpawn(CompPower __instance)
        {
            // A native transmitter can also reappear over an existing line after a
            // load or reconstruction. Retire ONLY these two CS light nodes
            // before that same-cell building queues its transmitter registration.
            if (__instance == null || __instance.parent == null || !__instance.parent.Spawned
                || CrimsonDeckLight.IsLight(__instance.parent.def) || !__instance.Props.transmitsPower) return;
            Map map = __instance.parent.Map;
            List<CrimsonDeckLightPower> affected = new List<CrimsonDeckLightPower>();
            foreach (IntVec3 cell in __instance.parent.OccupiedRect())
            {
                List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    if (!CrimsonDeckLight.IsLight(things[i].def)) continue;
                    CrimsonDeckLightPower power = things[i].TryGetComp<CrimsonDeckLightPower>();
                    if (power != null) affected.Add(power);
                }
            }
            for (int i = 0; i < affected.Count; i++) affected[i].YieldToBuildingBeforeRegistration();
        }
    }

    public sealed class CrimsonDeckLightPlaceWorker : PlaceWorker
    {
        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
            ThingDef target = other as ThingDef;
            if (target == null) return false;
            target = target.entityDefToBuild as ThingDef ?? target;
            return CrimsonDeckLightPlacement.CanShareWith(target);
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (!loc.InBounds(map)) return false;
            bool deck = false;
            List<Thing> things = map.thingGrid.ThingsListAtFast(loc);
            for (int i = 0; i < things.Count; i++)
            {
                Thing existing = things[i];
                if (existing == thingToIgnore || existing == thing) continue;
                ThingDef existingDef = existing.def;
                ThingDef builtDef = existingDef.entityDefToBuild as ThingDef ?? existingDef;
                // Monolith airlocks provide their own ship plating, so their
                // doorway does not contain a separate Crimson_Deck Thing.
                if (existingDef.defName == "Crimson_Deck"
                    || (builtDef.IsDoor && builtDef.defName == "Crimson_Airlock")) deck = true;
                if (CrimsonDeckLight.IsLight(builtDef))
                    return "Crimson_DeckLightDuplicate".Translate();
                if (CrimsonDeckLightPlacement.IsFloorLight(builtDef))
                    return "Crimson_DeckLightBlocked".Translate();
            }
            if (!deck) return "Crimson_DeckLightNeedsDeck".Translate();
            return true;
        }
    }

    // Limit underlay placement and spawn protection to these two floor rails.
    // Resolve blueprints/frames as well, so neither construction order erases
    // the existing object. Other floor lights still occupy their own slot.
    [StaticConstructorOnStartup]
    internal static class CrimsonDeckLightPlacement
    {
        static CrimsonDeckLightPlacement()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.decklight-placement");
            harmony.Patch(AccessTools.Method(typeof(GenConstruct), "CanPlaceBlueprintOver"),
                prefix: new HarmonyMethod(typeof(CrimsonDeckLightPlacement), "AllowUnderlay"));
            harmony.Patch(AccessTools.Method(typeof(GenSpawn), "SpawningWipes"),
                prefix: new HarmonyMethod(typeof(CrimsonDeckLightPlacement), "PreserveUnderlay"));
        }

        internal static bool IsFloorLight(ThingDef def)
        {
            return def != null && (CrimsonDeckLight.IsLight(def)
                || def.defName == "Crimson_FloorBeacon"
                || def.defName == "Crimson_PathGuideLight");
        }

        internal static bool CanShareWith(ThingDef def)
        {
            return def != null && def.category == ThingCategory.Building && !IsFloorLight(def);
        }

        private static ThingDef BuiltDef(BuildableDef def)
        {
            ThingDef thing = def as ThingDef;
            return thing == null ? null : thing.entityDefToBuild as ThingDef ?? thing;
        }

        private static bool SharesUnderlay(BuildableDef first, BuildableDef second)
        {
            ThingDef a = BuiltDef(first), b = BuiltDef(second);
            return (CrimsonDeckLight.IsLight(a) && CanShareWith(b))
                || (CrimsonDeckLight.IsLight(b) && CanShareWith(a));
        }

        private static bool AllowUnderlay(BuildableDef __0, ThingDef __1, ref bool __result)
        {
            if (!SharesUnderlay(__0, __1)) return true;
            __result = true;
            return false;
        }

        private static bool PreserveUnderlay(BuildableDef __0, BuildableDef __1, ref bool __result)
        {
            if (!SharesUnderlay(__0, __1)) return true;
            __result = false;
            return false;
        }
    }

    // Also registers the original rail for native icons and blueprint graphics.
    public sealed class CrimsonDeckLightGraphic : Graphic_Single
    {
        public override bool ShouldDrawRotated { get { return true; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            mat = CrimsonDeckLightArt.Register(mat, DeckLightSprite.Rail);
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef,
            Thing thing, float extraRotation)
        {
            ThingDef builtDef = thingDef == null ? null :
                thingDef.entityDefToBuild as ThingDef ?? thingDef;
            if (builtDef == null || builtDef.defName != CrimsonDeckLight.ParallelDefName)
            {
                base.DrawWorker(loc, rot, thingDef, thing, extraRotation);
                return;
            }
            // Construction ghosts retain their native blueprint tint while
            // displaying both lanes. Actual connected buildings draw above.
            int mask = 0;
            if (thing != null && thing.Spawned)
            {
                for (int direction = 0; direction < 4; direction++)
                {
                    IntVec3 cell = thing.Position + IntVec3.North.RotatedBy(new Rot4(direction));
                    if (!cell.InBounds(thing.Map)) continue;
                    List<Thing> neighbors = thing.Map.thingGrid.ThingsListAtFast(cell);
                    for (int i = 0; i < neighbors.Count; i++)
                        if ((neighbors[i].def.entityDefToBuild as ThingDef ?? neighbors[i].def) == builtDef)
                        { mask |= 1 << direction; break; }
                }
            }
            DeckLightDrawPiece[] pieces = CrimsonDeckLightDrawLayout.Get(true, Rot4.North, mask);
            for (int i = 0; i < pieces.Length; i++)
            {
                Matrix4x4 matrix = pieces[i].Transform;
                matrix.m03 += loc.x; matrix.m13 += loc.y; matrix.m23 += loc.z;
                Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
            }
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonDeckLightArt
    {
        private static readonly Material[] materials = new Material[3];
        private static readonly Material[] unlitMaterials = new Material[3];

        internal static Material For(DeckLightSprite sprite, bool powered = true)
        {
            int index = (int)sprite;
            if (materials[index] == null)
            {
                Material source = MaterialPool.MatFrom("Crimson/Decor/DeckLight" + sprite,
                    ShaderDatabase.TransparentPostLight);
                materials[index] = Register(source, sprite);
            }
            if (powered) return materials[index];
            if (unlitMaterials[index] == null)
            {
                // Preserve the physical rail and its joins while its red light
                // is off. The unlit material follows room lighting normally.
                Material unlit = new Material(materials[index]);
                unlit.shader = ShaderDatabase.Cutout;
                unlit.color = new Color(0.12f, 0.12f, 0.12f, 1f);
                unlitMaterials[index] = unlit;
            }
            return unlitMaterials[index];
        }

        internal static Material Register(Material original, DeckLightSprite sprite)
        {
            // Source PNGs are byte-identical generated assets. Only UV windows
            // are registered here; no image resizing/recoloring script is used.
            Rect pixels;
            switch (sprite)
            {
                case DeckLightSprite.Hub: pixels = new Rect(557f, 557f, 140f, 140f); break;
                case DeckLightSprite.End: pixels = new Rect(469f, 660f, 316f, 338f); break;
                default: pixels = new Rect(569f, 200f, 116f, 850f); break;
            }
            Rect uv = new Rect(pixels.x / 1254f, (1254f - pixels.y - pixels.height) / 1254f,
                pixels.width / 1254f, pixels.height / 1254f);
            Material result = new Material(original);
            result.mainTextureScale = new Vector2(uv.width, uv.height);
            result.mainTextureOffset = new Vector2(uv.x, uv.y);
            return result;
        }
    }
}
