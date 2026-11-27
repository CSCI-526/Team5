using NUnit.Framework;
using UnityEngine;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;

namespace Team5.Rebuild.Tests
{
    public sealed class CampaignMinionBalanceTests
    {
        [TestCase(3, .70f)]
        [TestCase(4, .85f)]
        [TestCase(5, .95f)]
        [TestCase(6, 1.05f)]
        [TestCase(7, 1.15f)]
        public void BombDamageScalesButStaysBelowQuarterOfFreshCommonSniperHp(int stage, float expected)
        {
            var damage = MinionRules.BombDamage(stage);
            var sniper = SquadCombatRules.Create(UnitRole.Sniper, UnitRarity.Common, stage - 1);
            Assert.That(damage, Is.EqualTo(expected).Within(.0001f));
            Assert.That(damage, Is.LessThanOrEqualTo(sniper.MaxHp * .25f));
        }

        [Test]
        public void BombCounterplayHasSlowerLargerShotsAndUnchangedShieldStrength()
        {
            Assert.That(MinionRules.BombReach, Is.EqualTo(7f));
            Assert.That(MinionRules.BombWarning, Is.EqualTo(2.4f));
            Assert.That(MinionRules.BombRadius, Is.EqualTo(1.25f));
            Assert.That(MinionRules.BombInterval, Is.EqualTo(5f));
            Assert.That(MinionRules.ShieldDamageScale, Is.EqualTo(.3f));
            Assert.That(MinionRules.Hp(MinionKind.Lobber, 7), Is.EqualTo(8f));
            Assert.That(MinionRules.BearerDamage(7), Is.EqualTo(.60f));
            Assert.That(MinionRules.HasMinions(1), Is.False);
        }
        [Test]
        public void StraightMarchAtWideRoadSpeedRemainsInBlastButTurningEscapes()
        {
            const float speed = .96f;
            var position = Vector3.zero;
            var previous = position - Vector3.right * speed * .1f;
            var origin = new Vector3(0f, 0f, 3f);
            var aim = MinionBombRules.PredictTarget(position, previous, .1f, origin, MinionRules.BombReach);
            Assert.That(aim.x, Is.EqualTo(speed * MinionRules.BombPredictionSeconds).Within(.001f));
            var straight = position + Vector3.right * speed * MinionRules.BombWarning;
            var turn = position + Vector3.forward * 1.2f; // reachable lateral reposition on five-wide road
            var radiusSquared = MinionRules.BombRadius * MinionRules.BombRadius;
            Assert.That(MinionBombRules.FlatDistanceSquared(straight, aim), Is.LessThan(radiusSquared));
            Assert.That(MinionBombRules.FlatDistanceSquared(turn, aim), Is.GreaterThan(radiusSquared));
            Assert.That(MinionBombRules.FlatDistanceSquared(straight + Vector3.forward * .5f, aim), Is.LessThan(radiusSquared));
        }

        [Test]
        public void StraightSixUnitFormationKeepsRearRowInsideLockedBlast()
        {
            const float speed = .96f;
            const float spacing = .86f;
            var origin = new Vector3(0f, 0f, 3f);
            var units = new Vector3[6];
            var predicted = new Vector3[6];
            for (var i = 0; i < units.Length; i++)
            {
                units[i] = new Vector3((i / 3 - .5f) * spacing, 0f, (i % 3 - 1) * spacing);
                predicted[i] = MinionBombRules.PredictTarget(units[i], units[i] - Vector3.right * speed * .1f,
                    .1f, origin, MinionRules.BombReach);
            }
            Assert.That(MinionBombRules.PickTarget(predicted, origin, MinionRules.BombReach,
                MinionRules.BombRadius, out var aim), Is.EqualTo(6));
            var radiusSquared = MinionRules.BombRadius * MinionRules.BombRadius;
            var rearHits = 0;
            var straightHits = 0;
            for (var i = 0; i < units.Length; i++)
            {
                var straight = units[i] + Vector3.right * speed * MinionRules.BombWarning;
                if (MinionBombRules.FlatDistanceSquared(straight, aim) <= radiusSquared)
                {
                    straightHits++;
                    if (i < 3) rearHits++;
                }
            }
            Assert.That(rearHits, Is.EqualTo(3));
            Assert.That(straightHits, Is.GreaterThanOrEqualTo(3));
            // Holding or changing direction is a valid alternative to blindly marching.
            var holdingHits = 0;
            foreach (var unit in units)
                if (MinionBombRules.FlatDistanceSquared(unit, aim) <= radiusSquared) holdingHits++;
            Assert.That(holdingHits, Is.LessThan(straightHits));
        }

        [Test]
        public void PredictionClipsAtReachAndDoesNotExtrapolateHeight()
        {
            var position = new Vector3(6.8f, .25f, 0f);
            var aim = MinionBombRules.PredictTarget(position, new Vector3(6.7f, 0f, 0f), .1f, Vector3.zero, 7f);
            Assert.That(aim.x, Is.GreaterThan(6.99f).And.LessThanOrEqualTo(7f));
            Assert.That(aim.y, Is.EqualTo(position.y));
        }

        [Test]
        public void StationaryTargetsStayStationaryAndSpeedSpikesAreCapped()
        {
            var position = Vector3.zero;
            Assert.That(MinionBombRules.PredictTarget(position, position, .1f, position, 7f), Is.EqualTo(position));
            var aim = MinionBombRules.PredictTarget(position, Vector3.left * 5f, .01f, position, 7f);
            Assert.That(aim.magnitude, Is.EqualTo(MinionRules.BombMaxLead).Within(.001f));
        }
    }
}
