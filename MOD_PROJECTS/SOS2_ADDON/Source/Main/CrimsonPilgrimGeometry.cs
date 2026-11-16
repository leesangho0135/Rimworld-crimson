using System;

namespace CrimsonSOS2
{
    // The production pose calculation has no Unity, Verse or vehicle dependency.
    // Heading follows Unity's top-down convention: positive angles turn north
    // (+z) towards east (+x). Output is a carrier-relative world x/z offset.
    public static class PilgrimBitGeometry
    {
        public static float[] Calculate(float carrierHeading, float mountX, float mountZ,
            int side, float ticks, float period, float amplitude, float phase)
        {
            double x = mountX;
            if (side < 0) x = -Math.Abs(x);
            else if (side > 0) x = Math.Abs(x);

            double safePeriod = Math.Max(1.0, period);
            double safeAmplitude = Math.Max(0.0, amplitude);
            double hoverPhase = (ticks % safePeriod) * (Math.PI * 2.0 / safePeriod) + phase;
            // The two configured phases are intentionally independent. Equal
            // phases give equal local drift; only the mount side is mirrored.
            x += Math.Cos(hoverPhase) * safeAmplitude * 0.25;
            double z = mountZ + Math.Sin(hoverPhase) * safeAmplitude;

            double radians = carrierHeading * (Math.PI / 180.0);
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new float[] { (float)(x * cos + z * sin), (float)(z * cos - x * sin) };
        }
    }
}
