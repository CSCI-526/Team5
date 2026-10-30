using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.Presentation.LegacyUI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class UnitInspectionTests
    {
        private BattleSession session;
        private BattlePresentation presentation;
        private BattleUnitInspection inspection;
        private Camera camera;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float originalOrthoSize, originalFov;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.PrototypeBattleScene);
            session = Object.FindFirstObjectByType<BattleSession>();
            session.enabled = false;
            typeof(BattleSession).GetField("logEvents", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, false);
            session.StartBattle(); session.Advance(0.02f);
            presentation = Object.FindFirstObjectByType<BattlePresentation>();
            inspection = presentation.GetComponent<BattleUnitInspection>();
            camera = Camera.main;
            originalPosition = camera.transform.position;
            originalRotation = camera.transform.rotation;
            originalOrthoSize = camera.orthographicSize;
            originalFov = camera.fieldOfView;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            if (inspection != null) inspection.CloseImmediately();
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Unit Inspection Cleanup"));
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
        }

        [UnityTest]
        public IEnumerator SparseSquadUsesFullGridCenterAndRestoresCaptainGlow()
        {
            typeof(BattleSession).GetField("preparedMembers", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session,
                new[] { new MemberSpawnDefinition(110, 0, SquadCombatRules.Create(UnitRole.Captain)) });
            session.StartBattle(); session.Advance(0.02f);
            yield return null;
            var captain = session.Simulation.Units[0];
            Assert.That(presentation.TryGetView(110, out var view), Is.True);
            view.SnapToState();
            Assert.That(session.Grid.TryGetCell(captain.OccupiedCell, out var cell), Is.True);
            var tileRenderer = cell.GetComponentInChildren<Renderer>();
            var tileBefore = new MaterialPropertyBlock(); tileRenderer.GetPropertyBlock(tileBefore);
            var renderer = view.transform.Find("Body").GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null);
            var material = renderer.sharedMaterial;
            var original = new MaterialPropertyBlock(); renderer.GetPropertyBlock(original);
            var hud = Object.FindFirstObjectByType<LegacyBattleHud>();
            hud.SelectSquad();
            Assert.That(inspection.IsSquadSelected, Is.True);
            Assert.That(inspection.TryGetSquadBounds(out var bounds), Is.True);
            Assert.That(Vector3.Distance(bounds.center, captain.AnchorPosition), Is.LessThan(0.001f));
            Assert.That(bounds.size.x, Is.EqualTo(3));
            Assert.That(bounds.size.z, Is.EqualTo(3));
            Assert.That(Vector3.Distance(bounds.center, captain.Position), Is.GreaterThan(1));
            Assert.That(inspection.HighlightedCaptain, Is.SameAs(view));
            var highlighted = new MaterialPropertyBlock(); renderer.GetPropertyBlock(highlighted);
            Assert.That(highlighted.GetColor("_BaseColor").r, Is.EqualTo(1f));
            Assert.That(highlighted.GetColor("_BaseColor"), Is.Not.EqualTo(original.GetColor("_BaseColor")));
            var tileAfter = new MaterialPropertyBlock(); tileRenderer.GetPropertyBlock(tileAfter);
            Assert.That(tileAfter.GetColor("_BaseColor"), Is.EqualTo(tileBefore.GetColor("_BaseColor")));
            Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            yield return new WaitForSecondsRealtime(1.2f);
            var screen = camera.WorldToScreenPoint(captain.AnchorPosition);
            Assert.That(screen.x / Screen.width, Is.EqualTo(0.75f).Within(0.015));
            Assert.That(screen.y / Screen.height, Is.EqualTo(0.5f).Within(0.015));
            Assert.That(inspection.SelectUnit(110), Is.True);
            Assert.That(inspection.HighlightedCaptain, Is.Null);
            var restored = new MaterialPropertyBlock(); renderer.GetPropertyBlock(restored);
            Assert.That(restored.GetColor("_BaseColor"), Is.EqualTo(original.GetColor("_BaseColor")));
            yield return null;
            hud.SelectSquad();
            Assert.That(inspection.HighlightedCaptain, Is.SameAs(view));
            presentation.enabled = false;
            Assert.That(inspection.HighlightedCaptain, Is.Null);
            renderer.GetPropertyBlock(restored);
            Assert.That(restored.GetColor("_BaseColor"), Is.EqualTo(original.GetColor("_BaseColor")));
        }

        [UnityTest]
        public IEnumerator SquadButtonsFocusAllMembersAndEditSlotsInsideLeftPanel()
        {
            typeof(BattleSession).GetField("preparedMembers", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session,
                new[] { new MemberSpawnDefinition(101, 0, SquadCombatRules.Create(UnitRole.Attack)),
                    new MemberSpawnDefinition(110, 4, SquadCombatRules.Create(UnitRole.Captain)),
                    new MemberSpawnDefinition(103, 8, SquadCombatRules.Create(UnitRole.Heal)) });
            session.StartBattle(); session.Advance(0.02f);
            var hud = Object.FindFirstObjectByType<LegacyBattleHud>();
            yield return null;
            for (var i = 0; i < 3; i++)
            {
                var button = hud.SquadButton(i);
                var rect = (RectTransform)button.transform;
                Assert.That(rect.sizeDelta.x, Is.EqualTo(rect.sizeDelta.y));
                Assert.That(button.GetComponentsInChildren<TMPro.TMP_Text>().Length, Is.Zero);
                if (i > 0)
                {
                    Assert.That(button.interactable, Is.False);
                    Assert.That(button.GetComponentsInChildren<LegacyUnitIconUI>().Length, Is.Zero);
                    Assert.That(rect.anchoredPosition.y, Is.LessThan(((RectTransform)hud.SquadButton(i - 1).transform).anchoredPosition.y));
                }
            }
            Assert.That(hud.SquadButton(0).GetComponentsInChildren<UnityEngine.UI.Image>().Any(image => image.name.StartsWith("Crown ")), Is.True);
            var before = session.Simulation.Units.Select(unit => (unit.MemberId, unit.Hp, unit.AttackCooldown)).ToArray();
            hud.SquadButton(0).onClick.Invoke();
            Assert.That(inspection.IsSquadSelected, Is.True);
            Assert.That(session.Simulation.IsPaused, Is.True);
            Assert.That(hud.FormationEditor.transform.IsChildOf(inspection.Panel), Is.True);
            var elapsed = session.Simulation.ElapsedTime;
            session.AdvancePlaybackFrame(0.02f);
            Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(elapsed));
            var grid = hud.FormationEditor.Grid;
            grid.HandleClick(0); grid.HandleClick(4);
            Assert.That(session.Simulation.GetMemberAtSlot(4).MemberId, Is.EqualTo(110));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(inspection.TryGetSquadBounds(out var bounds), Is.True);
            var center = camera.WorldToScreenPoint(bounds.center);
            Assert.That(center.x / Screen.width, Is.EqualTo(0.75f).Within(0.015));
            Assert.That(center.y / Screen.height, Is.EqualTo(0.5f).Within(0.015));
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = camera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                Assert.That(corner.x / Screen.width, Is.InRange(0.5f, 1f));
                Assert.That(corner.y / Screen.height, Is.InRange(0f, 1f));
            }
            Assert.That(hud.FormationEditor.ApplyAndResume(), Is.True);
            Assert.That(session.Simulation.GetMemberAtSlot(4).MemberId, Is.EqualTo(101));
            Assert.That(session.Simulation.GetMemberAtSlot(0).MemberId, Is.EqualTo(110));
            Assert.That(session.Simulation.Units.Select(unit => (unit.MemberId, unit.Hp, unit.AttackCooldown)), Is.EqualTo(before));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(inspection.IsSquadSelected, Is.False);
            Assert.That(session.Simulation.IsPaused, Is.False);
            Assert.That(session.IsUnitInspectionActive, Is.False);
            AssertCameraRestored();

            session.Simulation.SetPaused(true);
            hud.SelectSquad();
            hud.FormationEditor.Grid.HandleClick(0); hud.FormationEditor.Grid.HandleClick(4);
            inspection.CloseImmediately();
            Assert.That(session.Simulation.IsPaused, Is.True);
            Assert.That(session.Simulation.GetMemberAtSlot(0).MemberId, Is.EqualTo(110), "Closing cancels the draft.");
            AssertCameraRestored();
            yield return null;
            hud.SelectSquad();
            presentation.enabled = false;
            Assert.That(inspection.IsSquadSelected, Is.False);
            Assert.That(session.Simulation.IsPaused, Is.True);
            AssertCameraRestored();
        }

        [UnityTest]
        public IEnumerator CaptainSkillBoxesOpenExistingEditorAndRestoreInspectionSlowMotion()
        {
            typeof(BattleSession).GetField("preparedMembers", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session,
                new[] { new MemberSpawnDefinition(110, 4, SquadCombatRules.Create(UnitRole.Captain)) });
            session.StartBattle(); session.Advance(0.02f);
            Assert.That(inspection.SelectUnit(110), Is.True);
            var hud = Object.FindFirstObjectByType<LegacyBattleHud>();
            Assert.That(hud.EditButton.gameObject.activeSelf, Is.False);
            var row = inspection.Panel.Find("Unit Skills");
            Assert.That(row.gameObject.activeSelf, Is.True);
            var buttons = row.GetComponentsInChildren<UnityEngine.UI.Button>();
            Assert.That(buttons.Length, Is.EqualTo(3));
            foreach (var button in buttons)
            {
                var size = ((RectTransform)button.transform).sizeDelta;
                Assert.That(size.x, Is.EqualTo(size.y));
                Assert.That(button.interactable, Is.True);
            }
            buttons[1].onClick.Invoke();
            Assert.That(session.Simulation.IsPaused, Is.False);
            Assert.That(row.Find("Skill Feedback").GetComponent<TMPro.TMP_Text>().text, Does.Contain("not assigned"));
            buttons[0].onClick.Invoke();
            Assert.That(hud.FormationEditor, Is.Not.Null);
            Assert.That(session.Simulation.IsPaused, Is.True);
            Assert.That(hud.FormationEditor.ApplyAndResume(), Is.True);
            Assert.That(session.Simulation.IsPaused, Is.False);
            Assert.That(session.IsUnitInspectionActive, Is.True);
            yield return null;
            Assert.That(session.EffectivePlaybackSpeed, Is.EqualTo(BattleSession.BaseTimeMultiplier * BattleSession.InspectionTimeMultiplier));
            foreach (var role in new[] { UnitRole.Attack, UnitRole.Tank, UnitRole.Heal })
            {
                inspection.CloseImmediately();
                typeof(BattleSession).GetField("preparedMembers", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session,
                    new[] { new MemberSpawnDefinition(101, 4, SquadCombatRules.Create(role)) });
                session.StartBattle(); session.Advance(0.02f);
                Assert.That(inspection.SelectUnit(101), Is.True);
                row = inspection.Panel.Find("Unit Skills");
                Assert.That(row.gameObject.activeSelf, Is.True);
                buttons = row.GetComponentsInChildren<UnityEngine.UI.Button>();
                Assert.That(buttons.Length, Is.EqualTo(3));
                foreach (var button in buttons)
                {
                    Assert.That(button.interactable, Is.True);
                    Assert.That(((RectTransform)button.transform).sizeDelta, Is.EqualTo(new Vector2(96, 96)));
                    button.onClick.Invoke();
                    Assert.That(row.Find("Skill Feedback").GetComponent<TMPro.TMP_Text>().text, Does.Contain("not assigned"));
                }
                Assert.That(hud.FormationEditor == null, Is.True);
                Assert.That(session.Simulation.IsPaused, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator PickingUsesVisibleBoundsAndFocusesRightHalfWithoutMutatingCombat()
        {
            var unit = session.Simulation.Units[0];
            Assert.That(presentation.TryGetView(unit.Id, out var view), Is.True);
            Assert.That(view.GetComponentInChildren<Collider>(), Is.Null);
            Assert.That(view.TryGetVisibleBounds(out var bounds), Is.True);
            var state = (unit.Hp, unit.Position, unit.AttackCooldown, unit.SlotId, session.Simulation.ElapsedTime);
            Assert.That(inspection.TrySelectAt(camera.WorldToScreenPoint(bounds.center)), Is.True);
            Assert.That(inspection.Panel.anchorMax, Is.EqualTo(new Vector2(0.5f, 1)));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(view.TryGetVisibleBounds(out bounds), Is.True);
            var screen = camera.WorldToScreenPoint(bounds.center);
            Assert.That(screen.x / Screen.width, Is.EqualTo(0.75f).Within(0.02f));
            Assert.That(screen.y / Screen.height, Is.EqualTo(0.5f).Within(0.02f));
            Assert.That((unit.Hp, unit.Position, unit.AttackCooldown, unit.SlotId, session.Simulation.ElapsedTime), Is.EqualTo(state));
            Assert.That(camera.fieldOfView, Is.EqualTo(originalFov));
        }

        [UnityTest]
        public IEnumerator InspectionAlwaysRunsAtOneTenthAndRestoresSliderSpeed()
        {
            var globalTimeScale = Time.timeScale;
            foreach (var speed in new[] { 1, 2, 3 })
            {
                session.SetPlaybackSpeed(speed);
                Assert.That(inspection.SelectUnit(session.Simulation.Units[0].Id), Is.True);
                Assert.That(session.IsUnitInspectionActive, Is.True);
                Assert.That(session.EffectivePlaybackSpeed, Is.EqualTo(BattleSession.BaseTimeMultiplier * BattleSession.InspectionTimeMultiplier));
                var elapsed = session.Simulation.ElapsedTime;
                session.AdvancePlaybackFrame(0.02f);
                Assert.That(session.Simulation.ElapsedTime - elapsed, Is.EqualTo(0.0016).Within(0.000001));
                yield return null;
                Assert.That(Object.FindFirstObjectByType<LegacyBattleHud>().SpeedSlider.interactable, Is.False);
                Assert.That(inspection.SelectUnit(session.Simulation.Units[1].Id), Is.True);
                Assert.That(session.EffectivePlaybackSpeed, Is.EqualTo(BattleSession.BaseTimeMultiplier * BattleSession.InspectionTimeMultiplier));
                session.Simulation.SetPaused(true);
                elapsed = session.Simulation.ElapsedTime;
                session.AdvancePlaybackFrame(0.02f);
                Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(elapsed));
                session.Simulation.SetPaused(false);
                inspection.CloseInspection();
                Assert.That(session.PlaybackSpeed, Is.EqualTo(speed));
                Assert.That(session.EffectivePlaybackSpeed, Is.EqualTo(BattleSession.BaseTimeMultiplier * speed));
                session.AdvancePlaybackFrame(0.02f);
                Assert.That(session.Simulation.ElapsedTime - elapsed, Is.EqualTo(0.016 * speed).Within(0.000001));
            }
            Assert.That(Time.timeScale, Is.EqualTo(globalTimeScale));
        }

        [UnityTest]
        public IEnumerator StatsReadActualHpAndResetRestoresCamera()
        {
            var unit = session.Simulation.Units.First(member => member.Role == UnitRole.Tank);
            Assert.That(inspection.SelectUnit(unit.Id), Is.True);
            yield return new WaitForSecondsRealtime(0.3f);
            for (var i = 0; i < 100; i++) session.Advance(0.02f);
            yield return null;
            Assert.That(inspection.DetailsLabel.text, Does.Contain($"HP: {unit.Hp:0.##} / {unit.MaxHp:0.##}"));
            Assert.That(inspection.DetailsLabel.text, Does.Contain("Defense: 20% damage reduction"));
            session.StartBattle();
            Assert.That(inspection.SelectedUnit, Is.Null);
            Assert.That(session.IsUnitInspectionActive, Is.False);
            AssertCameraRestored();
        }

        [UnityTest]
        public IEnumerator ChangingSelectionThenDisablingPresentationRestoresOriginalView()
        {
            Assert.That(inspection.SelectUnit(session.Simulation.Units[0].Id), Is.True);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(inspection.SelectUnit(session.Simulation.Units[1].Id), Is.True);
            yield return new WaitForSecondsRealtime(0.2f);
            presentation.enabled = false;
            AssertCameraRestored();
            Assert.That(inspection.SelectedUnit, Is.Null);
            Assert.That(session.IsUnitInspectionActive, Is.False);
            var elapsed = session.Simulation.ElapsedTime;
            session.Advance(0.02f);
            Assert.That(session.Simulation.ElapsedTime, Is.GreaterThan(elapsed));
        }

        private void AssertCameraRestored()
        {
            Assert.That(camera.transform.position, Is.EqualTo(originalPosition));
            Assert.That(camera.transform.rotation, Is.EqualTo(originalRotation));
            Assert.That(camera.orthographicSize, Is.EqualTo(originalOrthoSize));
            Assert.That(camera.fieldOfView, Is.EqualTo(originalFov));
        }
    }
}
