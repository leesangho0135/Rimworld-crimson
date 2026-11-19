using UnityEngine;

namespace CrimsonSOS2
{
    internal static class CrimsonTacticalTableArt
    {
        // Pixel windows select the unmodified transparent source atlases. Both
        // axes use parallel-oblique artwork, with the apron below the tabletop.
        private static Rect Window(int left, int top, int right, int bottom)
        {
            return new Rect(left / 1536f, (1024 - bottom) / 1024f,
                (right - left) / 1536f, (bottom - top) / 1024f);
        }

        private static CrimsonV51ArtSpec Create(Rect northOff, Rect eastOff,
            Rect northOn, Rect eastOn, Vector2 northSize, Vector2 eastSize,
            float northOffset, float eastOffset)
        {
            return new CrimsonV51ArtSpec(true,
                new[] { northOff, eastOff, northOff, eastOff, northOn, eastOn, northOn, eastOn },
                new[] { northSize, eastSize, northSize, eastSize },
                new[] { northOffset, eastOffset, northOffset, eastOffset }, true);
        }

        internal static CrimsonV51ArtSpec Find(string path)
        {
            if (path.EndsWith("TacticalTableLargeParallelV137", System.StringComparison.Ordinal))
                return Create(Window(274,16,544,450), Window(829,142,1287,429),
                    Window(274,520,544,954), Window(829,646,1287,934),
                    new Vector2(3.04f,5.15f), new Vector2(5.04f,2.765f), -0.0504f,0.053333f);
            if (path.EndsWith("TacticalTableCompactParallelV137", System.StringComparison.Ordinal))
                return Create(Window(240,195,745,399), Window(1011,38,1199,470),
                    Window(240,677,745,881), Window(1011,521,1199,953),
                    new Vector2(2.96f,0.98f), new Vector2(0.98f,2.96f), 0f,0f);
            return Create(Window(171,110,750,441), Window(972,27,1266,481),
                Window(171,622,750,953), Window(972,540,1266,994),
                new Vector2(3f,2f), new Vector2(2f,3f), 0f,0f);
        }
    }
}
