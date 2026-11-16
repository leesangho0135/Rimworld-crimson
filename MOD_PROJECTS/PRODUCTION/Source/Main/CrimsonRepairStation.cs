using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonRepairStation : Building_Storage
    {
        internal const int Capacity = 4;
        internal const int RepairInterval = 250;
        internal const double DailyFraction = .25;
        private Dictionary<Thing, double> fractions = new Dictionary<Thing, double>();
        private List<Thing> fractionKeys;
        private List<double> fractionValues;

        internal static double RepairProgress(int maximum, int ticks)
        { return maximum * DailyFraction * ticks / 60000d; }

        internal static bool Eligible(Thing item)
        {
            return item != null && !item.Destroyed && item.def.useHitPoints
                && (item.def.IsWeapon || item.def.IsApparel);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref fractions, "crimsonRepairFractions", LookMode.Reference, LookMode.Value,
                ref fractionKeys, ref fractionValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && fractions == null)
                fractions = new Dictionary<Thing, double>();
        }

        public override void TickRare()
        {
            base.TickRare();
            List<Thing> items = GetSlotGroup().HeldThings.Where(Eligible).Take(Capacity).ToList();
            foreach (Thing old in fractions.Keys.Where(t => !items.Contains(t) || t.HitPoints >= t.MaxHitPoints).ToList())
                fractions.Remove(old);
            CompPowerTrader power = this.TryGetComp<CompPowerTrader>();
            if (power == null || !power.PowerOn) return;
            foreach (Thing item in items)
            {
                if (item.HitPoints >= item.MaxHitPoints) continue;
                double progress;
                fractions.TryGetValue(item, out progress);
                progress += RepairProgress(item.MaxHitPoints, RepairInterval);
                int amount = (int)Math.Floor(progress + 1e-9d);
                item.HitPoints = Math.Min(item.MaxHitPoints, item.HitPoints + amount);
                if (item.HitPoints >= item.MaxHitPoints) fractions.Remove(item);
                else fractions[item] = Math.Max(0d, progress - amount);
            }
        }

        public override string GetInspectString()
        {
            int stored = GetSlotGroup().HeldThings.Count(Eligible);
            bool powered = this.TryGetComp<CompPowerTrader>() != null && this.TryGetComp<CompPowerTrader>().PowerOn;
            return base.GetInspectString() + "\n장비 " + stored + "/" + Capacity + " · "
                + (powered ? "각 장비 최대 내구도의 25%/일 수리" : "전원 없음: 수리 정지");
        }
    }

    // Four complete independently drawn views, with the same wall-occlusion
    // depth transform used by the established furniture collection.
    public sealed class CrimsonRepairStationGraphic : Graphic_Single
    {
        private readonly Material[] views = new Material[4];
        private static readonly Rect[] Windows = {
            new Rect(77,98,485,450), new Rect(697,97,482,451),
            new Rect(73,726,488,450), new Rect(701,726,484,450) };
        public override bool ShouldDrawRotated { get { return false; } }
        public override Material MatSingle { get { return views[2] ?? mat; } }
        public override Material MatNorth { get { return views[0] ?? mat; } }
        public override Material MatEast { get { return views[1] ?? mat; } }
        public override Material MatSouth { get { return views[2] ?? mat; } }
        public override Material MatWest { get { return views[3] ?? mat; } }
        public override Material MatAt(Rot4 rot, Thing thing = null) { return views[rot.AsInt] ?? mat; }
        public override Material MatSingleFor(Thing thing) { return thing == null ? MatSingle : MatAt(thing.Rotation, thing); }
        public override Mesh MeshAt(Rot4 rot)
        { return MeshPool.GridPlane(rot.IsHorizontal ? new Vector2(drawSize.y, drawSize.x) : drawSize); }
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            for (int i = 0; i < views.Length; i++)
            {
                Rect r = Windows[i];
                Material view = new Material(mat);
                view.mainTextureScale = new Vector2(r.width / mat.mainTexture.width, r.height / mat.mainTexture.height);
                view.mainTextureOffset = new Vector2(r.x / mat.mainTexture.width, 1f - r.yMax / mat.mainTexture.height);
                views[i] = view;
            }
        }
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation); }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation))); }
        public override Graphic GetColoredVersion(Shader shader, Color first, Color second)
        { return GraphicDatabase.Get<CrimsonRepairStationGraphic>(path, shader, drawSize, first, second, data); }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonRepairStationIcon
    {
        static CrimsonRepairStationIcon()
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Crimson_RepairStation");
            if (def == null) return;
            Material view = def.graphicData.Graphic.MatAt(Rot4.South);
            Texture source = view.mainTexture;
            int width = Mathf.RoundToInt(source.width * view.mainTextureScale.x);
            int height = Mathf.RoundToInt(source.height * view.mainTextureScale.y);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(source, target, view.mainTextureScale, view.mainTextureOffset);
                RenderTexture.active = target;
                Texture2D icon = new Texture2D(width, height, TextureFormat.ARGB32, false);
                icon.name = "CrimsonRepairStationIcon";
                icon.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                icon.Apply(false, true);
                def.uiIcon = icon;
                def.uiIconMaterial = null;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
    }
}
