using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // Each material owns a real cropped frame. Blueprint shaders therefore
    // cannot accidentally draw the full atlas or another power-state row.
    public sealed class CrimsonCardinalGraphic : Graphic_Single
    {
        private CrimsonCardinalArtSpec art;
        private readonly Material[] frames = new Material[8];
        private readonly Material[] signals = new Material[4];
        public override bool ShouldDrawRotated { get { return false; } }
        public override Material MatSingle { get { return frames[4] ?? mat; } }
        public override Material MatNorth { get { return frames[4] ?? mat; } }
        public override Material MatEast { get { return frames[5] ?? mat; } }
        public override Material MatSouth { get { return frames[6] ?? mat; } }
        public override Material MatWest { get { return frames[7] ?? mat; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            art = CrimsonCardinalArt.Find(request.path);
            for (int i = 0; i < frames.Length; i++)
            {
                Rect uv = art.windows[i];
                Texture2D texture = CrimsonV51FurnitureGraphic.NativeFrame(mat.mainTexture,
                    uv.size, uv.position, i);
                Material frame = new Material(mat);
                frame.mainTexture = texture;
                frame.mainTextureScale = Vector2.one;
                frame.mainTextureOffset = Vector2.zero;
                frames[i] = frame;
            }
            mat = frames[4];
            if (request.shader == ShaderDatabase.Cutout)
                for (int i = 0; i < 4; i++) signals[i] = CrimsonFurniturePalette.Signal(frames[i + 4], frames[i]);
        }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            int direction = rot.AsInt;
            // Reflect the cabinet in its own front plane: an east/west mirror
            // must not turn the usable face away from the interaction side.
            if (rot.IsHorizontal && CrimsonVisualState.IsMirrored(thing)) direction = (direction + 2) % 4;
            Material frame = frames[direction + (CrimsonVisualState.Powered(thing) ? 4 : 0)];
            return CrimsonVisualState.Mirrored(frame, thing);
        }
        public override Material MatSingleFor(Thing thing)
        { return thing == null ? MatSingle : MatAt(thing.Rotation, thing); }
        public override Mesh MeshAt(Rot4 rot) { return MeshPool.GridPlane(art.sizes[rot.AsInt]); }
        internal float FrameOffset(Rot4 rot) { return art.offsets[rot.AsInt]; }
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef def, Thing thing, float extraRotation)
        {
            loc.z += FrameOffset(rot);
            CrimsonFurnitureDepth.Draw(this, loc, rot, def, thing, extraRotation);
            Material signal = Signal(rot, thing);
            if (signal != null)
            {
                Matrix4x4 matrix = CrimsonFurnitureDepth.Transform(this, loc, rot, thing, extraRotation);
                matrix.m13 += .002f;
                Graphics.DrawMesh(MeshAt(rot), matrix, signal, 0);
            }
        }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            Rot4 rot = thing.Rotation;
            CrimsonFurnitureDepth.Print(layer, thing, this, art.sizes[rot.AsInt], FrameOffset(rot));
            Material signal = Signal(rot, thing);
            if (signal == null) return;
            Matrix4x4 matrix = CrimsonFurnitureDepth.Transform(this,
                GenThing.TrueCenter(thing) + new Vector3(0f, 0f, FrameOffset(rot)), rot, thing, 0f);
            matrix.m13 += .002f;
            CrimsonFurnitureDepth.PrintQuad(layer.GetSubMesh(signal), matrix, art.sizes[rot.AsInt]);
        }
        private Material Signal(Rot4 rot, Thing thing)
        {
            int direction = rot.AsInt;
            if (rot.IsHorizontal && CrimsonVisualState.IsMirrored(thing)) direction = (direction + 2) % 4;
            if (thing == null || thing.def.entityDefToBuild != null || !CrimsonVisualState.Powered(thing)
                || signals[direction] == null) return null;
            return CrimsonVisualState.Mirrored(signals[direction], thing);
        }
        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            if (shader == ShaderDatabase.Cutout || shader == ShaderDatabase.CutoutComplex)
            { color = Color.white; colorTwo = Color.white; }
            return GraphicDatabase.Get<CrimsonCardinalGraphic>(path, shader, drawSize, color, colorTwo, data, null);
        }
    }

    internal sealed class CrimsonCardinalArtSpec
    {
        internal readonly Rect[] windows;
        internal readonly Vector2[] sizes;
        internal readonly float[] offsets;
        internal CrimsonCardinalArtSpec(Rect[] windows, Vector2[] sizes, float[] offsets)
        { this.windows = windows; this.sizes = sizes; this.offsets = offsets; }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonCardinalIcons
    {
        static CrimsonCardinalIcons()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.graphicData == null || def.graphicData.graphicClass != typeof(CrimsonCardinalGraphic)) continue;
                def.uiIcon = (Texture2D)def.graphicData.Graphic.MatNorth.mainTexture;
                def.uiIconMaterial = null;
            }
        }
    }

    internal static class CrimsonVisualState
    {
        private static readonly Dictionary<Material, Material> mirrors = new Dictionary<Material, Material>();
        internal static bool Powered(Thing thing)
        {
            if (thing == null) return true;
            CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
            CompFlickable flick = thing.TryGetComp<CompFlickable>();
            return (power == null || power.PowerOn) && (flick == null || flick.SwitchIsOn);
        }
        internal static bool IsMirrored(Thing thing)
        {
            CompCrimsonMirror mirror = thing == null ? null : thing.TryGetComp<CompCrimsonMirror>();
            return mirror != null && mirror.Mirrored;
        }
        internal static Material Mirrored(Material source, Thing thing)
        {
            if (!IsMirrored(thing)) return source;
            Material result;
            if (!mirrors.TryGetValue(source, out result))
            {
                result = new Material(source);
                Vector2 scale = source.mainTextureScale;
                Vector2 offset = source.mainTextureOffset;
                result.mainTextureScale = new Vector2(-scale.x, scale.y);
                result.mainTextureOffset = new Vector2(offset.x + scale.x, offset.y);
                mirrors.Add(source, result);
            }
            return result;
        }
    }

    public sealed class CompProperties_CrimsonMirror : CompProperties
    {
        public bool refrigeratorScreen;
        public CompProperties_CrimsonMirror() { compClass = typeof(CompCrimsonMirror); }
    }

    // Comp state travels with the real building, including when minified.
    [StaticConstructorOnStartup]
    public sealed class CompCrimsonMirror : ThingComp
    {
        private bool mirrored;
        private int screenVariant = -1;
        private static readonly Material[] screens = new Material[3];
        public bool Mirrored { get { return mirrored; } }
        public int ScreenVariant { get { EnsureVariant(); return screenVariant; } }
        private bool HasScreen { get { return ((CompProperties_CrimsonMirror)props).refrigeratorScreen; } }
        public override void PostPostMake()
        { if (HasScreen) screenVariant = Rand.Range(0, screens.Length); }
        private void EnsureVariant()
        {
            if (screenVariant < 0 || screenVariant >= screens.Length)
                screenVariant = (parent.thingIDNumber & int.MaxValue) % screens.Length;
        }
        public override void PostExposeData()
        {
            Scribe_Values.Look(ref mirrored, "crimsonFurnitureMirrored", false);
            Scribe_Values.Look(ref screenVariant, "crimsonRefrigeratorScreen", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && HasScreen) EnsureVariant();
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "Crimson_MirrorFurniture".Translate(),
                defaultDesc = "Crimson_MirrorFurnitureDesc".Translate(),
                icon = parent.def.uiIcon,
                isActive = delegate { return mirrored; },
                toggleAction = delegate
                {
                    mirrored = !mirrored;
                    parent.DirtyMapMesh(parent.Map);
                    foreach (IntVec3 cell in parent.OccupiedRect())
                        if (cell.InBounds(parent.Map)) parent.Map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Buildings);
                }
            };
        }
        public override void PostDraw()
        {
            // The south view is the actual closed rear. The embedded screen
            // is visible only from the front; it never floats over a side panel.
            if (!HasScreen || !parent.Spawned || parent.Rotation != Rot4.North
                || !CrimsonVisualState.Powered(parent)) return;
            EnsureVariant();
            if (screens[screenVariant] == null)
                screens[screenVariant] = MaterialPool.MatFrom(
                    "Crimson/Decor/Revision/RefrigeratorScreen" + (screenVariant + 1), ShaderDatabase.TransparentPostLight);
            CrimsonCardinalGraphic graphic = parent.Graphic as CrimsonCardinalGraphic;
            Vector3 location = parent.DrawPos;
            if (graphic != null) location.z += graphic.FrameOffset(parent.Rotation);
            Matrix4x4 body = CrimsonFurnitureDepth.Transform(parent.Graphic, location, parent.Rotation, parent, 0f);
            Matrix4x4 screen = body * Matrix4x4.TRS(new Vector3(mirrored ? -.48f : .48f, .012f, -.025f),
                Quaternion.identity, new Vector3(.29f, 1f, .54f));
            Graphics.DrawMesh(MeshPool.plane10, screen, screens[screenVariant], 0);
        }
    }
}
