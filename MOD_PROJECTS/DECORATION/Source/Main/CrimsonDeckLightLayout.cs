using System;
using System.Collections.Generic;

namespace CrimsonSOS2
{
    internal enum DeckLightSprite { Rail, Hub, End }

    internal struct DeckLightPiece
    {
        internal readonly DeckLightSprite Sprite;
        internal readonly float X, Z, Width, Length, Angle;

        internal DeckLightPiece(DeckLightSprite sprite, float x, float z,
            float width, float length, float angle)
        {
            Sprite = sprite;
            X = x;
            Z = z;
            Width = width;
            Length = length;
            Angle = angle;
        }
    }

    // Coordinates are in cells, before the building's cardinal rotation.
    // Sharing this layout with the preview keeps the source PNGs untouched.
    internal static class CrimsonDeckLightLayout
    {
        internal const float RailWidth = 0.14f;
        internal const float ParallelOffset = 0.20f;
        private static readonly DeckLightPiece[][] single = BuildSingles();
        private static readonly DeckLightPiece[][] parallel = BuildParallels();

        internal static DeckLightPiece[] Single(int mask)
        {
            if (mask < 0 || mask > 15) throw new ArgumentOutOfRangeException("mask");
            return single[mask];
        }

        internal static DeckLightPiece[] Parallel(int mask)
        {
            if (mask < 0 || mask > 15) throw new ArgumentOutOfRangeException("mask");
            return parallel[mask];
        }

        private static DeckLightPiece[][] BuildSingles()
        {
            DeckLightPiece[][] result = new DeckLightPiece[16][];
            for (int mask = 0; mask < result.Length; mask++)
            {
                List<DeckLightPiece> parts = new List<DeckLightPiece>();
                if (mask == 0)
                {
                    AddRun(parts, 0f, -0.5f, 0.5f, false, false);
                }
                else if (mask == 5 || mask == 10)
                {
                    parts.Add(new DeckLightPiece(DeckLightSprite.Rail, 0f, 0f,
                        RailWidth, 1f, mask == 5 ? 0f : 90f));
                }
                else
                {
                    int count = 0;
                    int lastDirection = 0;
                    for (int direction = 0; direction < 4; direction++)
                    {
                        if ((mask & (1 << direction)) == 0) continue;
                        float x = direction == 1 ? 0.25f : direction == 3 ? -0.25f : 0f;
                        float z = direction == 0 ? 0.25f : direction == 2 ? -0.25f : 0f;
                        parts.Add(new DeckLightPiece(DeckLightSprite.Rail, x, z,
                            RailWidth, 0.5f, direction * 90f));
                        count++;
                        lastDirection = direction;
                    }
                    if (count == 1)
                    {
                        float x = lastDirection == 1 ? -0.04f : lastDirection == 3 ? 0.04f : 0f;
                        float z = lastDirection == 0 ? -0.04f : lastDirection == 2 ? 0.04f : 0f;
                        parts.Add(new DeckLightPiece(DeckLightSprite.End, x, z,
                            RailWidth, 0.16f, lastDirection * 90f));
                    }
                    else if (mask == 15)
                    {
                        // The hub artwork has four arms. Corners and tees use
                        // only their connected half-rails, meeting at center.
                        parts.Add(new DeckLightPiece(DeckLightSprite.Hub, 0f, 0f,
                            0.16f, 0.16f, 0f));
                    }
                }
                result[mask] = parts.ToArray();
            }
            return result;
        }

        private static DeckLightPiece[][] BuildParallels()
        {
            DeckLightPiece[][] result = new DeckLightPiece[16][];
            for (int mask = 0; mask < result.Length; mask++)
            {
                List<DeckLightPiece> parts = new List<DeckLightPiece>();
                int count = 0, facing = 0;
                for (int direction = 0; direction < 4; direction++)
                    if ((mask & (1 << direction)) != 0) { count++; facing = direction; }
                if (count <= 1 || mask == 5 || mask == 10)
                {
                    bool openFront = count == 1 || count == 2;
                    bool openBack = count == 2;
                    AddRun(parts, -ParallelOffset, -0.5f, 0.5f, openBack, openFront);
                    AddRun(parts, ParallelOffset, -0.5f, 0.5f, openBack, openFront);
                    int turn = mask == 10 ? 1 : count == 1 ? facing : 0;
                    for (int i = 0; i < parts.Count; i++) parts[i] = Turn(parts[i], turn);
                }
                else
                {
                    // Trace both sides of a connected corridor. Missing arms close
                    // the center contour; connected arms meet at exact shared corners.
                    // This produces elbows, tees and four-way junctions from the
                    // same approved rail artwork, without overlapping a bright hub.
                    for (int direction = 0; direction < 4; direction++)
                    {
                        if ((mask & (1 << direction)) != 0)
                        {
                            float mid = (ParallelOffset + 0.5f) * 0.5f;
                            float length = 0.5f - ParallelOffset;
                            parts.Add(Turn(new DeckLightPiece(DeckLightSprite.Rail,
                                -ParallelOffset, mid, RailWidth, length, 0f), direction));
                            parts.Add(Turn(new DeckLightPiece(DeckLightSprite.Rail,
                                ParallelOffset, mid, RailWidth, length, 0f), direction));
                        }
                        else parts.Add(Turn(new DeckLightPiece(DeckLightSprite.Rail,
                            0f, ParallelOffset, RailWidth, 2f * ParallelOffset, 90f), direction));
                    }
                }
                result[mask] = parts.ToArray();
            }
            return result;
        }

        private static DeckLightPiece Turn(DeckLightPiece piece, int direction)
        {
            float x = piece.X, z = piece.Z;
            switch (direction)
            {
                case 1: x = piece.Z; z = -piece.X; break;
                case 2: x = -piece.X; z = -piece.Z; break;
                case 3: x = -piece.Z; z = piece.X; break;
            }
            return new DeckLightPiece(piece.Sprite, x, z, piece.Width, piece.Length,
                piece.Angle + direction * 90f);
        }

        private static void AddRun(List<DeckLightPiece> parts, float x, float min,
            float max, bool openBack, bool openFront)
        {
            float start = openBack ? min : min + 0.22f;
            float end = openFront ? max : max - 0.22f;
            parts.Add(new DeckLightPiece(DeckLightSprite.Rail, x, (start + end) * 0.5f,
                RailWidth, end - start, 0f));
            if (!openBack) parts.Add(new DeckLightPiece(DeckLightSprite.End, x, start,
                RailWidth, 0.16f, 0f));
            if (!openFront) parts.Add(new DeckLightPiece(DeckLightSprite.End, x, end,
                RailWidth, 0.16f, 180f));
        }
    }
}
