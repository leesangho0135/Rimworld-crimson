using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Choose an actual rear/side drawing while keeping the camera upright.
    public sealed class CrimsonWardrobeGraphic : Graphic_Multi
    {
        private Graphic inactive;
        public override bool ShouldDrawRotated { get { return false; } }
        public override Material MatSingle { get { return base.MatAt(Rot4.South); } }
        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            inactive = GraphicDatabase.Get<Graphic_Multi>(request.path + "_off", request.shader,
                request.drawSize, request.color, request.colorTwo, request.graphicData, null);
        }
        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            CompPowerTrader power = thing == null ? null : thing.TryGetComp<CompPowerTrader>();
            return power != null && !power.PowerOn ? inactive.MatAt(rot, thing) : base.MatAt(rot, thing);
        }
        public override Material MatSingleFor(Thing thing) { return MatAt(thing == null ? Rot4.South : thing.Rotation, thing); }
        public override Mesh MeshAt(Rot4 rot)
        { return MeshPool.GridPlane(rot.IsHorizontal ? new Vector2(drawSize.y, drawSize.x) : drawSize); }
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation); }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation))); }
        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        { return GraphicDatabase.Get<CrimsonWardrobeGraphic>(path, shader, drawSize, color, colorTwo, data, null); }
    }
}
