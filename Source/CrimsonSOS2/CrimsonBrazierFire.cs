using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Keep native fuel, heat, sound, particles and FireSize. Only this brazier's
    // flickering flame material changes; the shared vanilla graphic is untouched.
    [StaticConstructorOnStartup]
    public sealed class CompCrimsonBrazierFire : CompFireOverlay
    {
        private static readonly Graphic crimsonFire = GraphicDatabase.Get<Graphic_Flicker>(
            "Things/Special/Fire", ShaderDatabase.TransparentPostLight, Vector2.one,
            new Color(0.78f, 0.10f, 0.08f, 1f));

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Spawned && parent.IsHashIntervalTick(60))
            {
                // SOS2 atmosphere changes can change HasFuel without a refuel
                // signal. Keep native light/royal-room state in sync with it.
                CompGlower glower = parent.GetComp<CompGlower>();
                if (glower != null) glower.UpdateLit(parent.Map);
            }
        }

        public override void PostDraw()
        {
            CompRefuelable fuel = parent.GetComp<CompRefuelable>();
            if (fuel != null && !fuel.HasFuel) return;
            Vector3 position = parent.DrawPos;
            position.y = Mathf.Max(position.y + 0.03658537f, CrimsonFurnitureDepth.OverlayAltitude(parent));
            crimsonFire.Draw(position, parent.Rotation, parent, 0f);
        }
    }
}
