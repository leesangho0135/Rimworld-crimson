using System.Collections.Generic;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CrimsonShipVent : Building_ShipVent
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            // The native vent replaces a wall and regulates the next cell.
            // This attachment occupies that room cell; its north neighbor is the backing wall.
            ventTo = Position;
        }
    }

    public sealed class CrimsonShipVentGraphic : Graphic_Single
    {
        private static readonly Dictionary<Texture, Texture2D> frames = new Dictionary<Texture, Texture2D>();
        public override bool ShouldDrawRotated { get { return true; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            Texture source = mat.mainTexture;
            Texture2D frame;
            if (!frames.TryGetValue(source, out frame))
            {
                // A runtime view of V01's visible rectangle; the selected PNG is unchanged.
                // Ghost/blueprint shaders ignore material tiling, so use an actual frame texture.
                Vector2 scale = new Vector2(1103f / 1312f, 834f / 1199f);
                Vector2 offset = new Vector2(105f / 1312f, 178f / 1199f);
                RenderTexture previous = RenderTexture.active;
                RenderTexture target = RenderTexture.GetTemporary(1103, 834, 0, RenderTextureFormat.ARGB32);
                try
                {
                    Graphics.Blit(source, target, scale, offset);
                    RenderTexture.active = target;
                    frame = new Texture2D(1103, 834, TextureFormat.ARGB32, true);
                    frame.name = "CrimsonShipVentV01Frame";
                    frame.ReadPixels(new Rect(0, 0, 1103, 834), 0, 0);
                    frame.wrapMode = TextureWrapMode.Clamp;
                    frame.Apply(true, true);
                    frames.Add(source, frame);
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(target);
                }
            }
            mat = new Material(mat);
            mat.mainTexture = frame;
            mat.mainTextureScale = Vector2.one;
            mat.mainTextureOffset = Vector2.zero;
        }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            return GraphicDatabase.Get<CrimsonShipVentGraphic>(path, shader, drawSize, color, colorTwo, data, null);
        }
    }
}
