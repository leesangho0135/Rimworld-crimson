using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // One fixed overhead image; only the native interaction cell rotates.
    public sealed class CrimsonOrganicReconstructorGraphic : Graphic_SingleOnOff
    {
        public override bool ShouldDrawRotated { get { return false; } }

        public override Mesh MeshAt(Rot4 rot) { return MeshPool.GridPlane(drawSize); }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Draw(this, loc, Rot4.North, def, thing, 0f);
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation)));
        }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonOrganicReconstructorGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }
    }
}
