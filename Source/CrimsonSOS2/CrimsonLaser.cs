using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // V8's shared pivot is (627,714.78) on a 1254-square source canvas.
    // A virtual1600-square canvas centers that pivot without modifying PNGs.
    public sealed class CrimsonLaserGraphic : Graphic_Single
    {
        private Material registered;
        public override Material MatSingle
        {
            get
            {
                if (registered == null)
                {
                    registered = new Material(base.MatSingle);
                    if (path.StartsWith("Crimson/Weapons/Laser/"))
                        registered.mainTexture.wrapMode = TextureWrapMode.Clamp;
                    registered.mainTextureScale = new Vector2(1600f / 1254f, 1600f / 1254f);
                    registered.mainTextureOffset = new Vector2(-173f / 1254f, -260.78f / 1254f);
                }
                return registered;
            }
        }
        public override Material MatNorth { get { return MatSingle; } }
        public override Material MatEast { get { return MatSingle; } }
        public override Material MatSouth { get { return MatSingle; } }
        public override Material MatWest { get { return MatSingle; } }
        public override Material MatAt(Rot4 rot, Thing thing = null) { return MatSingle; }
        public override Graphic GetColoredVersion(Shader shader, Color newColor, Color newColorTwo)
        {
            return GraphicDatabase.Get<CrimsonLaserGraphic>(path, shader, drawSize, newColor, newColorTwo);
        }
    }

    public sealed class CrimsonLaserTurret : Building_ShipTurret
    {
        // Marker for the new turret's projectile-bank adapter. Its independent
        // weapon Def has minRange=0 for both ship and ground defense modes.
        public override string GetInspectString()
        {
            return CrimsonWeaponStatus.Append(base.GetInspectString(), CrimsonWeaponStatus.Building(this));
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonLaserPatches
    {
        static CrimsonLaserPatches()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.laser");
            harmony.Patch(AccessTools.Method(typeof(Verb_LaunchProjectileShip), "TryCastShot"),
                transpiler: new HarmonyMethod(typeof(CrimsonLaserPatches), "ResolveBankLoads"));
        }

        // SOS2 compares these definitions by reference, including its beam branch.
        // Substitute only the new turret's corresponding definitions at those reads.
        private static IEnumerable<CodeInstruction> ResolveBankLoads(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                FieldInfo field = instruction.operand as FieldInfo;
                if (instruction.opcode == OpCodes.Ldsfld && field != null &&
                    field.FieldType == typeof(ThingDef) &&
                    (field.Name == "Bullet_Fake_Laser" || field.Name == "Bullet_Ground_Laser"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(CrimsonLaserPatches), "DefinitionForContext"));
                }
            }
        }

        private static ThingDef DefinitionForContext(ThingDef original, object context)
        {
            Thing thing = context as Thing;
            Verb verb = context as Verb;
            if (verb != null) thing = verb.Caster;
            if (!(thing is CrimsonLaserTurret)) return original;
            if (original.defName == "Bullet_Fake_Laser")
                return DefDatabase<ThingDef>.GetNamed("Crimson_LaserFakeProjectile");
            if (original.defName == "Bullet_Ground_Laser")
                return DefDatabase<ThingDef>.GetNamed("Crimson_LaserGroundProjectile");
            return original;
        }
    }
}
