using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class SquadCombatTests
    {
        private static readonly Vector3[] Route = { new Vector3(0, 0.25f, 0), new Vector3(100, 0.25f, 0) };
        private static MemberSpawnDefinition Member(int id, int slot, UnitRole role = UnitRole.Attack, float at = 0)
            => new MemberSpawnDefinition(id, slot, SquadCombatRules.Create(role), at);
        private static UnitDefinition Passive(float hp = 4) => new UnitDefinition(hp, 0.01f);
        private static TowerDefinition Tower(Vector3 position, float range = 10, float damage = 1, float interval = 100,
            float hp = 20, AttackArea area = AttackArea.Square, float width = 0.3f, int id = 1)
            => new TowerDefinition(id, position, range, damage, interval, TargetRule.Nearest, 1, hp, area, width);
        private static BattleSimulation Make(IEnumerable<MemberSpawnDefinition> members, params TowerDefinition[] towers)
            => new BattleSimulation(new BattleDefinition(Route, towers, members));
        private static void Tick(BattleSimulation sim, int count)
        { for (var i = 0; i < count; i++) sim.Step(0.02f); }

        [Test]
        public void CaptainUsesConfiguredStatsAndExistingProjectileCombat()
        {
            var stats = SquadCombatRules.Create(UnitRole.Captain);
            Assert.That((stats.MaxHp, stats.Defense, stats.Damage), Is.EqualTo((6f, 0.5f, 0.5f)));
            var sim = Make(new[] { Member(110, 4, UnitRole.Captain) }, Tower(new Vector3(0, 0.25f, 2)));
            Tick(sim, 40);
            Assert.That(sim.Units[0].Hp, Is.EqualTo(5.5f).Within(0.0001));
            Assert.That(sim.Towers[0].Hp, Is.EqualTo(19.5f).Within(0.0001));
        }

        [Test]
        public void LegacyRoleValuesComeFromVerifiedCode()
        {
            var attack = SquadCombatRules.Create(UnitRole.Attack);
            Assert.That((attack.MaxHp, attack.Damage, attack.Range, attack.AttackInterval, attack.Speed), Is.EqualTo((4f, 1f, 2.5f, 1f, 0.5f)));
            Assert.That(SquadCombatRules.Create(UnitRole.Tank).Speed, Is.EqualTo(0.5f));
            Assert.That(SquadCombatRules.Create(UnitRole.Heal).Speed, Is.EqualTo(0.5f));
            Assert.That(SquadCombatRules.Create(UnitRole.Tank).Defense, Is.EqualTo(0.2f));
            Assert.That(SquadCombatRules.Create(UnitRole.Tank).MaxHp, Is.EqualTo(8));
            Assert.That(SquadCombatRules.Create(UnitRole.Heal).MaxHp, Is.EqualTo(3));
            Assert.That(SquadCombatRules.HealInterval, Is.EqualTo(2));
            Assert.That(SquadCombatRules.HealAmount, Is.EqualTo(0.4f));
        }

        [Test]
        public void SwapPreservesIdentityHpCooldownAndLocksProjectileToOriginalSlot()
        {
            var sim = Make(new[] { Member(104, 4), Member(101, 1, UnitRole.Tank) }, Tower(new Vector3(0, 0.25f, 2)));
            sim.Step(0.02f);
            var attack = sim.Units.Single(u => u.MemberId == 104);
            var tank = sim.Units.Single(u => u.MemberId == 101);
            var attackCooldown = attack.AttackCooldown;
            Assert.That(sim.Projectiles.Single(p => p.TargetKind == ProjectileTargetKind.FormationSlot).TargetId, Is.EqualTo(4));
            sim.SetPaused(true);
            var swap = Enumerable.Range(0, 9).ToArray(); swap[4] = 1; swap[1] = 4;
            Assert.That(sim.TryApplyFormation(swap), Is.True);
            Assert.That(sim.GetMemberAtSlot(1), Is.SameAs(attack));
            Assert.That(sim.GetMemberAtSlot(4), Is.SameAs(tank));
            Assert.That((attack.Hp, attack.AttackCooldown), Is.EqualTo((4f, attackCooldown)));
            sim.SetPaused(false); Tick(sim, 25);
            Assert.That(attack.Hp, Is.EqualTo(4));
            Assert.That(tank.Hp, Is.EqualTo(7.2f).Within(0.0001));
        }

        [Test]
        public void EmptyLockedSlotMissesAndDeadMemberNeverRevives()
        {
            var sim = Make(new[] { Member(77, 4, UnitRole.Tank) }, Tower(new Vector3(0, 0.25f, 2)));
            sim.Step(0.02f); sim.SetPaused(true);
            var swap = Enumerable.Range(0, 9).ToArray(); swap[4] = 1; swap[1] = 4;
            Assert.That(sim.TryApplyFormation(swap), Is.True);
            sim.SetPaused(false); Tick(sim, 30);
            Assert.That(sim.Units[0].Hp, Is.EqualTo(8));
            Assert.That(sim.GetMemberAtSlot(4), Is.Null);
        }

        [Test]
        public void SwappingAnAlreadyInjuredMemberDoesNotResetItsHpOrCooldown()
        {
            var sim = Make(new[] { Member(104, 4), Member(101, 1, UnitRole.Tank) }, Tower(new Vector3(0, 0.25f, 2)));
            Tick(sim, 20);
            var attack = sim.Units.Single(u => u.MemberId == 104);
            Assert.That(attack.Hp, Is.EqualTo(3));
            var before = (attack.MemberId, attack.Hp, attack.AttackCooldown, attack.RouteProgress);
            sim.SetPaused(true);
            var swap = Enumerable.Range(0, 9).ToArray(); swap[4] = 1; swap[1] = 4;
            Assert.That(sim.TryApplyFormation(swap), Is.True);
            Assert.That((attack.MemberId, attack.Hp, attack.AttackCooldown, attack.RouteProgress), Is.EqualTo(before));
        }

        [Test]
        public void PauseFreezesSpawnMovementShotsCooldownAndHealingTime()
        {
            var sim = Make(new[] { Member(1, 4), Member(2, 0, UnitRole.Heal, 1) }, Tower(new Vector3(0, 0.25f, 2)));
            sim.Step(0.02f); sim.SetPaused(true);
            var unit = sim.Units[0]; var shot = sim.Projectiles[0];
            var before = (unit.Position, unit.Hp, unit.AttackCooldown, shot.Position, sim.ElapsedTime, sim.PendingSpawnCount);
            Tick(sim, 200);
            Assert.That((unit.Position, unit.Hp, unit.AttackCooldown, shot.Position, sim.ElapsedTime, sim.PendingSpawnCount), Is.EqualTo(before));
            sim.SetPaused(false); Tick(sim, 60);
            Assert.That(sim.PendingSpawnCount, Is.Zero);
        }

        [Test]
        public void FirstArrivalWinsOnceAndCancelsPendingGeneration()
        {
            var members = new[] { new MemberSpawnDefinition(33, 4, new UnitDefinition(4, 100)), new MemberSpawnDefinition(44, 0, new UnitDefinition(4, 100), 10) };
            var sim = new BattleSimulation(new BattleDefinition(new[] { Vector3.zero, Vector3.right }, Array.Empty<TowerDefinition>(), members));
            var ended = 0; var arrived = 0;
            sim.BattleEnded += _ => ended++;
            sim.UnitReachedGoal += _ => { arrived++; Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Victory)); };
            Tick(sim, 1000);
            Assert.That((ended, arrived, sim.SpawnedCount), Is.EqualTo((1, 1, 1)));
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void NoLivingMembersDoesNotLoseWhileAnotherMemberIsScheduled()
        {
            var members = new[] { new MemberSpawnDefinition(11, 4, Passive(1)), new MemberSpawnDefinition(22, 0, Passive(1), 2) };
            var sim = Make(members, Tower(new Vector3(0, 0.25f, 0), damage: 10, interval: 1));
            var ends = 0; sim.BattleEnded += _ => ends++;
            Tick(sim, 10);
            Assert.That((sim.AliveCount, sim.PendingSpawnCount), Is.EqualTo((0, 1)));
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Running));
            Tick(sim, 180);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That((sim.KilledCount, ends), Is.EqualTo((2, 1)));
        }

        [Test]
        public void DelayedMembersJoinTheMovingAnchorWithoutBreakingFormation()
        {
            var sim = Make(new[] { Member(1, 4), Member(2, 0, UnitRole.Tank, 0.5f) });
            Tick(sim, 40);
            Assert.That((sim.Units[0].AnchorPosition - sim.Units[1].AnchorPosition).magnitude, Is.LessThan(0.0001));
            Assert.That(sim.Units.Select(u => u.OccupiedCell).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void AllEightNeighborsAreHealedIncludingDiagonalsAtSqrtTwoWorldDistance()
        {
            var members = Enumerable.Range(0, 9).Select(slot => new MemberSpawnDefinition(100 + slot, slot,
                slot == 4 ? new UnitDefinition(3, 0.01f, UnitRole.Heal) : Passive())).ToArray();
            var towers = Enumerable.Range(0, 9).Select(slot => Tower(Route[0] + SquadCombatRules.Offset(slot), range: 0.1f, id: slot + 1)).ToArray();
            var sim = Make(members, towers);
            var heals = new List<HealEvent>(); sim.UnitHealed += heals.Add;
            Tick(sim, 99);
            Assert.That(heals, Is.Empty);
            sim.Step(0.02f);
            Assert.That(heals.Count, Is.EqualTo(8));
            Assert.That(heals.All(h => h.Amount == SquadCombatRules.HealAmount), Is.True);
            Assert.That(sim.Units.Where(u => u.SlotId != 4).All(u => Mathf.Abs(u.Hp - 3.4f) < 0.0001), Is.True);
            Assert.That(sim.GetMemberAtSlot(4).Hp, Is.EqualTo(2), "Healer does not heal itself.");
            Assert.That(Vector3.Distance(sim.GetMemberAtSlot(4).Position, sim.GetMemberAtSlot(0).Position), Is.GreaterThan(1));
        }

        [Test]
        public void HealingDoesNotReachNonNeighborsOverhealOrResurrect()
        {
            var members = new[] { new MemberSpawnDefinition(1, 0, new UnitDefinition(3, 0.01f, UnitRole.Heal)),
                new MemberSpawnDefinition(2, 8, Passive()), new MemberSpawnDefinition(3, 1, Passive(0.5f)),
                new MemberSpawnDefinition(4, 3, Passive()) };
            var sim = Make(members,
                Tower(Route[0] + SquadCombatRules.Offset(8), range: 0.1f, id: 1),
                Tower(Route[0] + SquadCombatRules.Offset(1), range: 0.1f, id: 2));
            Tick(sim, 220);
            Assert.That(sim.Units.Single(u => u.MemberId == 2).Hp, Is.EqualTo(3));
            Assert.That(sim.Units.Single(u => u.MemberId == 3).LifeState, Is.EqualTo(UnitLifeState.Dead));
            Assert.That(sim.Units.Single(u => u.MemberId == 4).Hp, Is.EqualTo(4));
        }

        [Test]
        public void AttackTargetsNearestTowerAndClampsDamageToRemainingHp()
        {
            var sim = Make(new[] { Member(99, 4) },
                Tower(new Vector3(0, 0.25f, 2), range: 0, hp: 0.5f),
                Tower(new Vector3(0, 0.25f, 3), range: 0, id: 2));
            var deaths = 0; var damage = new List<TowerDamageEvent>();
            sim.TowerDied += _ => deaths++; sim.TowerDamaged += damage.Add;
            sim.Step(0.02f);
            Assert.That(sim.Towers[0].Hp, Is.EqualTo(0.5f), "Shots have travel time.");
            Assert.That(sim.Projectiles.Single().TargetId, Is.EqualTo(1));
            Tick(sim, 25);
            Assert.That((sim.Towers[0].Hp, deaths), Is.EqualTo((0f, 1)));
            Assert.That(damage.Single().Amount, Is.EqualTo(0.5f));
            Assert.That(sim.Towers[1].Hp, Is.EqualTo(20));
        }

        [Test]
        public void DestroyingAllTowersIsNotAVictoryAndShotsDoNotOverkill()
        {
            var sim = Make(new[] { Member(1, 4), Member(2, 1) }, Tower(new Vector3(0, 0.25f, 1), range: 0, hp: 1));
            var deaths = 0; sim.TowerDied += _ => deaths++;
            Tick(sim, 60);
            Assert.That(sim.Towers[0].IsAlive, Is.False);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Running));
        }

        [Test]
        public void DirectionalGeometryAndFastProjectileSweepRespectNarrowLane()
        {
            Assert.That(SquadCombatRules.Contains(new Vector3(1, 0, 0.2f), AttackArea.PositiveX, 3, 0.3f), Is.False);
            Assert.That(SquadCombatRules.Contains(new Vector3(-1, 0, 0), AttackArea.PositiveX, 3, 0.3f), Is.False);
            var sim = Make(new[] { new MemberSpawnDefinition(8, 4, Passive()) },
                Tower(new Vector3(-0.1f, 0.25f, 0), range: 3, area: AttackArea.PositiveX));
            sim.Step(0.02f);
            Assert.That(sim.Units[0].Hp, Is.EqualTo(3), "Swept hit must not skip a nearby target.");
        }

        [Test]
        public void IllegalFormationIsRejectedAtomicallyAndNotAllowedWhileRunning()
        {
            var sim = Make(new[] { Member(55, 4) }); sim.Step(0.02f);
            var permutation = Enumerable.Range(0, 9).ToArray();
            Assert.That(sim.TryApplyFormation(permutation), Is.False);
            sim.SetPaused(true); permutation[0] = 1;
            Assert.That(sim.TryApplyFormation(permutation), Is.False);
            Assert.That(sim.Units[0].SlotId, Is.EqualTo(4));
            Assert.Throws<ArgumentException>(() => Make(new[] { Member(1, 4), Member(1, 0) }));
            Assert.Throws<ArgumentException>(() => Make(new[] { Member(1, 4), Member(2, 4) }));
            Assert.Throws<ArgumentException>(() => Make(Array.Empty<MemberSpawnDefinition>()));
        }

        [Test]
        public void SlotTrajectoriesDetectBlockedTurnEvenWhenCenterlineIsClear()
        {
            var route = new[] { new Vector3(1, 0.25f, 1), new Vector3(3, 0.25f, 1), new Vector3(3, 0.25f, 3) };
            var cells = new HashSet<Vector3Int>();
            for (var segment = 1; segment < route.Length; segment++)
            for (var slot = 0; slot < 9; slot++)
                foreach (var cell in FormationClearance.SweptCells(route[segment - 1] + SquadCombatRules.Offset(slot), route[segment] + SquadCombatRules.Offset(slot))) cells.Add(cell);
            var members = Enumerable.Range(0, 9).Select(slot => Member(100 + slot, slot)).ToArray();
            Assert.DoesNotThrow(() => new BattleSimulation(new BattleDefinition(route, Array.Empty<TowerDefinition>(), members, cells)));
            Assert.That(cells.Remove(new Vector3Int(4, 0, 2)), Is.True);
            Assert.Throws<ArgumentException>(() => new BattleSimulation(new BattleDefinition(route, Array.Empty<TowerDefinition>(), members, cells)));
            Assert.Throws<ArgumentException>(() => FormationClearance.SweptCells(Vector3.zero, new Vector3(1, 0, 1)).ToArray());
        }

        [Test]
        public void CallbacksCannotReenterTicksOrMutateFormation()
        {
            var sim = Make(new[] { Member(1, 4) });
            sim.UnitSpawned += _ =>
            {
                Assert.Throws<InvalidOperationException>(() => sim.Step(0.02f));
                Assert.Throws<InvalidOperationException>(() => sim.SetPaused(true));
                Assert.That(sim.TryApplyFormation(Enumerable.Range(0, 9).ToArray()), Is.False);
            };
            sim.Step(0.02f);
        }
    }
}
