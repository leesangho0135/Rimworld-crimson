using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class Building_CrimsonBridgeConsole : Building_ShipBridge
    {
        public const string BlockerName = "Crimson_BridgeConsoleBodyCell";
        public const string ChairName = "Crimson_AuxiliaryCrewSeat";
        private static Material indicatorBody;
        private static Material indicatorLit;
        private List<Building_CrimsonBridgeConsoleBodyCell> bodyCells = new List<Building_CrimsonBridgeConsoleBodyCell>();
        private bool settleBody;

        public Vector3 SeatDrawShift
        {
            get
            {
                // Match the northward projection of the standalone auxiliary seat.
                if (def.size.x == 5 && Rotation.IsHorizontal)
                    return new Vector3(0f, 0f, .295274f);
                return def.size.x == 4
                    ? new Rot4((Rotation.AsInt + 1) % 4).FacingCell.ToVector3() * .5f : Vector3.zero;
            }
        }

        // Crew-seat rotations describe the side from which the chair is used.
        public Rot4 SeatArtRotation { get { return new Rot4((Rotation.AsInt + 2) % 4); } }

        public bool IsRecess(IntVec3 cell)
        {
            CompCrimsonBridgeDuty crew = this.GetComp<CompCrimsonBridgeDuty>();
            IntVec3 first = crew.SeatCell(0);
            if (first == cell) return true;
            if (crew.SeatCount == 2) return cell == crew.SeatCell(1);
            return def.size.x == 4 && cell == first + new Rot4((Rotation.AsInt + 1) % 4).FacingCell;
        }

        public override void Print(SectionLayer layer)
        {
            base.Print(layer);
            ThingDef chair = DefDatabase<ThingDef>.GetNamed(ChairName);
            Graphic graphic = chair.graphicData.Graphic;
            Rot4 rotation = SeatArtRotation;
            Vector3 meshSize = graphic.MeshAt(rotation).bounds.size;
            Vector2 size = new Vector2(meshSize.x, meshSize.z);
            CompCrimsonBridgeDuty crew = this.GetComp<CompCrimsonBridgeDuty>();
            for (int index = 0; index < crew.SeatCount; index++)
            {
                Vector3 center = crew.SeatCell(index).ToVector3Shifted() + SeatDrawShift + graphic.DrawOffset(rotation);
                center.y = def.altitudeLayer.AltitudeFor() + .005f;
                Printer_Plane.PrintPlane(layer, center, size, graphic.MatAt(rotation), 0f, false);
            }
            PrintPowerIndicator(layer);
        }

        private void PrintPowerIndicator(SectionLayer layer)
        {
            if (indicatorBody == null)
            {
                indicatorBody = new Material(ShaderDatabase.Cutout);
                indicatorBody.mainTexture = BaseContent.WhiteTex;
                indicatorBody.color = new Color(.12f, .026f, .029f);
                indicatorLit = new Material(ShaderDatabase.TransparentPostLight);
                indicatorLit.mainTexture = BaseContent.WhiteTex;
                indicatorLit.color = new Color(1f, .075f, .09f);
            }

            bool sideConsole = def.size.x == 5 && Rotation.IsHorizontal;
            bool frontConsole = def.size.x == 5 && Rotation == Rot4.North;
            Vector3 offset;
            if (frontConsole)
                offset = new Vector3(2.06f, 0f, -.92f);
            else if (sideConsole)
                offset = new Vector3(Rotation == Rot4.East ? .55f : -.55f, 0f, -2.36f);
            else
                offset = new Vector3(def.size.x * .35f, 0f, -.91f).RotatedBy(Rotation);
            Vector3 indicator = DrawPos + Graphic.DrawOffset(Rotation) + offset;
            indicator.y = def.altitudeLayer.AltitudeFor() + .012f;
            float angle = sideConsole ? 0f : Rotation.AsAngle;
            if (!frontConsole)
                Printer_Plane.PrintPlane(layer, indicator, new Vector2(.27f, .12f), indicatorBody, angle, false);
            CompPowerTrader power = this.TryGetComp<CompPowerTrader>();
            if (power == null || !power.PowerOn || !FlickUtility.WantsToBeOn(this)) return;
            indicator.y += .003f;
            Vector2 lightSize = frontConsole ? new Vector2(.07f, .062f) : new Vector2(.17f, .056f);
            Printer_Plane.PrintPlane(layer, indicator, lightSize, indicatorLit, angle, false);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            settleBody = true;
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned || !settleBody && !this.IsHashIntervalTick(250)) return;
            settleBody = false;
            EnsureBodyCells();
        }

        private void EnsureBodyCells()
        {
            if (bodyCells == null) bodyCells = new List<Building_CrimsonBridgeConsoleBodyCell>();
            bodyCells.RemoveAll(body => body == null || body.Destroyed || body.Console != this);
            foreach (IntVec3 cell in this.OccupiedRect())
            {
                if (IsRecess(cell)) continue;
                Building_CrimsonBridgeConsoleBodyCell existing = cell.GetEdifice(Map) as Building_CrimsonBridgeConsoleBodyCell;
                if (existing != null && existing.Console == this)
                {
                    if (!bodyCells.Contains(existing)) bodyCells.Add(existing);
                    continue;
                }
                if (cell.GetEdifice(Map) != null || !cell.Standable(Map)) continue;
                Building_CrimsonBridgeConsoleBodyCell body = bodyCells.FirstOrDefault(value => !value.Spawned);
                if (body == null)
                {
                    body = (Building_CrimsonBridgeConsoleBodyCell)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(BlockerName));
                    body.Console = this;
                    bodyCells.Add(body);
                }
                GenSpawn.Spawn(body, cell, Map, Rotation, WipeMode.VanishOrMoveAside);
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (bodyCells != null)
                foreach (Building_CrimsonBridgeConsoleBodyCell body in bodyCells)
                    if (body != null && body.Spawned) body.DeSpawn(mode);
            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (bodyCells != null)
                foreach (Building_CrimsonBridgeConsoleBodyCell body in bodyCells)
                    if (body != null && !body.Destroyed) body.Destroy(DestroyMode.Vanish);
            base.Destroy(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref bodyCells, "crimsonConsoleBodyCells", LookMode.Reference);
        }
    }

    public sealed class Building_CrimsonBridgeConsoleBodyCell : Building
    {
        public Building_CrimsonBridgeConsole Console;

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Spawned) base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref Console, "crimsonBridgeConsole");
        }
    }

    public sealed class CrimsonBridgeConsolePlaceWorker : PlaceWorker
    {
        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
            ThingDef thing = other as ThingDef;
            return other != null && (other.defName == "Crimson_Deck"
                || thing != null && thing.thingClass == typeof(Building_CrimsonBridgeConsole));
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot,
            Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            ThingDef definition = checkingDef as ThingDef;
            if (definition == null) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(loc, rot, definition.size))
            {
                if (!cell.InBounds(map) || !cell.Standable(map)) return "함교 콘솔은 걸을 수 있는 갑판 위에 설치해야 합니다.";
                foreach (Thing item in cell.GetThingList(map))
                {
                    if (item == thing || item == thingToIgnore) continue;
                    ThingDef built = item.def.entityDefToBuild as ThingDef ?? item.def;
                    if (built.defName == "Crimson_Deck" || built.building != null && built.building.isPowerConduit) continue;
                    if (built.category == ThingCategory.Building) return "함교 콘솔 설치 칸에 다른 시설이 있습니다.";
                }
            }
            return true;
        }
    }
}
