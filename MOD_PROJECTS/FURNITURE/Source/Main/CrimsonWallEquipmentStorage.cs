using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // A held container, not a floor slot group: a wall display can share its
    // mounting cell with furniture without taking over that furniture's slots.
    public sealed class CrimsonWallEquipmentStorage : Building, IHaulDestination,
        IHaulSource, IHaulEnroute, IApparelSource, ISearchableContents, IThingHolderEvents<Thing>
    {
        private CrimsonWallEquipmentOwner innerContainer;
        private StorageSettings settings;

        public CrimsonWallEquipmentStorage()
        {
            innerContainer = new CrimsonWallEquipmentOwner(this);
        }

        public bool StorageTabVisible { get { return true; } }
        public bool HaulDestinationEnabled { get { return true; } }
        public bool HaulSourceEnabled { get { return true; } }
        public bool ApparelSourceEnabled { get { return true; } }
        public ThingOwner SearchableContents { get { return innerContainer; } }
        public Thing StoredEquipment { get { return innerContainer.Count == 0 ? null : innerContainer[0]; } }
        public int EquipmentCapacity { get { return def != null && def.defName == "Crimson_WallArmorCabinet" ? 2 : 1; } }

        public ThingOwner GetDirectlyHeldThings() { return innerContainer; }
        // Native Wear jobs reserve the holder and extract the actual apparel
        // through this interface, just as they do with vanilla outfit stands.
        public bool RemoveApparel(Apparel apparel) { return innerContainer.Remove(apparel); }
        public void GetChildHolders(List<IThingHolder> holders)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(holders, innerContainer);
        }

        public StorageSettings GetStoreSettings() { return settings; }
        public StorageSettings GetParentStoreSettings() { return def.building.fixedStorageSettings; }

        internal bool AllowsEquipment(Thing item)
        {
            return item != null && !item.Destroyed && item.def.category == ThingCategory.Item
                && GetParentStoreSettings().AllowedToAccept(item)
                && settings != null && settings.AllowedToAccept(item);
        }

        public bool Accepts(Thing item)
        {
            return AllowsEquipment(item) && (innerContainer.Contains(item)
                || innerContainer.GetCountCanAccept(item) > 0);
        }

        public int SpaceRemainingFor(ThingDef itemDef)
        {
            return itemDef != null && settings != null
                && settings.AllowedToAccept(itemDef) && GetParentStoreSettings().AllowedToAccept(itemDef)
                ? Math.Max(0, EquipmentCapacity - innerContainer.TotalStackCount) : 0;
        }

        public void Notify_SettingsChanged()
        {
            if (Spawned) Map.listerHaulables.Notify_HaulSourceChanged(this);
        }
        public void Notify_ItemAdded(Thing item) { Notify_SettingsChanged(); }
        public void Notify_ItemRemoved(Thing item) { Notify_SettingsChanged(); }

        public override void PostMake()
        {
            base.PostMake();
            settings = new StorageSettings(this);
            settings.CopyFrom(def.building.defaultStorageSettings);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Deep.Look(ref settings, "settings", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (innerContainer == null) innerContainer = new CrimsonWallEquipmentOwner(this);
                if (settings == null)
                {
                    settings = new StorageSettings(this);
                    settings.CopyFrom(def.building.defaultStorageSettings);
                }
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode != DestroyMode.WillReplace)
                innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
            base.DeSpawn(mode);
        }

        public override string GetInspectString()
        {
            List<string> labels = new List<string>();
            foreach (Thing item in innerContainer) labels.Add(item.LabelCap);
            return base.GetInspectString() + "\n장비 보관: " + innerContainer.TotalStackCount + "/" + EquipmentCapacity
                + (labels.Count == 0 ? "" : " · " + string.Join(", ", labels.ToArray()));
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (Faction != Faction.OfPlayer) yield break;
            for (int index = 0; index < innerContainer.Count; index++)
            {
                Thing item = innerContainer[index];
                yield return new Command_Action {
                defaultLabel = innerContainer.TotalStackCount > 1 ? (index + 1) + ". " + item.LabelCap + " 꺼내기" : "보관 장비 꺼내기",
                defaultDesc = "선택한 보관 장비 한 개를 가까운 바닥에 내려놓습니다.",
                icon = item.def.uiIcon,
                action = delegate {
                    if (!Spawned || item.Destroyed || !innerContainer.Contains(item)) return;
                    Thing dropped;
                    innerContainer.TryDrop(item, Position, Map, ThingPlaceMode.Near, 1, out dropped);
                }
            };
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            int count = Math.Min(EquipmentCapacity, innerContainer.TotalStackCount), slot = 0;
            foreach (Thing item in innerContainer)
            {
                if (item.Destroyed) continue;
                for (int stack = 0; stack < item.stackCount && slot < count; stack++)
                    DrawStoredItem(drawLoc, item, slot++, count);
            }
        }

        private void DrawStoredItem(Vector3 drawLoc, Thing item, int slot, int count)
        {
            Graphic itemGraphic = item.Graphic;
            Vector2 source = itemGraphic.drawSize;
            Vector2 window = count > 1 ? new Vector2(.30f, .65f)
                : item.def.IsApparel ? new Vector2(.64f, .65f) : new Vector2(.76f, .68f);
            float angle = item.def.IsWeapon ? item.def.equippedAngleOffset : 0f;
            Vector2 size = FitContent(source, window, angle);
            // Use exactly the body's transform, including its reduced .55-cell
            // wall offset, then lift the item just above its own backplate.
            Matrix4x4 body = CrimsonFurnitureDepth.Transform(Graphic, drawLoc, Rotation, this, 0f);
            float offsetX = count > 1 ? (slot == 0 ? -.17f : .17f) : 0f;
            Matrix4x4 display = body * Matrix4x4.TRS(new Vector3(offsetX, .012f, -.04f),
                Quaternion.AngleAxis(angle, Vector3.up),
                new Vector3(size.x, 1f, size.y));
            Graphics.DrawMesh(MeshPool.plane10, display, itemGraphic.MatSingleFor(item), 0);
        }

        internal static Vector2 FitContent(Vector2 source, Vector2 window, float angle)
        {
            double radians = angle * Math.PI / 180d;
            float cos = (float)Math.Abs(Math.Cos(radians)), sin = (float)Math.Abs(Math.Sin(radians));
            float width = source.x * cos + source.y * sin;
            float height = source.x * sin + source.y * cos;
            float scale = Math.Min(window.x / Math.Max(.001f, width), window.y / Math.Max(.001f, height));
            return new Vector2(source.x * scale, source.y * scale);
        }
    }

    // Count actual items, not stacks. The armor cabinet holds two; the weapon
    // cabinet retains its original one-item limit, even for modded stacks.
    public sealed class CrimsonWallEquipmentOwner : ThingOwner<Thing>
    {
        public CrimsonWallEquipmentOwner(IThingHolder owner) : base(owner, false, LookMode.Deep) { }
        public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
        {
            CrimsonWallEquipmentStorage cabinet = Owner as CrimsonWallEquipmentStorage;
            if (cabinet == null || !cabinet.AllowsEquipment(item)) return 0;
            int available = cabinet.EquipmentCapacity - TotalStackCount;
            if (available <= 0) return 0;
            return Math.Min(available, base.GetCountCanAccept(item, canMergeWithExistingStacks));
        }
    }

    public sealed class CrimsonWallEquipmentPlaceWorker : PlaceWorker
    {
        private readonly PlaceWorker wallRule = new CrimsonFurnitureWallPlaceWorker();
        public override bool ForceAllowPlaceOver(BuildableDef other)
        { return wallRule.ForceAllowPlaceOver(other); }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            AcceptanceReport backing = wallRule.AllowsPlacing(checkingDef, loc, rot, map, thingToIgnore, thing);
            if (!backing.Accepted) return backing;
            foreach (Thing existing in map.thingGrid.ThingsListAtFast(loc))
            {
                if (existing == thingToIgnore || existing == thing) continue;
                ThingDef built = existing.def.entityDefToBuild as ThingDef ?? existing.def;
                if (typeof(CrimsonWallEquipmentStorage).IsAssignableFrom(built.thingClass))
                    return "SpaceAlreadyOccupied".Translate();
            }
            return true;
        }
    }

    // Select only the empty left panel from the unchanged concept sheet.
    public sealed class CrimsonWallEquipmentGraphic : Graphic_Single
    {
        public override bool ShouldDrawRotated { get { return true; } }
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            Rect crop = request.path.EndsWith("WeaponCabinet", StringComparison.Ordinal)
                ? new Rect(100f, 87f, 730f, 695f) : new Rect(49f, 37f, 801f, 795f);
            Material display = new Material(mat);
            display.mainTextureScale = new Vector2(crop.width / mat.mainTexture.width, crop.height / mat.mainTexture.height);
            display.mainTextureOffset = new Vector2(crop.x / mat.mainTexture.width, 1f - crop.yMax / mat.mainTexture.height);
            mat = display;
        }
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation); }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation))); }
        public override Graphic GetColoredVersion(Shader shader, Color first, Color second)
        { return GraphicDatabase.Get<CrimsonWallEquipmentGraphic>(path, shader, drawSize, first, second, data); }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonWallEquipmentIcons
    {
        static CrimsonWallEquipmentIcons()
        {
            foreach (string name in new[] { "Crimson_WallWeaponCabinet", "Crimson_WallArmorCabinet" })
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                Material material = def.graphicData.Graphic.MatAt(Rot4.North);
                Texture source = material.mainTexture;
                int width = Mathf.RoundToInt(source.width * material.mainTextureScale.x);
                int height = Mathf.RoundToInt(source.height * material.mainTextureScale.y);
                RenderTexture previous = RenderTexture.active;
                RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                try
                {
                    // Only an in-memory build-menu icon is cropped. The full
                    // original empty/loaded sheet on disk stays unchanged.
                    Graphics.Blit(source, target, material.mainTextureScale, material.mainTextureOffset);
                    RenderTexture.active = target;
                    Texture2D icon = new Texture2D(width, height, TextureFormat.ARGB32, false);
                    icon.name = name + "Icon";
                    icon.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    icon.Apply(false, true);
                    def.uiIcon = icon;
                    def.uiIconMaterial = null;
                }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
            }
        }
    }
}
