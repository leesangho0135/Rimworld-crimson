using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // The native casket owns its contents, suspension, eject and SOS2 launch
    // behavior. The immutable atlas now follows power rather than occupancy.
    public sealed class CrimsonCryptosleepGraphic : Graphic_Single
    {
        private Material unpowered;
        private Material powered;
        public override bool ShouldDrawRotated { get { return true; } }
        public override Material MatSingle { get { return powered ?? mat; } }
        public override Material MatNorth { get { return MatSingle; } }
        public override Material MatEast { get { return MatSingle; } }
        public override Material MatSouth { get { return MatSingle; } }
        public override Material MatWest { get { return MatSingle; } }

        public override void Init(GraphicRequest request)
        {
            base.Init(request);
            // Measured source windows are shared by live drawing, ghosts and
            // icons. Native frame textures avoid shaders ignoring atlas UVs.
            unpowered = Frame(mat, new Rect(111f, 70f, 406f, 1115f), "Unpowered");
            powered = Frame(mat, new Rect(738f, 70f, 406f, 1115f), "Powered");
            mat = powered;
        }

        private static Material Frame(Material source, Rect pixels, string state)
        {
            Texture texture = source.mainTexture;
            Vector2 scale = new Vector2(pixels.width / texture.width, pixels.height / texture.height);
            Vector2 offset = new Vector2(pixels.x / texture.width,
                1f - (pixels.y + pixels.height) / texture.height);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary((int)pixels.width, (int)pixels.height,
                0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(texture, target, scale, offset);
                RenderTexture.active = target;
                Texture2D frame = new Texture2D((int)pixels.width, (int)pixels.height,
                    TextureFormat.ARGB32, true);
                frame.name = texture.name + state;
                frame.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0);
                frame.wrapMode = TextureWrapMode.Clamp;
                frame.Apply(true, true);
                Material result = new Material(source);
                result.mainTexture = frame;
                result.mainTextureScale = Vector2.one;
                result.mainTextureOffset = Vector2.zero;
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        public override Material MatAt(Rot4 rotation, Thing thing = null)
        {
            CompPowerTrader power = thing == null ? null : thing.TryGetComp<CompPowerTrader>();
            return UsesPoweredFrame(power == null, power != null && power.PowerOn) ? powered : unpowered;
        }

        internal static bool UsesPoweredFrame(bool preview, bool powerOn)
        { return preview || powerOn; }

        public override Material MatSingleFor(Thing thing)
        { return MatAt(thing == null ? Rot4.North : thing.Rotation, thing); }

        public override void DrawWorker(Vector3 location, Rot4 rotation,
            ThingDef definition, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Draw(this, location, rotation, definition, thing, extraRotation); }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        { CrimsonFurnitureDepth.Print(layer, thing, this, CrimsonFurnitureDepth.Size(MeshAt(thing.Rotation))); }

        public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
        {
            if (shader == ShaderDatabase.Cutout || shader == ShaderDatabase.CutoutComplex)
            { color = Color.white; colorTwo = Color.white; }
            return GraphicDatabase.Get<CrimsonCryptosleepGraphic>(path, shader,
                drawSize, color, colorTwo, data, null);
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonCryptosleepRegistration
    {
        internal const string DefName = "Crimson_ShipCryptosleepCasket";
        static CrimsonCryptosleepRegistration()
        {
            ThingDef definition = DefDatabase<ThingDef>.GetNamedSilentFail(DefName);
            if (definition == null) return;
            definition.uiIcon = (Texture2D)definition.graphicData.Graphic.MatSingle.mainTexture;
            definition.uiIconMaterial = null;
            // SOS2's interstellar-travel predicate singles out its original
            // casket by Def identity. Extend that predicate only for our casket.
            Type closure = typeof(WorldObjectOrbitingShip).GetNestedType("<>c", BindingFlags.NonPublic);
            MethodInfo target = closure == null ? null : AccessTools.Method(closure, "<GetGizmos>b__36_2");
            if (target == null || target.ReturnType != typeof(bool)
                || target.GetParameters().Length != 1 || target.GetParameters()[0].ParameterType != typeof(Pawn))
                throw new InvalidOperationException("SOS2 ship cryptosleep travel predicate changed.");
            new Harmony("rimod.crimson.sos2.cryptosleep").Patch(target,
                postfix: new HarmonyMethod(typeof(CrimsonCryptosleepRegistration), "TravelReadiness"));
        }

        private static void TravelReadiness(Pawn __0, ref bool __result)
        {
            if (!__result || __0 == null || !__0.InContainerEnclosed) return;
            Thing holder = __0.ParentHolder as Thing;
            if (holder != null && holder.def.defName == DefName) __result = false;
        }
    }
}
