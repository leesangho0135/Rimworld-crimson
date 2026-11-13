using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    internal struct DeckLightDrawPiece
    {
        internal readonly DeckLightSprite Sprite;
        internal readonly Matrix4x4 Transform;

        internal DeckLightDrawPiece(DeckLightSprite sprite, Matrix4x4 transform)
        {
            Sprite = sprite;
            Transform = transform;
        }
    }

    // Only local artwork transforms are cached. Neighbors, power and world position
    // are read on every draw, so building/removing rails and switching power stay immediate.
    internal static class CrimsonDeckLightDrawLayout
    {
        private static readonly DeckLightDrawPiece[][] singles = new DeckLightDrawPiece[16][];
        private static readonly DeckLightDrawPiece[][] parallels = new DeckLightDrawPiece[16][];

        internal static DeckLightDrawPiece[] Get(bool parallel, Rot4 facing, int mask)
        {
            DeckLightPiece[] layout = parallel ? CrimsonDeckLightLayout.Parallel(mask)
                : CrimsonDeckLightLayout.Single(mask);
            // Both variants now infer their complete cardinal shape from neighbors.
            // Legacy saved rotations do not rotate an automatically connected rail.
            int key = mask;
            DeckLightDrawPiece[][] cache = parallel ? parallels : singles;
            if (cache[key] == null) cache[key] = Build(layout, 0f);
            return cache[key];
        }

        private static DeckLightDrawPiece[] Build(DeckLightPiece[] layout, float angle)
        {
            DeckLightDrawPiece[] result = new DeckLightDrawPiece[layout.Length];
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            for (int i = 0; i < layout.Length; i++)
            {
                DeckLightPiece piece = layout[i];
                Vector3 offset = rotation * new Vector3(piece.X, 0f, piece.Z);
                if (piece.Sprite != DeckLightSprite.Rail) offset.y += 0.001f;
                Quaternion facing = rotation * Quaternion.AngleAxis(piece.Angle, Vector3.up);
                result[i] = new DeckLightDrawPiece(piece.Sprite, Matrix4x4.TRS(offset, facing,
                    new Vector3(piece.Width, 1f, piece.Length)));
            }
            return result;
        }
    }
}
