using System;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonTorpedoLauncher : Building_ShipTurretTorpedo
    {
        private static readonly Vector3[] TubeOffsets =
        {
            // Upper, left, right, lower: a diamond in the front cross-section.
            // The upper and lower tubes share the central overhead projection.
            new Vector3(0f, 0f, 2.28f),
            new Vector3(-0.66f, 0f, 2.28f),
            new Vector3(0.66f, 0f, 2.28f),
            new Vector3(0f, 0f, 2.28f)
        };

        private int nextTube;
        private int nextVolleyReadyTick;
        private bool volleyStarted;

        public override void BeginBurst()
        {
            if (Find.TickManager.TicksGame < nextVolleyReadyTick) return;
            volleyStarted = false;
            base.BeginBurst();
        }

        internal void NotifyTorpedoFired()
        {
            if (volleyStarted) return;
            volleyStarted = true;
            // Subsequent shots in this volley must not restart the cycle.
            nextVolleyReadyTick = Find.TickManager.TicksGame + 1200;
        }

        public override void BurstComplete()
        {
            base.BurstComplete();
            // Count from the first actual shot, including interrupted bursts.
            // The native ground-defense cooldown multiplier must not extend it.
            burstCooldownTicksLeft = Math.Max(0, nextVolleyReadyTick - Find.TickManager.TicksGame);
        }

        public Vector3 NextTubeOffset()
        {
            Vector3 offset = TubeOffsets[nextTube];
            nextTube = (nextTube + 1) % TubeOffsets.Length;
            return Rotation.AsQuat * offset;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextTube, "crimsonNextTube", 0);
            Scribe_Values.Look(ref nextVolleyReadyTick, "crimsonNextVolleyReadyTick", 0);
            Scribe_Values.Look(ref volleyStarted, "crimsonVolleyStarted", false);
            nextTube = Math.Max(0, Math.Min(nextTube, TubeOffsets.Length - 1));
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // The Crimson open bay has its own four-tube artwork. SOS2's
            // hard-coded single-tube grey door must not be drawn over it.
            Graphic.Draw(drawLoc, Rotation, this);
            Comps_PostDraw();
            Comps_DrawAt(drawLoc, flip);
        }

        public override string GetInspectString()
        {
            string result = base.GetInspectString();
            result = CrimsonWeaponStatus.Append(result, "특이점 탄두 내장 · 탄약 및 재장전 불필요");
            return CrimsonWeaponStatus.Append(result, CrimsonWeaponStatus.Building(this));
        }
    }
}
