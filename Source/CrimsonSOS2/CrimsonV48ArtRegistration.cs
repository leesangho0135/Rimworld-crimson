using UnityEngine;

namespace CrimsonSOS2
{
    // Measured alpha bounds of the accepted original PNGs; no raster edits.
    internal static class CrimsonV48ArtRegistration
    {
        internal static CrimsonV48ArtSpec Find(string path, Vector2 fallback)
        {
            CrimsonV48ArtSpec royalty;
            if (CrimsonV49RoyaltyArtRegistration.TryFind(path, out royalty)) return royalty;
            switch (path)
            {
                case "Crimson/Decor/Rooms/AuxiliaryCrewSeatAtlas":
                    return new CrimsonV48ArtSpec(
                        new[] { new Rect(0.130781f, 0.582137f, 0.240829f, 0.318182f), new Rect(0.649123f, 0.582137f, 0.216906f, 0.341308f), new Rect(0.129984f, 0.082137f, 0.240032f, 0.338915f), new Rect(0.633971f, 0.082137f, 0.219298f, 0.341308f) },
                        new[] { new Vector2(1.051741f, 1.389552f), new Vector2(0.947264f, 1.490547f), new Vector2(1.048259f, 1.480100f), new Vector2(0.957711f, 1.490547f) },
                        new[] { 0.044776f, 0.095274f, 0.090050f, 0.095274f });
                case "Crimson/Decor/Workshops/FabricationBenchAtlas":
                    return new CrimsonV48ArtSpec(
                        new[] { new Rect(0.072568f, 0.637959f, 0.377990f, 0.204944f), new Rect(0.12890625f, 0.30078125f, 0.3056640625f, 0.482421875f), new Rect(0.069378f, 0.131579f, 0.385167f, 0.210526f), new Rect(0.56640625f, 0.30078125f, 0.3056640625f, 0.482421875f) },
                        new[] { new Vector2(3.120376f, 1.691850f), new Vector2(0.98f, 2.98f), new Vector2(3.179624f, 1.737931f), new Vector2(0.98f, 2.98f) },
                        new[] { 0f, 0f, 0f, 0f },
                        "Crimson/Decor/Workshops/FabricationBenchSidesV144");
                case "Crimson/Decor/Hydroponics/HydroponicsLargeAtlas":
                    return new CrimsonV48ArtSpec(
                        new[] { new Rect(0.045922f, 0.528892f, 0.423595f, 0.430177f), new Rect(0.531275f, 0.528892f, 0.424386f, 0.429374f), new Rect(0.045922f, 0.045746f, 0.423595f, 0.430979f), new Rect(0.531275f, 0.045746f, 0.424386f, 0.430979f) },
                        new[] { new Vector2(2.600000f, 2.604860f), new Vector2(2.604860f, 2.600000f), new Vector2(2.600000f, 2.609720f), new Vector2(2.604860f, 2.609720f) },
                        new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f });
                case "Crimson/Decor/Hydroponics/HydroponicsSmallAtlas":
                    return new CrimsonV48ArtSpec(
                        new[] { new Rect(0.050239f, 0.535088f, 0.408293f, 0.416268f), new Rect(0.541467f, 0.535885f, 0.409091f, 0.415470f), new Rect(0.050239f, 0.051037f, 0.409091f, 0.415470f), new Rect(0.541467f, 0.051037f, 0.409091f, 0.415470f) },
                        new[] { new Vector2(1.998049f, 2.037073f), new Vector2(2.001951f, 2.033171f), new Vector2(2.001951f, 2.033171f), new Vector2(2.001951f, 2.033171f) },
                        new[] { 0.000000f, 0.000000f, 0.000000f, 0.000000f });
                default:
                    return new CrimsonV48ArtSpec(
                        new[] { new Rect(0f,0f,1f,1f), new Rect(0f,0f,1f,1f), new Rect(0f,0f,1f,1f), new Rect(0f,0f,1f,1f) },
                        new[] { fallback, fallback, fallback, fallback }, new float[4]);
            }
        }
    }
}
