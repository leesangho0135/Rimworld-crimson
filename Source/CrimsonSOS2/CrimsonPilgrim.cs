using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SaveOurShip2;
using SmashTools;
using Vehicles;
using Verse;

namespace CrimsonSOS2
{
    // Separate vehicle identity. Normal VF movement, operator checks, damage,
    // construction, launching and upgrades keep their installed implementations.
    public sealed class CrimsonPilgrimVehicle : VehiclePawn
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            // Landed, idle craft start nose-north. Active ground movement keeps
            // VF's heading rather than being forced back every tick.
            if (!Drafted && (vehiclePather == null || !vehiclePather.Moving))
                FullRotation = Rot8.North;
        }

        public override string GetInspectString()
        {
            return CrimsonWeaponStatus.Append(base.GetInspectString(), CrimsonWeaponStatus.Pilgrim(this));
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            CompPilgrimHeatNet heat = this.TryGetComp<CompPilgrimHeatNet>();
            if (heat != null) heat.PrepareForVehicleDespawn();
            base.DeSpawn(mode);
            if (heat != null) heat.CompleteVehicleDespawn();
        }
    }

    [StaticConstructorOnStartup]
    internal static class CrimsonPilgrimInitialization
    {
        private static readonly FieldInfo HoverExhaustField = AccessTools.Field(
            AccessTools.TypeByName("SaveOurShip2.ResourceBank+FleckDefOf"), "SoS2Exhaust_Short");
        private static readonly FleckDef PilgrimHoverExhaust =
            DefDatabase<FleckDef>.GetNamed("Crimson_PilgrimExhaust_Short");

        static CrimsonPilgrimInitialization()
        {
            if (HoverExhaustField == null || PilgrimHoverExhaust == null)
                throw new InvalidOperationException("Crimson Pilgrim hover exhaust definitions or native field are missing.");
            Harmony harmony = new Harmony("rimod.crimson.sos2.pilgrim.identity");
            harmony.Patch(AccessTools.Method(typeof(ShipInteriorMod2), "ShuttleHasLaser"),
                prefix: new HarmonyMethod(typeof(CrimsonPilgrimInitialization), "HasFixedLasers"));
            harmony.Patch(AccessTools.Method(AccessTools.TypeByName("SaveOurShip2.Vehicles.CompHoverMode"), "CompTick"),
                transpiler: new HarmonyMethod(typeof(CrimsonPilgrimInitialization), "HoverExhaustTranspiler"));
        }

        private static IEnumerable<CodeInstruction> HoverExhaustTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo select = AccessTools.Method(typeof(CrimsonPilgrimInitialization), "HoverExhaustFor");
            int replacements = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldsfld && Equals(instruction.operand, HoverExhaustField))
                {
                    // Select only this vehicle's effect. The native hover tick's
                    // conditions, random values, position, size and timing remain.
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, select);
                    replacements++;
                }
            }
            if (replacements != 1)
                throw new InvalidOperationException("Crimson Pilgrim expected one native hover exhaust load; found " + replacements + ".");
        }

        private static FleckDef HoverExhaustFor(FleckDef original, ThingComp component)
        {
            return component != null && component.parent is CrimsonPilgrimVehicle
                ? PilgrimHoverExhaust : original;
        }

        private static bool HasFixedLasers(VehiclePawn __0, ref bool __result)
        {
            if (!(__0 is CrimsonPilgrimVehicle)) return true;
            // SOS2 normally looks for optional upgrade records. Both of this
            // vehicle's laser bits are factory-installed, without those upgrades.
            __result = true;
            return false;
        }
    }
}
