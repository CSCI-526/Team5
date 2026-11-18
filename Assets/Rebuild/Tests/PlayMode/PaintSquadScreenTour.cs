#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    // Screenshot tour of every Team5 UI screen with the Paint Squad set, written to Logs/PaintSquadScreens
    // as <prefix>-<screen>.png. Explicit: it never runs with the normal suites. Run it with
    //   Unity -batchmode -projectPath ~/Team5 -runTests -testPlatform PlayMode
    //     -testFilter Team5.Rebuild.Tests.PaintSquadScreenTour -testResults Logs/tour.xml
    // (no -nographics). The prefix comes from the PAINT_SQUAD_CAPTURE_PREFIX environment variable (default "tour").
    [Explicit("Screenshot tour; run on demand."), Category("PaintSquadCapture")]
    public sealed class PaintSquadScreenTour
    {
        private const string Scenes = "Assets/Rebuild/Scenes/";
        private CharacterSetPrefsScope prefs;
        private string prefix;

        [SetUp]
        public void Pin()
        {
            prefs = new CharacterSetPrefsScope(CharacterSet.PaintSquad);
            prefix = Environment.GetEnvironmentVariable("PAINT_SQUAD_CAPTURE_PREFIX");
            if (string.IsNullOrEmpty(prefix)) prefix = "tour";
            Time.timeScale = 1;
        }

        [TearDown]
        public void Restore() { prefs.Dispose(); Time.timeScale = 1; }

        private IEnumerator Shot(string screen, int width = 1920, int height = 1080)
        {
            var name = width == 1920 ? $"{prefix}-{screen}" : $"{prefix}-{screen}-{width}x{height}";
            yield return PaintSquadScreenCapture.CaptureRoutine(name, width, height);
        }

        private static IEnumerator Load(string scene)
        {
            EditorSceneManager.LoadSceneInPlayMode(Scenes + scene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitForScene(scene);
        }

        private static IEnumerator WaitForScene(string scene)
        {
            var end = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < end && SceneManager.GetActiveScene().path != Scenes + scene + ".unity") yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Scenes + scene + ".unity"));
            for (var i = 0; i < 4; i++) yield return null;
        }

        private static IEnumerator Frames(int count) { for (var i = 0; i < count; i++) yield return null; }

        private static IEnumerator Seconds(float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Exclude);

        private static object Flow => typeof(BattleSceneFlow).GetField("current", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);

        private static void SetFlowField(string field, object value) =>
            typeof(BattleSceneFlow).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(Flow, value);

        private static void SetFlowStatic(string property, object value) =>
            typeof(BattleSceneFlow).GetProperty(property, BindingFlags.Public | BindingFlags.Static)?.GetSetMethod(true)?.Invoke(null, new[] { value });

        private static void Click(GameObject root, string name)
        {
            var button = root.GetComponentsInChildren<Button>(true).First(b => b.name == name);
            button.onClick.Invoke();
        }

        // One battle-camera shot per level for the Paint Squad dressing, as <prefix>-level<N>.png
        // (prefix from PAINT_SQUAD_CAPTURE_PREFIX, default "tour"), plus <prefix>-level<N>-overview.png before the start.
        [UnityTest]
        public IEnumerator CaptureEveryLevel()
        {
            for (var level = 1; level <= 7; level++)
            {
                yield return Load($"Level{level}GridBattle");
                foreach (var guide in Object.FindObjectsByType<LevelOneTutorial>(FindObjectsSortMode.None)) guide.Skip();
                yield return Frames(3);
                yield return Shot($"level{level}-overview");
                var session = Find<BattleSession>();
                var fixture = new SerializedObject(session);
                fixture.FindProperty("initialRoster").objectReferenceValue = null;
                var roles = new[] { UnitRole.Captain, UnitRole.Attack, UnitRole.Tank, UnitRole.Heal, UnitRole.Sniper };
                var slots = new[] { 4, 1, 3, 5, 7 };
                var members = fixture.FindProperty("members");
                members.arraySize = roles.Length;
                for (var i = 0; i < roles.Length; i++)
                {
                    var member = members.GetArrayElementAtIndex(i);
                    member.FindPropertyRelative("enabled").boolValue = true;
                    member.FindPropertyRelative("memberId").intValue = 310 + i;
                    member.FindPropertyRelative("slotId").intValue = slots[i];
                    member.FindPropertyRelative("role").intValue = (int)roles[i];
                    member.FindPropertyRelative("spawnTime").floatValue = 0;
                }
                fixture.ApplyModifiedPropertiesWithoutUndo();
                session.StartBattle();
                session.Simulation.SetPaused(false);
                yield return Seconds(1.5f);
                session.Simulation.SetPaused(true);
                yield return Frames(2);
                yield return Shot($"level{level}");
            }
        }

        [UnityTest]
        public IEnumerator CaptureEveryScreen()
        {
            // Title, character select, Level 1 with its tutorial.
            yield return Load("StartGame");
            yield return Shot("start");
            Find<LegacySceneMenu>().StartGame();
            yield return Frames(3);
            yield return Shot("select");
            CharacterSelectScreen.Active.Choose(CharacterSet.PaintSquad);
            yield return WaitForScene("Level1GridBattle");
            yield return Seconds(1.5f);
            yield return Shot("tutorial");
            foreach (var guide in Object.FindObjectsByType<LevelOneTutorial>(FindObjectsSortMode.None)) guide.Skip();

            // A fuller battle on Level 2: Captain plus two legendaries for the skill panel and health bars.
            yield return Load("Level2GridBattle");
            var session = Find<BattleSession>();
            var fixture = new SerializedObject(session);
            fixture.FindProperty("initialRoster").objectReferenceValue = null;
            var roles = new[] { UnitRole.Captain, UnitRole.Frostweaver, UnitRole.Warcaller, UnitRole.Attack, UnitRole.Tank, UnitRole.Heal };
            var slots = new[] { 4, 3, 5, 1, 7, 0 };
            var members = fixture.FindProperty("members");
            members.arraySize = roles.Length;
            for (var i = 0; i < roles.Length; i++)
            {
                var member = members.GetArrayElementAtIndex(i);
                member.FindPropertyRelative("enabled").boolValue = true;
                member.FindPropertyRelative("memberId").intValue = 210 + i;
                member.FindPropertyRelative("slotId").intValue = slots[i];
                member.FindPropertyRelative("role").intValue = (int)roles[i];
                member.FindPropertyRelative("spawnTime").floatValue = 0;
            }
            fixture.ApplyModifiedPropertiesWithoutUndo();
            session.StartBattle();
            session.Simulation.SetPaused(false);
            session.SetPlaybackSpeed(3);
            yield return Seconds(7f);
            session.Simulation.SetPaused(true);
            yield return Frames(2);
            yield return Shot("battle-hud");
            yield return Shot("battle-hud", 960, 600);
            var hud = Find<LegacyBattleHud>();
            session.Simulation.SetPaused(false);
            hud.OpenSquadEditor();
            yield return Frames(3);
            yield return Shot("formation-editor");
            if (hud.FormationEditor != null) hud.FormationEditor.CancelAndClose();
            yield return Frames(2);
            hud.SelectSquad();
            yield return Frames(3);
            yield return Shot("squad-inspection");
            hud.SelectSquad();
            yield return Frames(2);
            // Legendary skill panel with its tooltip: arm the Frostweaver freeze.
            session.Simulation.SetPaused(false);
            var frost = hud.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == UnitRole.Frostweaver + " Skill");
            if (frost != null) frost.onClick.Invoke();
            yield return Frames(2);
            yield return Shot("skill-panel");
            session.Simulation.SetPaused(true);
            yield return Frames(2);
            var recovery = typeof(BattleSession).GetProperty("RecoveryRequired", BindingFlags.Public | BindingFlags.Instance);
            recovery?.GetSetMethod(true)?.Invoke(session, new object[] { true });
            yield return Frames(3);
            yield return Shot("result-defeat");
            recovery?.GetSetMethod(true)?.Invoke(session, new object[] { false });
            yield return Frames(2);
            session.Simulation.SetPaused(true);

            // Settlement, both outcomes.
            SetFlowStatic("LastOutcome", BattleOutcome.Victory);
            SetFlowStatic("CompletedLevelNumber", 2);
            SetFlowStatic("LastRewardGold", 75);
            SetFlowStatic("LastStageClearGold", 45);
            SetFlowStatic("LastTowerBonusGold", 30);
            SetFlowStatic("LastTowersDestroyedCount", 2);
            yield return Load("LevelSettlement");
            yield return Shot("settlement-victory");
            SetFlowStatic("LastOutcome", BattleOutcome.Defeat);
            var settlement = Find<LegacyLevelSettlement>();
            settlement.enabled = false; settlement.enabled = true;
            yield return Frames(2);
            yield return Shot("settlement-defeat");

            // Upgrade: first-summon guide, summon offer, new unit intro, filled storage, popups, preview.
            SetFlowField("highestClearedLevel", 1);
            SetFlowField("nextBattleLevelNumber", 2);
            var wallet = typeof(BattleSceneFlow).GetField("wallet", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(Flow) as CampaignWallet;
            wallet?.AddBonusGold(3000);
            BattleSceneFlow.ContinueToUpgrade();
            yield return WaitForScene("UpgradeScene");
            yield return Frames(4);
            yield return Shot("summon-guide");
            var upgrade = Find<LegacyUpgradeScene>();
            upgrade.OpenClaimCards();
            yield return Frames(3);
            yield return Shot("claim-cards");
            var offer = GameObject.Find("Unit Summon Offer");
            Click(offer, "Summon Option 0");
            yield return Frames(3);
            if (GameObject.Find("New Unit Introduction") != null)
            {
                yield return Shot("unit-intro");
                Click(GameObject.Find("New Unit Introduction"), "Got It");
                yield return Frames(2);
            }
            for (var pull = 0; pull < 5 && upgrade.Preparation.CanAddMember; pull++)
            {
                upgrade.OpenClaimCards();
                yield return Frames(2);
                var next = GameObject.Find("Unit Summon Offer");
                if (next == null) break;
                Click(next, "Summon Option " + (pull % 3));
                yield return Frames(2);
                var intro = GameObject.Find("New Unit Introduction");
                if (intro != null) { Click(intro, "Got It"); yield return Frames(2); }
            }
            yield return Frames(4);
            yield return Shot("upgrade");
            yield return Shot("upgrade", 960, 600);
            var last = upgrade.Preparation.Members.Last();
            upgrade.ShowUnitDetails(last.MemberId);
            yield return Frames(3);
            yield return Shot("unit-details");
            upgrade.CloseUnitDetails();
            yield return Frames(2);
            upgrade.TryDismissMember(last.MemberId);
            yield return Frames(3);
            yield return Shot("dismiss-confirm");
            Click(GameObject.Find("Dismiss Unit Confirmation"), "Cancel");
            yield return Frames(2);
            upgrade.OpenNextLevelPreview();
            for (var end = Time.realtimeSinceStartup + 30f; Time.realtimeSinceStartup < end;)
            {
                var preview = Find<LegacyNextLevelPreview>();
                if (preview == null || preview.IsReady) break;
                yield return null;
            }
            yield return Seconds(1f);
            yield return Shot("next-level");
            var open = Find<LegacyNextLevelPreview>();
            if (open != null) open.Close();
            for (var i = 0; i < 300 && Find<LegacyNextLevelPreview>() != null; i++) yield return null;
            yield return Frames(3);
            for (var pull = 0; pull < 12 && upgrade.Preparation.CanAddMember; pull++)
            {
                upgrade.OpenClaimCards();
                yield return Frames(2);
                var next = GameObject.Find("Unit Summon Offer");
                if (next == null) break;
                Click(next, "Summon Option 0");
                yield return Frames(2);
                var intro = GameObject.Find("New Unit Introduction");
                if (intro != null) { Click(intro, "Got It"); yield return Frames(2); }
            }
            yield return Seconds(1f);
            yield return Shot("storage-full");

            // End of the run.
            yield return Load("EndGame");
            yield return Shot("endgame");
        }
    }
}
#endif
