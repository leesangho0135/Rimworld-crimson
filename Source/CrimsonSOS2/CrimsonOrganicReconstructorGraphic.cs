using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Building rotation belongs to the interaction cell. Artwork remains in a
    // fixed overhead orientation for live drawing, map printing and ghosts.
    public sealed class CrimsonOrganicReconstructorGraphic : Graphic_SingleOnOff
    {
        public override bool ShouldDrawRotated { get { return false; } }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Draw(this, loc, Rot4.North, def, thing, 0f);
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(Rot4.North)));
        }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonOrganicReconstructorGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }
    }
}
