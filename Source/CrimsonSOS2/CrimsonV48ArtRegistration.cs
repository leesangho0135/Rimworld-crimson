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
                case "Crimson/Decor/Rooms/MedicalSupplyCabinetAtlas":
                    return new CrimsonV48ArtSpec(
                        new[] { new Rect(0.129187f, 0.557416f, 0.262360f, 0.397129f), new Rect(0.610048f, 0.556619f, 0.254386f, 0.397927f), new Rect(0.129187f, 0.057416f, 0.262360f, 0.397129f), new Rect(0.611643f, 0.057416f, 0.257576f, 0.397129f) },
                        new[] { new Vector2(1.000000f, 1.513678f), new Vector2(0.969605f, 1.516717f), new Vector2(1.000000f, 1.513678f), new Vector2(0.981763f, 1.513678f) },
                        new[] { 0.056839f, 0.058359f, 0.056839f, 0.056839f });
                case "Crimson/Decor/Workshops/InductionCookerAtlas":
                    return new CrimsonV48ArtSpec(
                        new[] { new Rect(0.039075f, 0.624402f, 0.500000f, 0.217703f), new Rect(0.683413f, 0.538278f, 0.200957f, 0.385965f), new Rect(0.038278f, 0.143541f, 0.502392f, 0.216906f), new Rect(0.682616f, 0.062998f, 0.201754f, 0.386762f) },
                        new[] { new Vector2(3.142482f, 1.368258f), new Vector2(1.263007f, 3.365752f), new Vector2(3.157518f, 1.363246f), new Vector2(1.268019f, 3.365752f) },
                        new[] { 0.234129f, 0.232876f, 0.231623f, 0.232876f });
                case "Crimson/Decor/Workshops/MachiningBenchAtlas":
                    return new CrimsonV48ArtSpec(
                        new[] { new Rect(0.059809f, 0.644338f, 0.505582f, 0.208931f), new Rect(0.726475f, 0.587719f, 0.168262f, 0.315789f), new Rect(0.055024f, 0.148325f, 0.514354f, 0.216108f), new Rect(0.728070f, 0.097289f, 0.168262f, 0.321372f) },
                        new[] { new Vector2(3.122909f, 1.290539f), new Vector2(1.039328f, 3.312705f), new Vector2(3.177091f, 1.334871f), new Vector2(1.039328f, 3.312705f) },
                        new[] { 0f, 0f, 0f, 0f });
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
