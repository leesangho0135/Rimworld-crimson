using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // This is an overhead drawing: its controls turn with the interaction cell.
    public sealed class CrimsonOrganicReconstructorGraphic : Graphic_SingleOnOff
    {
        public override bool ShouldDrawRotated { get { return true; } }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation);
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
