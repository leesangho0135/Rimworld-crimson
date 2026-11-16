using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    [StaticConstructorOnStartup]
    public sealed class CrimsonPulseProjectile : Bullet
    {
        private static Material glow, core;
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (glow == null)
            {
                glow = new Material(ShaderDatabase.TransparentPostLight);
                glow.mainTexture = BaseContent.WhiteTex;
                glow.color = new Color(1f, .025f, .10f, .65f);
                core = new Material(glow);
                core.color = new Color(1f, .36f, .4f, 1f);
            }
            Vector3 pos = DrawPos;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, ExactRotation, new Vector3(.075f, 1f, .62f)), glow, 0);
            pos.y += .001f;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, ExactRotation, new Vector3(.024f, 1f, .5f)), core, 0);
        }
    }
}
