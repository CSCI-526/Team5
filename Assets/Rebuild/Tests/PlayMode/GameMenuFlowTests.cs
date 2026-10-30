using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Session;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Team5.Rebuild.Tests
{
    public sealed class GameMenuFlowTests
    {
        [UnityTest]
        public IEnumerator TutorialPauseSkipAndReplayRestoreControlsWithoutChangingCaptain()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.BattleScene);
            var session = Object.FindFirstObjectByType<BattleSession>();
            session.enabled = false;
            if (session.Simulation.Units.Count == 0) session.Advance(0.02f);
            var tutorial = session.GetComponent<LevelOneTutorial>();
            var hud = Object.FindFirstObjectByType<LegacyBattleHud>();
            var inspection = Object.FindFirstObjectByType<BattleUnitInspection>();
            var camera = Camera.main;
            var pose = camera.transform.position;
            var size = camera.orthographicSize;
            var fov = camera.fieldOfView;
            var state = (session.Simulation.Units[0].Id, session.Simulation.Units[0].Hp, session.Simulation.Units[0].SlotId);
            for (var i = 0; i < 300 && tutorial.CurrentStep != 1; i++) yield return null;
            Assert.That(tutorial.CurrentStep, Is.EqualTo(1));
            Assert.That(session.Simulation.IsPaused, Is.True);
            Assert.That(hud.TutorialOwnsPause, Is.True);
            Assert.That(inspection.IsInputSuppressed, Is.True);
            hud.TogglePause();
            Assert.That(session.Simulation.IsPaused, Is.True);
            tutorial.Skip();
            Assert.That(session.Simulation.IsPaused, Is.False);
            Assert.That(hud.TutorialOwnsPause, Is.False);
            Assert.That(inspection.IsInputSuppressed, Is.False);
            Assert.That(camera.transform.position, Is.EqualTo(pose));
            Assert.That(camera.orthographicSize, Is.EqualTo(size));
            Assert.That(camera.fieldOfView, Is.EqualTo(fov));
            Assert.That((session.Simulation.Units[0].Id, session.Simulation.Units[0].Hp, session.Simulation.Units[0].SlotId), Is.EqualTo(state));
            tutorial.Replay(); session.Advance(0.02f);
            for (var i = 0; i < 300 && tutorial.CurrentStep != 1; i++) yield return null;
            Assert.That(tutorial.CurrentStep, Is.EqualTo(1));
            tutorial.enabled = false;
            Assert.That(session.Simulation.IsPaused, Is.False);
            Assert.That(hud.TutorialOwnsPause, Is.False);
            Assert.That(inspection.IsInputSuppressed, Is.False);
        }

        private static IEnumerator WaitForCaptainBattle()
        {
            yield return WaitForScene(BattleSceneFlow.BattleScene);
            Assert.That(Object.FindFirstObjectByType<LegacyUpgradeScene>(), Is.Null);
            var session = Object.FindFirstObjectByType<BattleSession>();
            if (session.Simulation.Units.Count == 0) session.Advance(0.02f);
            Assert.That(session.UsesSceneRosterOnly, Is.True);
            Assert.That(session.Simulation.Units.Count, Is.EqualTo(1));
            var captain = session.Simulation.Units.Single();
            Assert.That(captain.Role, Is.EqualTo(UnitRole.Captain));
            Assert.That(captain.SlotId, Is.EqualTo(4));
            Assert.That(captain.MaxHp, Is.EqualTo(6));
            Assert.That(session.Simulation.PendingSpawnCount, Is.Zero);
            Assert.Throws<System.InvalidOperationException>(() => session.SetPreparedMembers(
                new[] { new MemberSpawnDefinition(101, 0, SquadCombatRules.Create(UnitRole.Attack)) }));
        }

        private static IEnumerator WaitForScene(string path)
        {
            for (var frame = 0; frame < 600 && SceneManager.GetActiveScene().path != path; frame++) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(path));
            yield return null; // Let Start and the asynchronous load finish.
            if (path == BattleSceneFlow.BattleScene)
            {
                var tutorial = Object.FindFirstObjectByType<LevelOneTutorial>();
                if (tutorial != null) tutorial.enabled = false;
            }
        }

        private static void ConfigureOutcome(BattleSession session, bool victory)
        {
            var tutorial = session.GetComponent<LevelOneTutorial>();
            if (tutorial != null) tutorial.enabled = false;
            session.enabled = false;
            typeof(BattleSession).GetField("logEvents", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, false);
            foreach (var tower in session.Towers)
            {
                typeof(BattleTower).GetField("damage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(tower, 1000f);
                typeof(BattleTower).GetField("range", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(tower, victory ? 0f : 1000f);
                typeof(BattleTower).GetField("attackInterval", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(tower, 0.02f);
            }
            session.StartBattle();
        }

        private static void Finish(BattleSession session)
        {
            for (var tick = 0; tick < 10000 && session.Simulation.Outcome == BattleOutcome.Running; tick++) session.Advance(0.02f);
            Assert.That(session.Simulation.Outcome, Is.Not.EqualTo(BattleOutcome.Running));
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            foreach (var flow in Object.FindObjectsByType<BattleSceneFlow>(FindObjectsSortMode.None)) Object.Destroy(flow.gameObject);
            yield return null;
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Menu Flow Cleanup"));
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
        }

        [UnityTest]
        public IEnumerator TitleLevelOneSettlementUpgradeLevelTwoEndAndNewRun()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.StartScene);
            var start = Object.FindFirstObjectByType<Button>();
            start.onClick.Invoke(); start.onClick.Invoke(); // A double click must not create a second run.
            Assert.That(Object.FindObjectsByType<BattleSceneFlow>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return WaitForCaptainBattle();
            var session = Object.FindFirstObjectByType<BattleSession>();
            ConfigureOutcome(session, true);
            Finish(session);
            Assert.That(session.Simulation.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BattleSceneFlow.BattleScene), "Transition waits until the tick returns.");
            yield return WaitForScene(BattleSceneFlow.SettlementScene);
            Assert.That(BattleSceneFlow.LastOutcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(BattleSceneFlow.NextBattleLevelNumber, Is.EqualTo(2));
            Assert.That(BattleSceneFlow.GoldBalance, Is.EqualTo(10));
            Assert.That(BattleSceneFlow.PendingUnitRewardCount, Is.EqualTo(1));
            Assert.That(BattleSceneFlow.LastRewardGold, Is.EqualTo(10));
            Assert.That(BattleSceneFlow.LastRewardUnits, Is.EqualTo(new[] { UnitRole.Tank }));
            var rewardTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Select(label => label.text).ToArray();
            Assert.That(rewardTexts, Does.Contain("+10 GOLD"));
            Assert.That(rewardTexts, Does.Contain("+1 TANK UNIT"));
            Assert.That(BattleSceneFlow.TrySpendGold(1), Is.False, "Currency can only be spent in UpgradeScene.");
            Object.FindFirstObjectByType<Button>().onClick.Invoke();
            yield return WaitForScene(BattleSceneFlow.UpgradeScene);
            var upgrade = Object.FindFirstObjectByType<LegacyUpgradeScene>();
            Assert.That(upgrade.LevelLabel.text, Does.StartWith("LEVEL 2").And.EndWith("3/3"));
            Assert.That(upgrade.LevelLabel.transform.Find("Heart Icon"), Is.Not.Null);
            Assert.That(upgrade.GoldLabel.text, Does.EndWith("10"));
            var tankReward = upgrade.Preparation.Members.Single(member => member.Unit.Role == UnitRole.Tank);
            Assert.That(tankReward.MemberId, Is.Not.EqualTo(110));
            Assert.That(upgrade.Roster.CaptureMembers().Count, Is.EqualTo(1), "Reward must not modify the asset.");
            var rewardCount = upgrade.Preparation.Members.Count;
            BattleSceneFlow.PrepareSquad(upgrade.Roster);
            Assert.That(upgrade.Preparation.Members.Count, Is.EqualTo(rewardCount), "Reward was granted twice.");
            Assert.That(BattleSceneFlow.PendingUnitRewardCount, Is.Zero);
            Assert.That(upgrade.GoldLabel.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(BattleSceneFlow.TrySpendGold(3), Is.True);
            Assert.That(BattleSceneFlow.TrySpendGold(8), Is.False);
            yield return null;
            Assert.That(upgrade.GoldLabel.text, Does.EndWith("7"));
            Assert.That(upgrade.Preparation.TryAssign(110, 4), Is.True);
            Assert.That(upgrade.Preparation.TryAssign(tankReward.MemberId, 7), Is.True);
            upgrade.StartWave(); upgrade.StartWave();
            yield return WaitForScene(BattleSceneFlow.LevelTwoScene);
            session = Object.FindFirstObjectByType<BattleSession>();
            Assert.That(session.DisplayedLevelNumber, Is.EqualTo(2));
            Assert.That(session.AttemptsRemaining, Is.EqualTo(BattleSession.DefaultLivesPerLevel));
            Assert.That(BattleSceneFlow.GoldBalance, Is.EqualTo(7));
            Assert.That(BattleSceneFlow.TrySpendGold(1), Is.False);
            Assert.That(session.GetComponent<LevelOneTutorial>(), Is.Null);
            if (session.Simulation.Units.Count == 0) session.Advance(0.02f);
            Assert.That(session.Simulation.Units.Select(unit => unit.Id), Is.EquivalentTo(new[] { 110, tankReward.MemberId }));
            ConfigureOutcome(session, true);
            Finish(session);
            yield return WaitForScene(BattleSceneFlow.EndScene);
            Assert.That(BattleSceneFlow.GoldBalance, Is.EqualTo(107));
            Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Any(label => label.text.Trim() == "Thanks for playing"), Is.True);
            Object.FindFirstObjectByType<Button>().onClick.Invoke();
            yield return WaitForScene(BattleSceneFlow.StartScene);
            Assert.That(BattleSceneFlow.IsRunActive, Is.False);
            Object.FindFirstObjectByType<Button>().onClick.Invoke();
            yield return WaitForCaptainBattle();
            session = Object.FindFirstObjectByType<BattleSession>();
            Assert.That(BattleSceneFlow.GoldBalance, Is.Zero);
            Assert.That(session.AttemptsRemaining, Is.EqualTo(session.AttemptsPerLevel));
            Assert.That(session.PlaybackSpeed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DefeatSettlesOnceAndRetryKeepsLevelAndLives()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.StartScene);
            Object.FindFirstObjectByType<Button>().onClick.Invoke();
            yield return WaitForCaptainBattle();
            var session = Object.FindFirstObjectByType<BattleSession>();
            ConfigureOutcome(session, false);
            var transitions = 0;
            void Loaded(Scene scene, LoadSceneMode mode) { if (scene.path == BattleSceneFlow.SettlementScene) transitions++; }
            SceneManager.sceneLoaded += Loaded;
            try
            {
                Finish(session);
                Assert.That(session.Simulation.Outcome, Is.EqualTo(BattleOutcome.Defeat));
                session.Advance(0.02f);
                yield return null;
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BattleSceneFlow.BattleScene));
                Assert.That(transitions, Is.Zero);
                Assert.That(session.RecoveryRequired, Is.True);
                Assert.That(session.AttemptsRemaining, Is.EqualTo(2));
                Assert.That(BattleSceneFlow.NextBattleLevelNumber, Is.EqualTo(1));
                Assert.That(BattleSceneFlow.GoldBalance, Is.Zero, "Defeat grants no gold.");
                BattleSceneFlow.RestartCurrentLevel();
                yield return null;
                for (var frame = 0; frame < 600 && Object.FindFirstObjectByType<BattleSession>() == session; frame++) yield return null;
                yield return null;
                Assert.That(Object.FindFirstObjectByType<BattleSession>().AttemptsRemaining, Is.EqualTo(2));
            }
            finally { SceneManager.sceneLoaded -= Loaded; }
        }

        [UnityTest]
        public IEnumerator DirectLevelOneVictoryTransitionsToSettlementExactlyOnce()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.BattleScene);
            yield return null;
            Assert.That(BattleSceneFlow.IsRunActive, Is.True);
            Assert.That(Object.FindObjectsByType<BattleSceneFlow>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            var session = Object.FindFirstObjectByType<BattleSession>();
            ConfigureOutcome(session, true);
            var transitions = 0;
            void Loaded(Scene scene, LoadSceneMode mode) { if (scene.path == BattleSceneFlow.SettlementScene) transitions++; }
            SceneManager.sceneLoaded += Loaded;
            try
            {
                Finish(session);
                Assert.That(session.Simulation.Outcome, Is.EqualTo(BattleOutcome.Victory));
                Assert.That(session.Simulation.ReachedGoalCount, Is.EqualTo(1));
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BattleSceneFlow.BattleScene));
                session.Advance(0.02f);
                yield return WaitForScene(BattleSceneFlow.SettlementScene);
                yield return null;
                Assert.That(transitions, Is.EqualTo(1));
            }
            finally { SceneManager.sceneLoaded -= Loaded; }
        }

        [UnityTest]
        public IEnumerator LevelOneRewindReopensTheActualLastTutorialHint()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.BattleScene);
            var session = Object.FindFirstObjectByType<BattleSession>();
            session.enabled = false;
            if (session.Simulation.Units.Count == 0) session.Advance(.02f);
            var tutorial = session.GetComponent<LevelOneTutorial>();
            for (var frame = 0; frame < 300 && tutorial.CurrentStep != 1; frame++) yield return null;
            Assert.That(tutorial.CurrentStep, Is.EqualTo(1));
            var position = session.Simulation.Units[0].Position;
            var elapsed = session.Simulation.ElapsedTime;
            tutorial.Skip();
            Finish(session);
            Assert.That(session.RecoveryRequired, Is.True);
            yield return null;
            var recovery = Object.FindFirstObjectByType<LegacyLevelOneRecovery>();
            Assert.That(recovery, Is.Not.Null);
            Assert.That(recovery.GetComponent<Canvas>().isRootCanvas, Is.True, "Recovery must not inherit the scaled HUD Canvas.");
            Assert.That(BattleSceneFlow.RewindLevelOne(), Is.True);
            for (var frame = 0; frame < 300 && tutorial.CurrentStep != 1; frame++) yield return null;
            Assert.That(tutorial.CurrentStep, Is.EqualTo(1));
            Assert.That(session.Simulation.IsPaused, Is.True);
            Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(elapsed));
            Assert.That(session.Simulation.Units[0].Position, Is.EqualTo(position));
            Assert.That(session.AttemptsRemaining, Is.EqualTo(2));
            tutorial.Skip();
            Assert.That(session.Simulation.IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator LevelOneRewindPreservesLostLivesAndExhaustionOnlyAllowsTitle()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.BattleScene);
            yield return WaitForCaptainBattle();
            var session = Object.FindFirstObjectByType<BattleSession>();
            session.enabled = false;
            var checkpointPosition = session.Simulation.Units[0].Position;
            var checkpointHp = session.Simulation.Units[0].Hp;
            session.CaptureTutorialCheckpoint(false);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                Finish(session);
                Assert.That(session.AttemptsRemaining, Is.EqualTo(2 - attempt));
                Assert.That(session.RecoveryRequired, Is.True);
                if (attempt == 2) break;
                Assert.That(BattleSceneFlow.RewindLevelOne(), Is.True);
                Assert.That(session.Simulation.Outcome, Is.EqualTo(BattleOutcome.Running));
                Assert.That(session.Simulation.Units[0].Position, Is.EqualTo(checkpointPosition));
                Assert.That(session.Simulation.Units[0].Hp, Is.EqualTo(checkpointHp));
                Assert.That(session.AttemptsRemaining, Is.EqualTo(2 - attempt));
            }
            Assert.That(BattleSceneFlow.RewindLevelOne(), Is.False);
            Assert.That(BattleSceneFlow.IsOutOfLives, Is.True);
            BattleSceneFlow.ContinueToUpgrade();
            BattleSceneFlow.RestartCurrentLevel();
            yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BattleSceneFlow.BattleScene));
            var recovery = Object.FindFirstObjectByType<LegacyLevelOneRecovery>();
            Assert.That(recovery.GetComponentsInChildren<Button>().Length, Is.EqualTo(1));
            recovery.GetComponentInChildren<Button>().onClick.Invoke();
            yield return WaitForScene(BattleSceneFlow.StartScene);
            Object.FindFirstObjectByType<Button>().onClick.Invoke();
            yield return WaitForCaptainBattle();
            Assert.That(BattleSceneFlow.IsOutOfLives, Is.False);
            Assert.That(Object.FindFirstObjectByType<BattleSession>().AttemptsRemaining, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator DirectLevelTwoPlaySupportsThreeAttemptsAndEndsAfterThirdDefeat()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.LevelTwoScene);
            yield return null;
            Assert.That(BattleSceneFlow.IsRunActive, Is.True);
            var hud = Object.FindFirstObjectByType<LegacyBattleHud>();
            Assert.That(hud.LevelLabel, Is.Not.Null, "HUD must initialize even if the obsolete Edit Formation button is absent.");
            Assert.That(hud.LevelLabel.text, Is.EqualTo("LEVEL 2"));
            Assert.That(hud.LivesLabel, Is.Not.Null);
            Assert.That(hud.PauseButton, Is.Not.Null);
            Assert.That(hud.SquadButton(0), Is.Not.Null);
            Assert.That(hud.Session.GetComponent<BattleCombatControls>().ShowDebugPanel, Is.False);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var session = Object.FindFirstObjectByType<BattleSession>();
                Assert.That(session.DisplayedLevelNumber, Is.EqualTo(2));
                Assert.That(session.AttemptsRemaining, Is.EqualTo(3 - attempt));
                ConfigureOutcome(session, false);
                Finish(session);
                if (attempt == 2)
                {
                    yield return null; yield return null;
                    Assert.That(session.AttemptsRemaining, Is.Zero);
                    Assert.That(session.RecoveryRequired, Is.True);
                    Assert.That(BattleSceneFlow.IsOutOfLives, Is.True);
                    var recovery = Object.FindFirstObjectByType<LegacyLevelOneRecovery>();
                    Assert.That(recovery, Is.Not.Null);
                    Assert.That(recovery.GetComponentsInChildren<Button>().Length, Is.EqualTo(1));
                    BattleSceneFlow.ContinueToUpgrade();
                    BattleSceneFlow.RestartCurrentLevel();
                    yield return null;
                    Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BattleSceneFlow.LevelTwoScene));
                    recovery.GetComponentInChildren<Button>().onClick.Invoke();
                    yield return WaitForScene(BattleSceneFlow.StartScene);
                    break;
                }
                yield return WaitForScene(BattleSceneFlow.SettlementScene);
                Assert.That(BattleSceneFlow.NextBattleLevelNumber, Is.EqualTo(2));
                Object.FindFirstObjectByType<Button>().onClick.Invoke();
                yield return WaitForScene(BattleSceneFlow.UpgradeScene);
                var upgrade = Object.FindFirstObjectByType<LegacyUpgradeScene>();
                Assert.That(upgrade.LevelLabel.text, Does.StartWith("LEVEL 2").And.EndWith($"{2 - attempt}/3"));
                upgrade.Preparation.TryAssign(110, 4);
                upgrade.StartWave();
                yield return WaitForScene(BattleSceneFlow.LevelTwoScene);
            }
        }

        [UnityTest]
        public IEnumerator DirectBattlePlayKeepsThePrototypeOpenAfterSettlement()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.PrototypeBattleScene);
            var session = Object.FindFirstObjectByType<BattleSession>();
            ConfigureOutcome(session, false);
            Finish(session);
            yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BattleSceneFlow.PrototypeBattleScene));
            Assert.That(BattleSceneFlow.IsRunActive, Is.False);
        }

        [UnityTest]
        public IEnumerator UpcomingLevelPreviewLoadsActualMapWithoutStartingBattleThenClosesCleanly()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.StartScene);
            Object.FindFirstObjectByType<Button>().onClick.Invoke();
            yield return WaitForCaptainBattle();
            var session = Object.FindFirstObjectByType<BattleSession>();
            ConfigureOutcome(session,true); Finish(session);
            yield return WaitForScene(BattleSceneFlow.SettlementScene);
            Object.FindFirstObjectByType<Button>().onClick.Invoke();
            yield return WaitForScene(BattleSceneFlow.UpgradeScene);
            var ui = Object.FindFirstObjectByType<LegacyUpgradeScene>();
            var gold = BattleSceneFlow.GoldBalance;
            var lives = BattleSceneFlow.CurrentLevelLivesRemaining;
            var camera = Camera.main;
            var pose = camera.transform.position;
            var rotation = camera.transform.rotation;
            ui.PreviewNextLevelButton.onClick.Invoke();
            var preview = Object.FindFirstObjectByType<LegacyNextLevelPreview>();
            Assert.That(preview,Is.Not.Null);
            Assert.That(preview.GetComponent<Canvas>().isRootCanvas,Is.True);
            for(var frame=0;frame<600 && !preview.IsReady;frame++) yield return null;
            Assert.That(preview.IsReady,Is.True);
            Assert.That(preview.LoadedScene.path,Is.EqualTo(BattleSceneFlow.LevelTwoScene));
            var previewSession = preview.LoadedScene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<BattleSession>(true)).Single();
            Assert.That(previewSession.Simulation,Is.Null,"Map preview must never start a battle.");
            var ranges = preview.LoadedScene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<TowerRangeView>(true)).ToArray();
            Assert.That(ranges.Length,Is.EqualTo(previewSession.Towers.Count));
            for(var index=0;index<ranges.Length;index++)
            {
                var definition = previewSession.Towers[index].CaptureDefinition(index+1);
                Assert.That(ranges[index].DisplayBounds,Is.EqualTo(SquadCombatRules.AreaBounds(definition.Position,definition.Area,definition.Range,definition.AttackWidth)));
            }
            Assert.That(SceneManager.GetActiveScene().path,Is.EqualTo(BattleSceneFlow.UpgradeScene));
            Assert.That(BattleSceneFlow.NextBattleLevelNumber,Is.EqualTo(2));
            Assert.That(BattleSceneFlow.GoldBalance,Is.EqualTo(gold));
            Assert.That(BattleSceneFlow.CurrentLevelLivesRemaining,Is.EqualTo(lives));
            preview.transform.Find("Close").GetComponent<Button>().onClick.Invoke();
            for(var frame=0;frame<600 && preview!=null;frame++) yield return null;
            Assert.That(preview==null,Is.True);
            Assert.That(SceneManager.GetSceneByPath(BattleSceneFlow.LevelTwoScene).isLoaded,Is.False);
            Assert.That(camera.transform.position,Is.EqualTo(pose));
            Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
            ui.Preparation.TryAssign(110,4);
            ui.StartWave();
            yield return WaitForScene(BattleSceneFlow.LevelTwoScene);
            Assert.That(Object.FindFirstObjectByType<BattleSession>().Simulation,Is.Not.Null,"Preview cleanup must allow the real battle to start.");
        }

        [UnityTest]
        public IEnumerator ClosingMapPreviewDuringLoadingDoesNotLeakItsScene()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.UpgradeScene);
            yield return null;
            var ui = Object.FindFirstObjectByType<LegacyUpgradeScene>();
            ui.OpenNextLevelPreview();
            var preview = Object.FindFirstObjectByType<LegacyNextLevelPreview>();
            Assert.That(preview,Is.Not.Null);
            preview.Close();
            for(var frame=0;frame<600 && preview!=null;frame++) yield return null;
            Assert.That(preview==null,Is.True);
            Assert.That(SceneManager.GetSceneByPath(BattleSceneFlow.BattleScene).isLoaded,Is.False);
            Assert.That(SceneManager.GetActiveScene().path,Is.EqualTo(BattleSceneFlow.UpgradeScene));
            Assert.That(BattleSceneFlow.GoldBalance,Is.Zero);
            ui.OpenNextLevelPreview();
            preview = Object.FindFirstObjectByType<LegacyNextLevelPreview>();
            Assert.That(preview,Is.Not.Null,"A closed preview must be reopenable.");
            preview.Close();
            for(var frame=0;frame<600 && preview!=null;frame++) yield return null;
        }

        [UnityTest]
        public IEnumerator ClaimCardBelowStorageCancelsForFreeThenBuysOneWarehouseMember()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.StartScene);
            Object.FindFirstObjectByType<Button>().onClick.Invoke();
            yield return WaitForCaptainBattle();
            var session = Object.FindFirstObjectByType<BattleSession>();
            ConfigureOutcome(session,true); Finish(session);
            yield return WaitForScene(BattleSceneFlow.SettlementScene);
            Object.FindFirstObjectByType<Button>().onClick.Invoke();
            yield return WaitForScene(BattleSceneFlow.UpgradeScene);
            var ui = Object.FindFirstObjectByType<LegacyUpgradeScene>();
            var membersBefore = ui.Preparation.Members.Count;
            var assetBefore = ui.Roster.CaptureMembers().Count;
            Assert.That(ui.ClaimCardButton.interactable,Is.True);
            Assert.That(((RectTransform)ui.ClaimCardButton.transform).anchoredPosition.y,Is.LessThan(-100));
            ui.ClaimCardButton.onClick.Invoke();
            var overlay = Object.FindFirstObjectByType<LegacyClaimCardOverlay>();
            Assert.That(overlay.GetComponent<Canvas>().isRootCanvas,Is.True);
            var offer = BattleSceneFlow.CardShop.BeginOffer().ToArray();
            foreach(var choice in offer)
            {
                var introduction = overlay.transform.Find(choice.ToString()+"/Introduction");
                Assert.That(introduction.Find("Emblem"),Is.Not.Null);
                Assert.That(introduction.Find("Name").GetComponent<TMP_Text>().text,Is.EqualTo(LegacyUnitLooks.Name(ClaimCardShop.RoleOf(choice))));
                Assert.That(introduction.GetComponentsInChildren<TMP_Text>().Any(label=>label.text=="HP"),Is.True);
            }
            Assert.That(overlay.GetComponentsInChildren<TMP_Text>().Any(label=>label.text.Contains("Adds one unit")),Is.False);
            overlay.Close();
            yield return null;
            Assert.That(BattleSceneFlow.GoldBalance,Is.EqualTo(10));
            Assert.That(BattleSceneFlow.CardShop.CardsTaken,Is.Zero);
            ui.OpenClaimCards();
            Assert.That(BattleSceneFlow.CardShop.BeginOffer(),Is.EqualTo(offer));
            Assert.That(offer,Is.EqualTo(new[] { ClaimCard.AddAttack, ClaimCard.AddTank, ClaimCard.AddHeal }));
            var unitCard = ClaimCard.AddTank;
            overlay = Object.FindFirstObjectByType<LegacyClaimCardOverlay>();
            overlay.transform.Find(unitCard.ToString()).GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(BattleSceneFlow.GoldBalance,Is.Zero);
            Assert.That(BattleSceneFlow.CardShop.CardsTaken,Is.EqualTo(1));
            Assert.That(ui.Preparation.Members.Count,Is.EqualTo(membersBefore+1));
            Assert.That(ui.Roster.CaptureMembers().Count,Is.EqualTo(assetBefore));
            Assert.That(ui.Preparation.Members.Last().Unit.Role,Is.EqualTo(ClaimCardShop.RoleOf(unitCard)));
            Assert.That(ui.ClaimCardButton.interactable,Is.False);
        }

        [UnityTest]
        public IEnumerator WarehouseHasTwelveCellsLiveCountAndReturnsUnits()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.UpgradeScene);
            yield return null;
            var ui = Object.FindFirstObjectByType<LegacyUpgradeScene>();
            var root = ui.Grid.transform.parent;
            Assert.That(root.Cast<Transform>().Count(child => child.name.StartsWith("Warehouse Cell ")), Is.EqualTo(12));
            Assert.That(ui.StartWaveButton.interactable, Is.False);
            ui.StartWave(); yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BattleSceneFlow.UpgradeScene));
            var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(-10000, -10000), dragging = true };
            for (var slot = 0; slot < 6; slot++)
            {
                pointer.pointerCurrentRaycast = new RaycastResult { gameObject = ui.Grid.transform.Find("Cell " + slot).gameObject };
                Assert.That(ui.TryPlaceMember(ui.Preparation.Members[slot].MemberId, pointer), Is.True);
            }
            var count = ui.Grid.transform.Find("Squad Count").GetComponent<TMP_Text>();
            Assert.That(count.text, Is.EqualTo("6/6"));
            pointer.pointerCurrentRaycast = new RaycastResult { gameObject = ui.Grid.transform.Find("Cell 8").gameObject };
            Assert.That(ui.TryPlaceMember(ui.Preparation.Members[6].MemberId, pointer), Is.False);
            Assert.That(count.text, Is.EqualTo("6/6"));
            ui.Grid.HandleBeginDrag(0, pointer);
            pointer.pointerCurrentRaycast = default;
            ui.Grid.HandleEndDrag(pointer);
            Assert.That(count.text, Is.EqualTo("5/6"));
            Assert.That(ui.Preparation.IsDeployed(ui.Preparation.Members[0].MemberId), Is.False);
            Assert.That(root.Find("Warehouse Cell 0").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StandaloneEndSceneCanReturnToTitle()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.EndScene);
            Object.FindFirstObjectByType<Button>().onClick.Invoke();
            yield return WaitForScene(BattleSceneFlow.StartScene);
            Assert.That(BattleSceneFlow.IsRunActive, Is.False);
        }
    }
}
