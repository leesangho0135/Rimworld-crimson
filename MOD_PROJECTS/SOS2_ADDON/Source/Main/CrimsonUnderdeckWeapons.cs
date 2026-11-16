using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace CrimsonSOS2
{
    // The dedicated mote keeps SOS2's timing, color and beam geometry.
    // Its marker limits the depth change to the Crimson spinal weapon.
    public sealed class CrimsonUnderdeckLaserMote : ShipCombatLaserMote { }

    [StaticConstructorOnStartup]
    internal static class CrimsonUnderdeckWeapons
    {
        internal static float Altitude { get { return AltitudeLayer.TerrainEdges.AltitudeFor(); } }
        internal static bool IsWeapon(Thing thing)
        { return thing is CrimsonSpinalLaser || thing is CrimsonTorpedoLauncher; }

        static CrimsonUnderdeckWeapons()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.underdeck-weapons");
            harmony.Patch(AccessTools.Method(typeof(Verb_LaunchProjectileShip), "TryCastShot"),
                transpiler: new HarmonyMethod(typeof(CrimsonUnderdeckWeapons), "BeamDefinition"));
            harmony.Patch(AccessTools.Method(typeof(ShipCombatLaserMote), "DrawAt"),
                transpiler: new HarmonyMethod(typeof(CrimsonUnderdeckWeapons), "BeamDepth"));
            harmony.Patch(AccessTools.Method(AccessTools.TypeByName("SaveOurShip2.Projectile_ShipFake"), "DrawAt"),
                prefix: new HarmonyMethod(typeof(CrimsonUnderdeckWeapons), "OutgoingDraw"));
            harmony.Patch(AccessTools.Method(typeof(Verb), "TryCastNextBurstShot"),
                transpiler: new HarmonyMethod(typeof(CrimsonUnderdeckWeapons), "FlashDefinition"));
        }

        private static ThingDef BeamFor(ThingDef original, Verb verb)
        {
            return verb.Caster is CrimsonSpinalLaser
                ? DefDatabase<ThingDef>.GetNamed("Crimson_UnderdeckLaserMote") : original;
        }
        private static FleckDef FlashFor(FleckDef original, Verb verb)
        {
            return IsWeapon(verb.Caster)
                ? DefDatabase<FleckDef>.GetNamed("Crimson_UnderdeckShotFlash") : original;
        }
        private static float DepthFor(float original, ShipCombatLaserMote mote)
        { return mote is CrimsonUnderdeckLaserMote ? Altitude : original; }

        private static IEnumerable<CodeInstruction> BeamDefinition(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                FieldInfo field = instruction.operand as FieldInfo;
                if (instruction.opcode == OpCodes.Ldsfld && field != null
                    && field.FieldType == typeof(ThingDef) && field.Name == "ShipCombatLaserMote")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CrimsonUnderdeckWeapons), "BeamFor"));
                }
            }
        }

        private static IEnumerable<CodeInstruction> BeamDepth(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo altitudeFor = AccessTools.Method(typeof(Altitudes), "AltitudeFor", new[] { typeof(AltitudeLayer) });
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(altitudeFor))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CrimsonUnderdeckWeapons), "DepthFor"));
                }
            }
        }

        private static IEnumerable<CodeInstruction> FlashDefinition(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                FieldInfo field = instruction.operand as FieldInfo;
                if (instruction.opcode == OpCodes.Ldsfld && field != null
                    && field.FieldType == typeof(FleckDef) && field.Name == "ShotFlash")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CrimsonUnderdeckWeapons), "FlashFor"));
                }
            }
        }

        private static bool OutgoingDraw(Projectile __instance, Vector3 drawLoc)
        {
            if (!IsWeapon(__instance.Launcher) || __instance.Map != __instance.Launcher.Map) return true;
            // Skip the native fake projectile's elevated arc and ground shadow.
            // Only the draw plane changes; projectile flight and hits are native.
            drawLoc.y = Altitude;
            Graphics.DrawMesh(MeshPool.GridPlane(__instance.def.graphicData.drawSize),
                drawLoc, __instance.ExactRotation, __instance.DrawMat, 0);
            return false;
        }
    }
}
