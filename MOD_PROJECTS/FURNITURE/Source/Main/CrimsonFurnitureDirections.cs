using UnityEngine;

namespace CrimsonSOS2
{
    // Gameplay Rot4 is unchanged. Only the source-art selection is corrected:
    // North exposes the usable face to the south, East to the west.
    internal static class CrimsonFurnitureDirections
    {
        internal static int Source(string path, int direction)
        {
            string file = path.Substring(path.LastIndexOf('/') + 1);
            switch (file)
            {
                case "CommandSeatDirections":
                case "AuxiliaryCrewSeatAtlas":
                case "DiningChairV43States":
                case "LoungeChairV43States":
                case "WorkChairV43States":
                case "DiningChairAtlas":
                case "LoungeChairAtlas":
                case "PianoAtlas":
                case "Harpsichord":
                case "ShowerV42States":
                case "SafeV42States":
                case "CargoContainerStates":
                    return (direction + 2) % 4;
                case "CrewSofaTwoAtlas":
                case "CrewSofa3x1Atlas":
                case "SofaAtlas":
                case "MedicalSupplyCabinetAtlas":
                    return (direction & 1) == 0 ? (direction + 2) % 4 : direction;
                case "Drape":
                    return (direction & 1) == 1 ? (direction + 2) % 4 : direction;
                default:
                    return direction;
            }
        }

        internal static CrimsonV48ArtSpec Remap(string path, CrimsonV48ArtSpec original)
        {
            Rect[] windows = new Rect[4];
            Vector2[] sizes = new Vector2[4];
            float[] offsets = new float[4];
            for (int i = 0; i < 4; i++)
            {
                int source = Source(path, i);
                windows[i] = original.windows[source];
                sizes[i] = original.sizes[source];
                offsets[i] = original.offsets[source];
            }
            return new CrimsonV48ArtSpec(windows, sizes, offsets, original.horizontalTexture);
        }

        internal static CrimsonV51ArtSpec Remap(string path, CrimsonV51ArtSpec original)
        {
            Rect[] windows = new Rect[original.windows.Length];
            Vector2[] sizes = new Vector2[4];
            float[] offsets = new float[4];
            for (int i = 0; i < windows.Length; i++)
                windows[i] = original.windows[Source(path, i % 4) + (i / 4) * 4];
            for (int i = 0; i < 4; i++)
            {
                int source = Source(path, i);
                sizes[i] = original.sizes[source];
                offsets[i] = original.offsets[source];
            }
            return new CrimsonV51ArtSpec(original.powered, windows, sizes, offsets, original.nativeFrameTextures);
        }
    }
}
