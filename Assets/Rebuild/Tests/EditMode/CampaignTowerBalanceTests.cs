using NUnit.Framework;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class CampaignTowerBalanceTests
    {
        [Test]
        public void GeometryOnlyTuningPreservesFinalAuthoredStats()
        {
            var tower = new TowerDefinition(1, new Vector3(0,.25f,3), 4f, 3.35f, 2.3f, TargetRule.Nearest,
                maxHp: 75f, area: AttackArea.ConePositiveZ);
            var road = new[] { new Vector3(-4,.25f,0), new Vector3(4,.25f,0) };
            var tuned = TacticalStageRules.Tune(tower, road, 7, applyStatScaling: false);
            Assert.That(tuned.Area, Is.EqualTo(AttackArea.ConeNegativeZ));
            Assert.That(tuned.Damage, Is.EqualTo(3.35f));
            Assert.That(tuned.MaxHp, Is.EqualTo(75f));
            Assert.That(tuned.AttackInterval, Is.EqualTo(2.3f));
        }

        [TestCase(1)] [TestCase(3)] [TestCase(7)]
        public void CampaignSessionDoesNotDoubleScaleInspectorStats(int level)
        {
            var sessionHost = new GameObject("Session"); var towerHost = new GameObject("Tower");
            try
            {
                var session = sessionHost.AddComponent<BattleSession>();
                JsonUtility.FromJsonOverwrite("{\"squadCombat\":true,\"continuousSquadMovement\":true,\"restrictToTacticalRoad\":true,\"displayedLevelNumber\":" + level + "}", session);
                var tower = towerHost.AddComponent<BattleTower>();
                towerHost.transform.position = new Vector3(0,.25f,3);
                JsonUtility.FromJsonOverwrite("{\"damage\":3.35,\"maxHp\":75,\"attackInterval\":2.3,\"range\":4,\"attackArea\":5}", tower);
                var road = new[] { new Vector3(-4,.25f,0), new Vector3(4,.25f,0) };
                var captured = session.CaptureTowerDefinition(tower, 1, road);
                Assert.That(captured.Damage, Is.EqualTo(tower.Damage));
                Assert.That(captured.MaxHp, Is.EqualTo(75f));
                Assert.That(captured.AttackInterval, Is.EqualTo(tower.AttackInterval));
            }
            finally { Object.DestroyImmediate(towerHost); Object.DestroyImmediate(sessionHost); }
        }
    }
}
