using System;

namespace CrimsonSOS2
{
    internal static class CrimsonDecorHoverMotion
    {
        internal const int PeriodTicks = 240;
        internal const float Amplitude = 0.04f;
        internal const float MinimumGap = 0.10f;
        internal const float BaseCenterZ = 0f;
        internal const float BaseDepth = 2f;

        internal static float CenterZ(float bodyHeight)
        {
            // The lower tip hovers over the pedestal's center, rather than
            // starting beyond the pedestal's north/back edge.
            return BaseCenterZ + MinimumGap + Amplitude
                + bodyHeight * 0.5f;
        }

        internal static float Offset(int tick, int identity)
        {
            long phase = ((long)tick + identity) % PeriodTicks;
            return (float)(Math.Sin(phase * (2.0 * Math.PI / PeriodTicks)) * Amplitude);
        }
    }
}
