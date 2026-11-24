using System.Collections;
using NUnit.Framework;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class MinionGameplayRepairTests
    {
        private GameObject root;
        private MinionMaterials materials;
        private static Vector3 At(float x, float z = 0f) => new Vector3(x, .25f, z);
        private BattleSimulation Create(UnitRole role, float towerHp, out MinionPost post)
        {
            root = new GameObject("Minion gameplay regression");
            var enemy = new GameObject("Lobber");
            enemy.transform.SetParent(root.transform, false);
            enemy.transform.position = new Vector3(2, .75f, 0);
            var tower = enemy.AddComponent<BattleTower>();
            var look = new GameObject("Look");
            look.transform.SetParent(enemy.transform, false);
            var bot = new GameObject("Bot").transform;
            bot.SetParent(look.transform, false);
            post = new MinionPost(tower, MinionKind.Lobber, 0, new MinionRig(look, bot, null, null));
            var sim = new BattleSimulation(new BattleDefinition(new[] { At(0), At(20) },
                new[] { new TowerDefinition(1, enemy.transform.position, 0, 0, 1, TargetRule.Nearest,
                    maxHp: towerHp, isMinion: true) },
                new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(role)) }));
            sim.EnableContinuousMovement();
            tower.BindRuntimeState(sim.Towers[0]);
            sim.Step(.02f);
            return sim;
        }

        [UnityTest]
        public IEnumerator LobberWarnsOnTargetGroundThenDamagesAndClearsEffects()
        {
            var sim = Create(UnitRole.Captain, 100f, out var post);
            materials = new MinionMaterials();
            var lobber = new MinionLobber(root.transform, post, 2f, materials);
            var effects = root.transform.Find("Lobber Effects");
            var ring = effects.Find("Warning Ring");
            var bomb = effects.Find("Paint Bomb");
            var splat = effects.Find("Paint Splat");
            var hp = sim.Units[0].Hp;
            int source = -99;
            sim.UnitDamaged += value => source = value.TowerId;
            lobber.Tick(sim, 0f, .02f);
            for (var i = 0; i < 100 && !ring.gameObject.activeSelf; i++)
            {
                sim.Step(.02f); lobber.Tick(sim, (float)sim.ElapsedTime, .02f);
            }
            Assert.That(ring.gameObject.activeSelf, Is.True);
            Assert.That(bomb.gameObject.activeSelf, Is.True);
            Assert.That(ring.position.y, Is.EqualTo(sim.Units[0].Position.y + .03f).Within(.001f));
            sim.SetPaused(true);
            var bombPosition = bomb.position;
            sim.Step(3f); lobber.Tick(sim, 99f, 3f);
            Assert.That(bomb.position, Is.EqualTo(bombPosition));
            Assert.That(sim.Units[0].Hp, Is.EqualTo(hp));
            sim.SetPaused(false);
            for (var i = 0; i < 150 && ring.gameObject.activeSelf; i++)
            {
                sim.Step(.02f); lobber.Tick(sim, (float)sim.ElapsedTime, .02f);
            }
            Assert.That(sim.Units[0].Hp, Is.LessThan(hp));
            Assert.That(source, Is.EqualTo(1));
            Assert.That(ring.gameObject.activeSelf, Is.False);
            Assert.That(bomb.gameObject.activeSelf, Is.False);
            Assert.That(splat.gameObject.activeSelf, Is.True);
            sim.ForceTimeLimitDefeat(); lobber.Tick(sim, 0f, .02f);
            Assert.That(splat.gameObject.activeSelf, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DefeatedLobberModelIsHiddenAndRestoreReactivatesIt()
        {
            var sim = Create(UnitRole.Sniper, .1f, out var post);
            Assert.That(post.Rig.Look.activeSelf, Is.True);
            for (var i = 0; i < 100 && sim.Towers[0].IsAlive; i++) sim.Step(.02f);
            Assert.That(sim.Towers[0].IsAlive, Is.False);
            post.Animate(1f, .02f, .02f, 0);
            Assert.That(post.Rig.Look.activeSelf, Is.False);
            var next = new BattleSimulation(new BattleDefinition(new[] { At(0), At(20) },
                new[] { sim.Towers[0].Definition },
                new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)) }));
            post.Tower.BindRuntimeState(next.Towers[0]); post.Restore();
            Assert.That(post.Rig.Look.activeSelf, Is.True);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (materials != null) materials.Dispose();
            if (root != null) Object.Destroy(root);
            materials = null; root = null;
            yield return null;
        }
    }
}
