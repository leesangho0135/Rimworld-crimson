using System;
using RimWorld;
using Verse.AI;

namespace CrimsonSOS2
{
    // Only the Crimson poker job uses this driver. The native play toil still
    // handles reservations, facing, comfort, joy, Intellectual XP and effects.
    public sealed class CrimsonJobDriver_PlayPoker : JobDriver_PlayPoker
    {
        protected override void ModifyPlayToil(Toil toil)
        {
            base.ModifyPlayToil(toil);
            Action<int> nativeTick = toil.tickIntervalAction;
            toil.tickIntervalAction = delegate(int delta)
            {
                // Match JoyTickCheckEnd's no-joy guard and interval scaling.
                // Both skills use the job's 0.003 rate (native 0.004 * 0.75).
                if (pawn.skills != null && (pawn.needs.joy != null || job.doUntilGatheringEnded))
                    pawn.skills.GetSkill(SkillDefOf.Social).Learn(job.def.joyXpPerTick * delta);
                nativeTick(delta);
            };
        }
    }
}
