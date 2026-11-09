using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Team5.Rebuild.Analytics;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class AnalyticsTests
    {
        private sealed class RecordingSink : IAnalyticsSink
        {
            public readonly List<AnalyticsEvent> Events = new List<AnalyticsEvent>();
            public void Emit(AnalyticsEvent value) => Events.Add(value);
            public List<AnalyticsEvent> Named(string name) => Events.Where(value => value.Name == name).ToList();
        }

        private sealed class ThrowingSink : IAnalyticsSink
        {
            public void Emit(AnalyticsEvent value) => throw new InvalidOperationException("sink failure");
        }

        [TearDown]
        public void ClearSink() => GameAnalytics.SetSink(null);

        [Test]
        public void JsonEscapesStringsAndKeepsUnicode()
        {
            var json = AnalyticsJson.Object(new[]
            {
                new KeyValuePair<string, object>("text", "a\"b\\c\nd\te\u0001f"),
                new KeyValuePair<string, object>("unicode", "\u00e9\u6f22\u2028"),
                new KeyValuePair<string, object>("empty", null)
            });
            Assert.That(json, Is.EqualTo("{\"text\":\"a\\\"b\\\\c\\nd\\te\\u0001f\",\"unicode\":\"\u00e9\u6f22\\u2028\",\"empty\":null}"));
            Assert.That(AnalyticsJson.Batch(new[] { "{\"a\":1}", "{\"b\":2}" }), Is.EqualTo("{\"events\":[{\"a\":1},{\"b\":2}]}"));
        }

        [Test]
        public void JsonNumbersUseInvariantCultureAndRounding()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var json = AnalyticsJson.Object(new[]
                {
                    new KeyValuePair<string, object>("f", 1.5f),
                    new KeyValuePair<string, object>("d", 1234.56789),
                    new KeyValuePair<string, object>("tiny", 0.0004),
                    new KeyValuePair<string, object>("neg", -2.25),
                    new KeyValuePair<string, object>("nan", float.NaN),
                    new KeyValuePair<string, object>("i", 42),
                    new KeyValuePair<string, object>("big", 9000000000L),
                    new KeyValuePair<string, object>("b", true),
                    new KeyValuePair<string, object>("e", UnitRole.Tank)
                });
                Assert.That(json, Is.EqualTo("{\"f\":1.5,\"d\":1234.568,\"tiny\":0,\"neg\":-2.25,\"nan\":null,\"i\":42,\"big\":9000000000,\"b\":true,\"e\":\"Tank\"}"));
            }
            finally { Thread.CurrentThread.CurrentCulture = previous; }
        }

        [Test]
        public void FacadeNeverThrowsAndAddsCommonFields()
        {
            GameAnalytics.SetSink(null);
            Assert.That(GameAnalytics.IsActive, Is.False);
            Assert.DoesNotThrow(() => GameAnalytics.Track("ignored", ("a", 1)));
            Assert.DoesNotThrow(() => GameAnalytics.TrackBattle("ignored"));
            Assert.DoesNotThrow(() => GameAnalytics.Track(null, null));

            GameAnalytics.SetSink(new ThrowingSink());
            Assert.DoesNotThrow(() => GameAnalytics.Track("broken", ("a", 1)));

            var sink = new RecordingSink();
            GameAnalytics.SetSink(sink);
            GameAnalytics.SetLevelContext(3, 2);
            GameAnalytics.Track("probe", ("value", 7), (null, 1), ("value", 8));
            GameAnalytics.Track("probe");
            Assert.That(sink.Events.Count, Is.EqualTo(2));
            var first = sink.Events[0];
            foreach (var key in new[] { "event", "ts", "player_id", "session_id", "run_id", "build", "platform", "is_editor",
                "is_test_run", "scene", "level", "attempt", "seq" })
                Assert.That(first.TryGet(key, out _), Is.True, key);
            Assert.That(first["event"], Is.EqualTo("probe"));
            Assert.That(first["value"], Is.EqualTo(8), "A repeated key overwrites instead of duplicating a column.");
            Assert.That(first.Fields.Count(field => field.Key == "value"), Is.EqualTo(1));
            Assert.That(first["level"], Is.EqualTo(3));
            Assert.That(first["attempt"], Is.EqualTo(2));
            Assert.That(first["is_editor"], Is.True);
            Assert.That((long)sink.Events[1]["seq"], Is.EqualTo((long)first["seq"] + 1));
            Assert.That(first.ToJson(), Does.StartWith("{\"event\":\"probe\",\"ts\":\""));
            Assert.DoesNotThrow(() => DateTime.Parse((string)first["ts"], CultureInfo.InvariantCulture));
            GameAnalytics.SetLevelContext(0, 0);
        }

        [Test]
        public void SettingsAssetShipsWithoutAnEndpoint()
        {
            var settings = Resources.Load<AnalyticsSettings>(AnalyticsSettings.ResourceName);
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Enabled, Is.True);
            Assert.That(settings.EndpointUrl, Is.Empty);
            Assert.That(settings.SendFromEditor, Is.False);
        }

        [Test]
        public void VictoryAttemptReportsOneLevelEndAndTowerRows()
        {
            var sink = new RecordingSink();
            GameAnalytics.SetSink(sink);
            var definition = Definition(new TowerDefinition(1, new Vector3(5, 0, 30), 1, 1, 1, TargetRule.Nearest, maxHp: 30),
                new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)),
                new MemberSpawnDefinition(2, 1, SquadCombatRules.Create(UnitRole.Attack, UnitRarity.Rare, 3)));
            var simulation = new BattleSimulation(definition);
            var tracker = new BattleAnalyticsTracker(simulation, RouteLength(definition), 1, false);
            Run(simulation, tracker);
            tracker.Finish(45, 3, false);
            tracker.Finish(45, 3, false);

            Assert.That(simulation.Outcome, Is.EqualTo(BattleOutcome.Victory));
            var start = sink.Named("level_start").Single();
            Assert.That(start["squad_size"], Is.EqualTo(2));
            Assert.That(start["roles"], Is.EqualTo("Attack:1|Captain:1"));
            Assert.That(start["slots"], Is.EqualTo("1:Attack|4:Captain"));
            Assert.That(start["captain_present"], Is.True);
            Assert.That(start["max_rarity"], Is.EqualTo("Rare"));
            Assert.That(start["tower_count"], Is.EqualTo(1));
            var end = sink.Named("level_end").Single();
            Assert.That(sink.Events.IndexOf(start), Is.LessThan(sink.Events.IndexOf(end)));
            Assert.That(end["outcome"], Is.EqualTo("victory"));
            Assert.That(end["killed"], Is.EqualTo(0));
            Assert.That(end["reached_goal"], Is.EqualTo(simulation.ReachedGoalCount));
            Assert.That(end["survivors"], Is.EqualTo(2));
            Assert.That(end["gold_reward"], Is.EqualTo(45));
            Assert.That(end["furthest_progress_pct"], Is.EqualTo(100f));
            Assert.That((double)end["sim_s_at_1x"], Is.EqualTo(simulation.ElapsedTime).Within(1e-6));
            Assert.That(sink.Named("unit_death"), Is.Empty);
            var tower = sink.Named("tower_stats").Single();
            Assert.That(tower["tower_id"], Is.EqualTo(1));
            Assert.That(tower["destroyed"], Is.False);
            Assert.That(tower["destroyed_at_s"], Is.EqualTo(-1));
            Assert.That(tower["kills"], Is.EqualTo(0));
        }

        [Test]
        public void DefeatAttemptAttributesEveryDeathToTheKillingTower()
        {
            var sink = new RecordingSink();
            GameAnalytics.SetSink(sink);
            var definition = Definition(new TowerDefinition(1, new Vector3(5, 0, 2), 4, 1000, 0.2f, TargetRule.Nearest, maxHp: 100000),
                new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Tank)),
                new MemberSpawnDefinition(2, 3, SquadCombatRules.Create(UnitRole.Heal)));
            var simulation = new BattleSimulation(definition);
            var tracker = new BattleAnalyticsTracker(simulation, RouteLength(definition), 2, false);
            Run(simulation, tracker);
            tracker.Finish(0, 1, false);

            Assert.That(simulation.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            var deaths = sink.Named("unit_death");
            Assert.That(deaths.Count, Is.EqualTo(simulation.KilledCount));
            Assert.That(deaths.Count, Is.EqualTo(2));
            Assert.That(deaths.All(value => (int)value["killer_tower_id"] == 1), Is.True);
            Assert.That(deaths.All(value => (float)value["progress_pct"] > 0 && (float)value["progress_pct"] < 100), Is.True);
            Assert.That(deaths.Select(value => value["role"]), Is.EquivalentTo(new[] { "Tank", "Heal" }));
            var end = sink.Named("level_end").Single();
            Assert.That(end["outcome"], Is.EqualTo("defeat"));
            Assert.That(end["killed"], Is.EqualTo(2));
            Assert.That(end["survivors"], Is.EqualTo(0));
            Assert.That(end["squad_hp_left_pct"], Is.EqualTo(0f));
            Assert.That((float)end["damage_taken"], Is.EqualTo(simulation.Units.Sum(unit => unit.MaxHp)).Within(1e-3));
            Assert.That((float)end["furthest_progress_pct"], Is.GreaterThan(0).And.LessThan(100));
            Assert.That(end["lives_left"], Is.EqualTo(1));
            var tower = sink.Named("tower_stats").Single();
            Assert.That(tower["kills"], Is.EqualTo(2));
            Assert.That((float)tower["damage_dealt"], Is.EqualTo((float)end["damage_taken"]).Within(1e-3));
            Assert.That(sink.Named("tower_stats").Count, Is.EqualTo(simulation.Towers.Count));
        }

        [Test]
        public void AbandonedAttemptReportsOnceAndStopsListening()
        {
            var sink = new RecordingSink();
            GameAnalytics.SetSink(sink);
            var definition = Definition(new TowerDefinition(1, new Vector3(5, 0, 2), 4, 1000, 0.2f, TargetRule.Nearest, maxHp: 100000),
                new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Tank)));
            var simulation = new BattleSimulation(definition);
            var tracker = new BattleAnalyticsTracker(simulation, RouteLength(definition), 1, false);
            simulation.Step(0.02f);
            tracker.Abandon();
            tracker.Finish(0, 3, false);
            for (var i = 0; i < 5000 && simulation.Outcome == BattleOutcome.Running; i++) simulation.Step(0.02f);
            Assert.That(sink.Named("level_abandon").Count, Is.EqualTo(1));
            Assert.That(sink.Named("level_end"), Is.Empty);
            Assert.That(sink.Named("unit_death"), Is.Empty);
        }

        [Test]
        public void MovedMembersCountsOnlyOccupiedSlotsThatChange()
        {
            var definition = Definition(new TowerDefinition(1, new Vector3(5, 0, 30), 1, 1, 1, TargetRule.Nearest),
                new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)),
                new MemberSpawnDefinition(2, 1, SquadCombatRules.Create(UnitRole.Tank)));
            var simulation = new BattleSimulation(definition);
            simulation.Step(0.02f);
            var identity = Enumerable.Range(0, 9).ToArray();
            Assert.That(BattleAnalytics.MovedMembers(simulation, identity), Is.Zero);
            var swap = identity.ToArray();
            swap[4] = 7; swap[7] = 4;
            Assert.That(BattleAnalytics.MovedMembers(simulation, swap), Is.EqualTo(1));
            Assert.That(BattleAnalytics.MovedMembers(simulation, null), Is.Zero);
        }

        private static BattleDefinition Definition(TowerDefinition tower, params MemberSpawnDefinition[] members) =>
            new BattleDefinition(new[] { Vector3.zero, new Vector3(10, 0, 0) }, new[] { tower }, members);

        private static float RouteLength(BattleDefinition definition)
        {
            var length = 0f;
            for (var i = 1; i < definition.Waypoints.Count; i++) length += Vector3.Distance(definition.Waypoints[i - 1], definition.Waypoints[i]);
            return length;
        }

        private static void Run(BattleSimulation simulation, BattleAnalyticsTracker tracker)
        {
            for (var i = 0; i < 5000 && simulation.Outcome == BattleOutcome.Running; i++)
            {
                simulation.Step(0.02f);
                tracker.Sample(1);
            }
        }
    }
}
