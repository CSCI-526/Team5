using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    public sealed class LegacyHudPlayModeTests
    {
        private BattleSession session;
        private LegacyBattleHud hud;
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Rebuild/Scenes/SquadBattlePrototype.unity");
            session = Object.FindFirstObjectByType<BattleSession>(); session.enabled = false;
            typeof(BattleSession).GetField("logEvents", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, false);
            session.StartBattle(); hud = Object.FindFirstObjectByType<LegacyBattleHud>();
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Close()
        {
            var empty = SceneManager.CreateScene("HUD Test Cleanup"); SceneManager.SetActiveScene(empty);
            if (session != null) yield return SceneManager.UnloadSceneAsync(session.gameObject.scene);
        }
        [UnityTest]
        public IEnumerator PauseButtonFreezesBattleAndPreservesSpeedAndEditorPause()
        {
            var speedRect = (RectTransform)hud.SpeedSlider.transform;
            var originalPosition = speedRect.anchoredPosition;
            var originalScale = speedRect.localScale;
            Assert.That(hud.PauseButton.transform.parent, Is.EqualTo(speedRect));
            hud.SetSpeedFromSlider(2);
            session.AdvancePlaybackFrame(0.02f);
            hud.PauseButton.onClick.Invoke();
            Assert.That(session.Simulation.IsPaused, Is.True);
            Assert.That(hud.PauseButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Resume"));
            var elapsed = session.Simulation.ElapsedTime;
            session.AdvancePlaybackFrame(0.02f);
            Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(elapsed));
            hud.OpenSquadEditor();
            hud.TogglePause();
            Assert.That(session.Simulation.IsPaused, Is.True);
            Assert.That(hud.FormationEditor.ApplyAndResume(), Is.True);
            yield return null;
            Assert.That(session.Simulation.IsPaused, Is.True);
            session.SetUnitInspectionActive(true);
            hud.PauseButton.onClick.Invoke();
            Assert.That(session.Simulation.IsPaused, Is.False);
            Assert.That(session.IsUnitInspectionActive, Is.True);
            Assert.That(session.PlaybackSpeed, Is.EqualTo(3));
            Assert.That(hud.PauseButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Pause"));
            Assert.That(speedRect.anchoredPosition, Is.EqualTo(originalPosition));
            Assert.That(speedRect.localScale, Is.EqualTo(originalScale));
            session.StartBattle();
            Assert.That(session.Simulation.IsPaused, Is.False);
            yield return null;
            Assert.That(hud.PauseButton.interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator HudMatchesLegacyLabelsAndSpeedSliderRunsFixedTicksWithoutGlobalTimeScale()
        {
            Assert.That(hud.LevelLabel.text, Is.EqualTo("LEVEL 1"));
            Assert.That(hud.LivesLabel.text, Is.EqualTo(": 3/3"));
            Assert.That(hud.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text.Contains("$")), Is.False);
            Assert.That(session.GetComponent<BattleCombatControls>().ShowDebugPanel, Is.False);
            var globalScale = Time.timeScale;
            hud.SpeedSlider.value = 2;
            Assert.That(session.PlaybackSpeed, Is.EqualTo(3));
            session.AdvancePlaybackFrame(0.02f);
            Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(0.048).Within(0.00001));
            hud.SpeedSlider.value = 1;
            session.AdvancePlaybackFrame(0.02f);
            Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(0.080).Within(0.00001));
            hud.SpeedSlider.value = 0;
            session.AdvancePlaybackFrame(0.02f);
            Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(0.096).Within(0.00001));
            session.Simulation.SetPaused(true); session.AdvancePlaybackFrame(0.02f);
            Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(0.096).Within(0.00001));
            Assert.That(Time.timeScale, Is.EqualTo(globalScale));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.SetPlaybackSpeed(4));
            yield return null;
        }
        [UnityTest]
        public IEnumerator LegacyDragDraftIsAppliedAsASlotCommandWithoutResettingMembers()
        {
            session.Advance(0.02f);
            var simulation = session.Simulation;
            var before = simulation.Units.Select(u => (u.MemberId, u.Hp, u.AttackCooldown, u.RouteProgress)).ToArray();
            var healer = simulation.GetMemberAtSlot(4); var tank = simulation.GetMemberAtSlot(1);
            hud.EditButton.onClick.Invoke();
            Assert.That(simulation.IsPaused, Is.True);
            var editor = hud.FormationEditor; var grid = editor.Grid;
            Canvas.ForceUpdateCanvases();
            var start = grid.transform.Find("Cell 4"); var end = grid.transform.Find("Cell 1");
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, start.position), dragging = true };
            grid.HandleBeginDrag(4, pointer);
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, end.position);
            grid.HandleDrag(pointer); grid.HandleEndDrag(pointer);
            Assert.That(grid.CopySourceSlots()[1], Is.EqualTo(4));
            Assert.That(simulation.GetMemberAtSlot(4), Is.SameAs(healer), "Dragging changes only the draft.");
            Assert.That(editor.ApplyAndResume(), Is.True);
            Assert.That(simulation.GetMemberAtSlot(1), Is.SameAs(healer));
            Assert.That(simulation.GetMemberAtSlot(4), Is.SameAs(tank));
            Assert.That(simulation.Units.Select(u => (u.MemberId, u.Hp, u.AttackCooldown, u.RouteProgress)), Is.EqualTo(before));
            Assert.That(simulation.IsPaused, Is.False);
            yield return null;
            Assert.That(hud.FormationEditor == null, Is.True);
        }
        [UnityTest]
        public IEnumerator RemovingHudWhileEditingDoesNotMutateOrResumeTheSimulation()
        {
            session.Advance(0.02f); hud.OpenSquadEditor();
            var simulation = session.Simulation;
            var before = simulation.Units.Select(u => (u.MemberId, u.Hp, u.Position, u.SlotId)).ToArray();
            hud.enabled = false;
            yield return null;
            Assert.That(simulation.IsPaused, Is.True);
            Assert.That(simulation.Units.Select(u => (u.MemberId, u.Hp, u.Position, u.SlotId)), Is.EqualTo(before));
            Assert.That(hud.GetComponent<Canvas>().enabled, Is.False);
            hud.enabled = true;
            yield return null;
            Assert.That(simulation.IsPaused, Is.True);
            Assert.That(hud.GetComponent<Canvas>().enabled, Is.True);
            Assert.That(hud.LivesLabel.text, Is.EqualTo(": 3/3"));
        }
        [UnityTest]
        public IEnumerator LivesTrackResolvedFailuresOnceAndHudRebindsAfterRestart()
        {
            foreach (var tower in session.Towers)
            {
                typeof(BattleTower).GetField("damage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tower, 1000f);
                typeof(BattleTower).GetField("range", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tower, 1000f);
                typeof(BattleTower).GetField("attackInterval", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tower, 0.02f);
            }
            for (var attempt = 0; attempt < 3; attempt++)
            {
                session.StartBattle();
                for (var tick = 0; tick < 3000 && session.Simulation.Outcome == BattleOutcome.Running; tick++) session.Advance(0.02f);
                Assert.That(session.Simulation.Outcome, Is.EqualTo(BattleOutcome.Defeat));
                Assert.That(session.AttemptsRemaining, Is.EqualTo(2 - attempt));
                session.Advance(0.02f);
                Assert.That(session.AttemptsRemaining, Is.EqualTo(2 - attempt));
            }
            yield return null;
            Assert.That(hud.LivesLabel.text, Is.EqualTo(": 0/3"));
            session.StartBattle();
            yield return null;
            Assert.That(hud.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text.Contains("$")), Is.False);
            Assert.That(session.AttemptsRemaining, Is.Zero);
        }
    }
}
