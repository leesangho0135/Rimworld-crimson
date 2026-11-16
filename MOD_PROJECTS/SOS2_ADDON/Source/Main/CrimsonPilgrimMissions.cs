using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SaveOurShip2;
using SaveOurShip2.Vehicles;
using UnityEngine;
using Vehicles;
using Verse;

namespace CrimsonSOS2
{
    public sealed class CompProperties_PilgrimMissions : CompProperties
    {
        public CompProperties_PilgrimMissions()
        {
            compClass = typeof(CompPilgrimMissions);
        }
    }

    // Native vehicle missions do not run the map turret's firing sequence.
    // Keep the two mission burst clocks on the carrier so they survive saving.
    public sealed class CompPilgrimMissions : ThingComp
    {
        private int missionId = -1;
        private List<int> remainingShots = new List<int> { 0, 0 };
        private List<int> nextShotTick = new List<int> { 0, 0 };

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref missionId, "crimsonPilgrimWeaponMissionId", -1);
            Scribe_Collections.Look(ref remainingShots, "crimsonPilgrimMissionRemainingShots", LookMode.Value);
            Scribe_Collections.Look(ref nextShotTick, "crimsonPilgrimMissionNextShotTick", LookMode.Value);
            Normalize();
        }

        private void Normalize()
        {
            if (remainingShots == null) remainingShots = new List<int>();
            if (nextShotTick == null) nextShotTick = new List<int>();
            while (remainingShots.Count < 2) remainingShots.Add(0);
            while (nextShotTick.Count < 2) nextShotTick.Add(0);
            for (int slot = 0; slot < 2; slot++)
                remainingShots[slot] = Mathf.Clamp(remainingShots[slot], 0, PilgrimMissionAdapter.BurstShotCount);
        }

        internal bool CommitShot(int id, int slot, int tick, VehicleTurret turret)
        {
            Normalize();
            if (missionId != id)
            {
                missionId = id;
                for (int index = 0; index < 2; index++)
                {
                    remainingShots[index] = 0;
                    nextShotTick[index] = tick;
                }
            }
            if (tick < nextShotTick[slot]) return false;
            if (remainingShots[slot] <= 0)
            {
                remainingShots[slot] = PilgrimMissionAdapter.BurstShotCount;
                nextShotTick[slot] = tick + Math.Max(0, Mathf.RoundToInt(turret.def.warmUpTimer * 60f));
                if (tick < nextShotTick[slot]) return false;
            }

            remainingShots[slot]--;
            Vehicles.FireMode mode = turret.CurrentFireMode;
            int interval = mode == null ? 15 : Math.Max(1, mode.ticksBetweenShots);
            int reload = Math.Max(1, Mathf.RoundToInt(turret.def.reloadTimer * 60f));
            nextShotTick[slot] = tick + (remainingShots[slot] > 0 ? interval : reload);
            return true;
        }
    }

    [StaticConstructorOnStartup]
    public static class PilgrimMissionAdapter
    {
        public const float ShipMaximumRange = 100f;
        public const int BurstShotCount = 30;
        public const int LaserDamage = 75;

        private const string ProjectileDefName = "Crimson_PilgrimSpaceLaserProjectile";
        private const string TurretDefName = "Crimson_PilgrimLaser";
        // These installed SOS2 projectile classes are internal. Resolve their
        // identities for classification without changing assembly visibility.
        private static readonly Type TorpedoType = AccessTools.TypeByName("SaveOurShip2.Projectile_ExplosiveShipTorpedo");
        private static readonly Type AntimatterType = AccessTools.TypeByName("SaveOurShip2.Projectile_ExplosiveShipAntigrain");

        static PilgrimMissionAdapter()
        {
            Harmony harmony = new Harmony("rimod.crimson.sos2.pilgrim.missions");
            harmony.Patch(AccessTools.Method(typeof(ShipMapComp), "MapComponentTick"),
                transpiler: new HarmonyMethod(typeof(PilgrimMissionAdapter), "ConnectMissionWeapons"));
        }

        // Insert two guarded calls in the installed method: after the native
        // lifted-off vehicle Tick, and before the native weapon-only branches.
        // Movement, fuel use, retreat checks, arrival and all other vehicles
        // continue through their original instructions.
        private static IEnumerable<CodeInstruction> ConnectMissionWeapons(
            IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            int cooldown = -1;
            int gate = -1;
            int fuel = -1;
            int vehicleTick = -1;
            Type missionType = typeof(ShipMapComp.ShuttleMissionData);
            for (int index = 0; index < code.Count; index++)
            {
                FieldInfo field = code[index].operand as FieldInfo;
                if (cooldown < 0 && code[index].opcode == OpCodes.Ldfld && field != null
                    && field.DeclaringType == missionType && field.Name == "weaponCooldown")
                    cooldown = index;

                MethodInfo method = code[index].operand as MethodInfo;
                if (method != null && method.Name == "Tick" && index >= 3)
                {
                    FieldInfo shuttle = code[index - 1].operand as FieldInfo;
                    FieldInfo closure = code[index - 2].operand as FieldInfo;
                    if (shuttle != null && shuttle.DeclaringType == missionType && shuttle.Name == "shuttle"
                        && closure != null && closure.FieldType == missionType)
                        vehicleTick = index;
                }
                if (method != null && method.Name == "get_FuelEfficiency" && index >= 8
                    && index + 1 < code.Count && code[index + 1].opcode == OpCodes.Ldc_R4
                    && (float)code[index + 1].operand == 6000f)
                    fuel = index - 8;
            }
            if (cooldown >= 0)
            {
                for (int index = cooldown - 1; index >= Math.Max(2, cooldown - 24); index--)
                {
                    FieldInfo field = code[index].operand as FieldInfo;
                    if (code[index].opcode == OpCodes.Ldfld && field != null
                        && field.DeclaringType == missionType && field.Name == "mission"
                        && code[index + 1].opcode == OpCodes.Ldc_I4_3)
                    {
                        gate = index - 2;
                        break;
                    }
                }
            }

            FieldInfo gateClosure = gate >= 0 ? code[gate + 1].operand as FieldInfo : null;
            FieldInfo fuelClosure = fuel >= 0 ? code[fuel + 1].operand as FieldInfo : null;
            if (gate < 0 || fuel <= gate || vehicleTick < 0 || vehicleTick >= gate || gateClosure == null
                || gateClosure.FieldType != missionType || !Equals(gateClosure, fuelClosure))
            {
                Log.Error("[Crimson SOS2] N01 mission bridge did not match installed SOS2; original mission code retained.");
                return code;
            }

            Label nativeFlightFuel = generator.DefineLabel();
            code[fuel].labels.Add(nativeFlightFuel);
            CodeInstruction first = new CodeInstruction(OpCodes.Ldarg_0);
            first.labels.AddRange(code[gate].labels);
            first.blocks.AddRange(code[gate].blocks);
            code[gate].labels.Clear();
            code[gate].blocks.Clear();
            List<CodeInstruction> weaponHook = new List<CodeInstruction>
            {
                first,
                new CodeInstruction(code[gate].opcode, code[gate].operand),
                new CodeInstruction(OpCodes.Ldfld, gateClosure),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PilgrimMissionAdapter), "HandleWeapons")),
                new CodeInstruction(OpCodes.Brtrue, nativeFlightFuel)
            };
            code.InsertRange(gate, weaponHook);

            // The vehicle Tick precedes the weapon gate in the installed method.
            List<CodeInstruction> heatHook = new List<CodeInstruction>
            {
                new CodeInstruction(code[vehicleTick - 3].opcode, code[vehicleTick - 3].operand),
                new CodeInstruction(OpCodes.Ldfld, code[vehicleTick - 2].operand),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PilgrimMissionAdapter), "TickMissionHeat"))
            };
            code.InsertRange(vehicleTick + 1, heatHook);
            // Inserted calls can put an existing short branch out of byte range.
            // Expand its encoding while retaining exactly the same target label.
            foreach (CodeInstruction instruction in code)
                instruction.opcode = LongBranch(instruction.opcode);
            return code;
        }

        private static OpCode LongBranch(OpCode opcode)
        {
            if (opcode == OpCodes.Br_S) return OpCodes.Br;
            if (opcode == OpCodes.Brfalse_S) return OpCodes.Brfalse;
            if (opcode == OpCodes.Brtrue_S) return OpCodes.Brtrue;
            if (opcode == OpCodes.Beq_S) return OpCodes.Beq;
            if (opcode == OpCodes.Bge_S) return OpCodes.Bge;
            if (opcode == OpCodes.Bge_Un_S) return OpCodes.Bge_Un;
            if (opcode == OpCodes.Bgt_S) return OpCodes.Bgt;
            if (opcode == OpCodes.Bgt_Un_S) return OpCodes.Bgt_Un;
            if (opcode == OpCodes.Ble_S) return OpCodes.Ble;
            if (opcode == OpCodes.Ble_Un_S) return OpCodes.Ble_Un;
            if (opcode == OpCodes.Blt_S) return OpCodes.Blt;
            if (opcode == OpCodes.Blt_Un_S) return OpCodes.Blt_Un;
            if (opcode == OpCodes.Bne_Un_S) return OpCodes.Bne_Un;
            if (opcode == OpCodes.Leave_S) return OpCodes.Leave;
            return opcode;
        }

        private static void TickMissionHeat(ShipMapComp.ShuttleMissionData mission)
        {
            if (mission == null || !mission.liftedOffYet || !(mission.shuttle is CrimsonPilgrimVehicle)) return;
            CompPilgrimHeatSink sink = mission.shuttle.TryGetComp<CompPilgrimHeatSink>();
            if (sink != null) sink.ProcessActiveMissionHeat();
        }

        private static bool HandleWeapons(ShipMapComp map, ShipMapComp.ShuttleMissionData mission)
        {
            if (mission == null || !(mission.shuttle is CrimsonPilgrimVehicle)) return false;
            VehiclePawn vehicle = mission.shuttle;
            if (!mission.liftedOffYet || vehicle.Destroyed || vehicle.CompFueledTravel == null
                || vehicle.CompVehicleTurrets == null || map.TargetMapComp == null) return true;
            CompPilgrimMissions schedule = vehicle.TryGetComp<CompPilgrimMissions>();
            if (schedule == null) return true;
            int tick = Find.TickManager.TicksGame;
            int slot = 0;
            foreach (VehicleTurret turret in vehicle.CompVehicleTurrets.Turrets)
            {
                if (!(turret is CrimsonPilgrimBit) || turret.def == null
                    || turret.def.defName != TurretDefName) continue;
                if (slot >= 2) break;
                int index = slot++;
                if (turret.ComponentDisabled || vehicle.CompFueledTravel.Fuel < 1f) continue;

                if (mission.mission == ShipMapComp.ShuttleMission.STRAFE
                    || mission.mission == ShipMapComp.ShuttleMission.BOMB)
                {
                    if (Mathf.Abs(map.OriginMapComp.Range - mission.rangeTraveled) > ShipMaximumRange) continue;
                    IntVec3 target;
                    if (!TryFindShipTarget(map, vehicle, out target)) continue;
                    if (schedule.CommitShot(mission.uniqueID, index, tick, turret))
                    {
                        ConsumeShotFuel(vehicle, turret);
                        FireAtShip(map, vehicle, target);
                    }
                }
                else if (mission.mission == ShipMapComp.ShuttleMission.INTERCEPT)
                {
                    ShipCombatProjectile torpedo = FindTorpedo(map, mission);
                    VehiclePawn shuttle = torpedo == null ? FindShuttle(map, mission) : null;
                    if (torpedo == null && shuttle == null) continue;
                    if (!schedule.CommitShot(mission.uniqueID, index, tick, turret)) continue;
                    ConsumeShotFuel(vehicle, turret);
                    int skill = vehicle.GetGunnerShootingSkill();
                    if (torpedo != null)
                    {
                        if (Rand.Chance(Mathf.Lerp(0.75f, 1.25f, skill / 20f)))
                        {
                            map.TargetMapComp.Projectiles.Remove(torpedo);
                            map.TargetMapComp.TorpsInRange.Remove(torpedo);
                        }
                    }
                    else if (Rand.Chance(Mathf.Lerp(0.85f, 1.15f, skill / 20f) - shuttle.GetPDDodgeChance()))
                    {
                        HitShuttle(map, vehicle, shuttle);
                    }
                }
            }
            return true;
        }

        private static void ConsumeShotFuel(VehiclePawn vehicle, VehicleTurret turret)
        {
            // Same non-torpedo weapon charge used by the N01 ground turret:
            // two fuel per magazine, not two fuel for each of its thirty shots.
            vehicle.CompFueledTravel.ConsumeFuel(2f / Math.Max(1, turret.def.magazineCapacity));
        }

        private static bool TryFindShipTarget(ShipMapComp map, VehiclePawn vehicle, out IntVec3 target)
        {
            target = IntVec3.Invalid;
            Map enemyMap = map.ShipCombatTargetMap;
            if (enemyMap == null) return false;
            if (vehicle.Faction == Faction.OfPlayer && map.TargetMapComp.ShuttleTarget != IntVec3.Invalid
                && map.TargetMapComp.ShuttleTarget.InBounds(enemyMap))
            {
                target = map.TargetMapComp.ShuttleTarget;
                return true;
            }
            List<Building> buildings = vehicle.Faction == Faction.OfPlayer
                ? enemyMap.listerBuildings.allBuildingsNonColonist : enemyMap.listerBuildings.allBuildingsColonist;
            if (buildings.Count == 0) return false;
            target = buildings.RandomElement().Position;
            return true;
        }

        private static void FireAtShip(ShipMapComp map, VehiclePawn vehicle, IntVec3 target)
        {
            Map enemyMap = map.ShipCombatTargetMap;
            IntVec3 spawn = map.FindClosestEdgeCell(enemyMap, target);
            float miss = Mathf.Lerp(1.5f, 0.5f, vehicle.GetGunnerShootingSkill() / 20f) / 4f;
            float angle = (target - spawn).AngleFlat + Rand.Range(-miss, miss);
            IntVec3 destination = spawn + new Vector3(1000f * Mathf.Sin(Mathf.Deg2Rad * angle), 0f,
                1000f * Mathf.Cos(Mathf.Deg2Rad * angle)).ToIntVec3();
            Projectile projectile = (Projectile)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed(ProjectileDefName), spawn, enemyMap);
            projectile.Launch(vehicle, spawn.ToVector3Shifted(), destination, target, ProjectileHitFlags.All);
        }

        private static float DistanceToEnemy(ShipMapComp map, ShipMapComp.ShuttleMissionData mission, float enemyDistance)
        {
            // Both missions/projectiles measure progress from their own home.
            return Mathf.Abs(map.OriginMapComp.Range - mission.rangeTraveled - enemyDistance);
        }

        private static ShipCombatProjectile FindTorpedo(ShipMapComp map, ShipMapComp.ShuttleMissionData mission)
        {
            foreach (ShipCombatProjectile projectile in map.TargetMapComp.Projectiles)
            {
                if (projectile.spawnProjectile == null || projectile.spawnProjectile.thingClass == null) continue;
                Type type = projectile.spawnProjectile.thingClass;
                if (((TorpedoType != null && TorpedoType.IsAssignableFrom(type))
                    || (AntimatterType != null && AntimatterType.IsAssignableFrom(type)))
                    && DistanceToEnemy(map, mission, projectile.range) <= ShipMaximumRange)
                    return projectile;
            }
            return null;
        }

        private static VehiclePawn FindShuttle(ShipMapComp map, ShipMapComp.ShuttleMissionData mission)
        {
            foreach (ShipMapComp.ShuttleMissionData other in map.TargetMapComp.ShuttleMissions)
            {
                if (!other.liftedOffYet || other.shuttle == null || other.shuttle.Destroyed
                    || other.shuttle.Faction == mission.shuttle.Faction) continue;
                if (DistanceToEnemy(map, mission, other.rangeTraveled) <= ShipMaximumRange)
                    return other.shuttle;
            }
            return null;
        }

        private static void HitShuttle(ShipMapComp map, VehiclePawn shooter, VehiclePawn target)
        {
            CompShipHeatShield shield = target.TryGetComp<CompShipHeatShield>();
            CompVehicleHeatNet network = target.TryGetComp<CompVehicleHeatNet>();
            if (shield != null && target.statHandler != null
                && target.statHandler.GetComponentHealth("shieldGenerator") > 10f
                && network != null && network.myNet != null
                && network.myNet.StorageUsed < network.myNet.StorageCapacity)
            {
                Projectile dummy = (Projectile)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(ProjectileDefName));
                shield.HitShield(dummy);
                if (!dummy.Destroyed) dummy.Destroy();
                return;
            }

            ThingDef definition = DefDatabase<ThingDef>.GetNamed(ProjectileDefName);
            target.TakeDamage(new DamageInfo(definition.projectile.damageDef, LaserDamage, instigator: shooter), IntVec2.Zero);
            if (target.statHandler == null) return;
            ShipMapComp.ShuttleMissionData targetMission = null;
            foreach (ShipMapComp.ShuttleMissionData candidate in map.TargetMapComp.ShuttleMissions)
                if (candidate.shuttle == target) { targetMission = candidate; break; }
            if (targetMission == null) return;
            float health = target.statHandler.GetStatValue(VehicleStatDefOf.BodyIntegrity);
            if (health <= 0f)
            {
                FinishDestroyedShuttle(map, target, targetMission);
            }
            else if (health <= targetMission.retreatAtHealth && targetMission.mission != ShipMapComp.ShuttleMission.RETURN)
            {
                Messages.Message((target.Faction == Faction.OfPlayer ? "SoS.ShuttleRetreat" : "SoS.EnemyShuttleRetreat").Translate(),
                    target.Faction == Faction.OfPlayer ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent);
                targetMission.mission = ShipMapComp.ShuttleMission.RETURN;
            }
        }

        private static void FinishDestroyedShuttle(ShipMapComp map, VehiclePawn target, ShipMapComp.ShuttleMissionData mission)
        {
            // Native interception cleanup, scoped to the target of an N01 shot.
            Messages.Message((target.Faction == Faction.OfPlayer ? "SoS.CombatPodDestroyedPlayer" : "SoS.CombatPodDestroyedEnemy").Translate(),
                target.Faction == Faction.OfPlayer ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent);
            map.TargetMapComp.DeRegisterShuttleMission(mission, true);
            map.TargetMapComp.ShuttlesInRange.Remove(target);
            foreach (Pawn pawn in new List<Pawn>(target.AllPawnsAboard))
            {
                target.RemovePawn(pawn);
                if (pawn.Faction == Faction.OfPlayer && (ModSettings_SoS.easyMode || Rand.Chance(0.5f)))
                {
                    HealthUtility.DamageUntilDowned(pawn, false);
                    DropPodUtility.DropThingsNear(DropCellFinder.RandomDropSpot(map.ShipCombatOriginMap), map.OriginMapComp.map,
                        new List<Thing> { pawn });
                }
                else
                {
                    pawn.Kill(new DamageInfo(DamageDefOf.Bomb, 100f));
                    if (pawn.Faction == Faction.OfPlayer && pawn.Corpse != null)
                        DropPodUtility.DropThingsNear(DropCellFinder.RandomDropSpot(map.ShipCombatOriginMap), map.OriginMapComp.map,
                            new List<Thing> { pawn.Corpse });
                }
            }
            foreach (Thing cargo in new List<Thing>(target.GetDirectlyHeldThings())) cargo.Kill();
        }
    }
}
