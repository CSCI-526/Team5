#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    public sealed class PaintSquadUnitPlayModeTests
    {
        const string Catalog = "Assets/Rebuild/PaintSquad/RoleVisuals/PaintSquadAllRoles.asset";
        const int MemberId = 901;
        private Scene initial, opened;
        private CharacterSetPrefsScope prefs;

        // The scene is pointed at the Paint Squad catalog below; pin the selection so runtime lookup agrees.
        [SetUp]
        public void PinPaintSquad() => prefs = new CharacterSetPrefsScope(CharacterSet.PaintSquad);

        [UnityTest]
        public IEnumerator PaintSquadUnitGoesIdleThenRunThenShootsOrDies()
        {
            initial = SceneManager.GetActiveScene(); Time.timeScale = 1;
            const string path = "Assets/Rebuild/Scenes/Level4GridBattle.unity";
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; yield return null;
            opened = SceneManager.GetSceneByPath(path); SceneManager.SetActiveScene(opened);
            var owner = opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattlePresentation>()).Single();
            var session = owner.Session;
            // Test-only: point this scene at the Paint Squad catalog and deploy one Attack member.
            var presentation = new SerializedObject(owner);
            presentation.FindProperty("roleVisuals").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnitRoleVisualSet>(Catalog);
            presentation.ApplyModifiedPropertiesWithoutUndo();
            var fixture = new SerializedObject(session); fixture.FindProperty("initialRoster").objectReferenceValue = null;
            // A second (Tank) member keeps the battle running if the observed unit dies, so no defeat flow starts.
            var members = fixture.FindProperty("members"); members.arraySize = 2;
            for (var i = 0; i < 2; i++)
            {
                var member = members.GetArrayElementAtIndex(i);
                member.FindPropertyRelative("enabled").boolValue = true;
                member.FindPropertyRelative("memberId").intValue = MemberId + i;
                member.FindPropertyRelative("slotId").intValue = i == 0 ? 4 : 1;
                member.FindPropertyRelative("role").intValue = (int)(i == 0 ? UnitRole.Attack : UnitRole.Tank);
                member.FindPropertyRelative("spawnTime").floatValue = 0;
            }
            fixture.ApplyModifiedPropertiesWithoutUndo();
            session.StartBattle(); session.Simulation.SetPaused(false);
            for (var i = 0; i < 120 && !owner.TryGetView(MemberId, out _); i++)
            { session.AdvancePlaybackFrame(.02f); yield return null; }
            Assert.That(owner.TryGetView(MemberId, out var view), Is.True);
            var driver = view.GetComponent<PaintSquadUnitAnimation>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty);
            var animator = driver.CharacterAnimator;
            Assert.That(animator.applyRootMotion, Is.False);

            // Idle: paused battle holds the Idle end of the Locomotion blend.
            session.Simulation.SetPaused(true);
            yield return null; yield return null;
            Assert.That(driver.State, Is.EqualTo(PaintSquadAnimationState.Idle));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), Is.True);
            Assert.That(animator.GetFloat("Speed"), Is.LessThan(.05f));
            Assert.That(animator.speed, Is.Zero);

            // Run: moving along the route blends Locomotion up to the Run take and turns the model.
            session.Simulation.SetPaused(false);
            for (var i = 0; i < 120 && animator.GetFloat("Speed") < .35f; i++) yield return null;
            Assert.That(driver.State, Is.EqualTo(PaintSquadAnimationState.Run));
            Assert.That(animator.GetFloat("Speed"), Is.GreaterThanOrEqualTo(.35f));
            Assert.That(Vector3.Distance(view.transform.position, view.State.Position), Is.LessThan(.1f));

            // Shoot or Die: keep the battle going (faster) until the unit fires on a tower or falls.
            Time.timeScale = 4; session.SetPlaybackSpeed(3);
            bool shot = false, died = false;
            for (var i = 0; i < 3000 && !shot && !died; i++)
            {
                yield return null;
                if (owner.TryGetView(MemberId, out var live) && live == view)
                {
                    var actions = animator.GetCurrentAnimatorStateInfo(1);
                    var next = animator.GetNextAnimatorStateInfo(1);
                    shot = driver.State == PaintSquadAnimationState.Shoot && (actions.IsName("Shoot") || next.IsName("Shoot"));
                }
                else
                {
                    var fallen = owner.transform.Find("PaintSquad Fallen " + MemberId);
                    Assert.That(fallen, Is.Not.Null, "a dead Paint Squad unit leaves its body to play Die");
                    var body = fallen.GetComponentInChildren<Animator>();
                    Assert.That(body.GetCurrentAnimatorStateInfo(0).IsName("Die") || body.GetNextAnimatorStateInfo(0).IsName("Die"), Is.True);
                    died = true;
                }
            }
            Debug.Log("PaintSquad unit observed " + (shot ? "Shoot" : died ? "Die" : "nothing"));
            session.Simulation.SetPaused(true);
            Assert.That(session.Simulation.Outcome, Is.EqualTo(BattleOutcome.Running), "the test must not trigger the end-of-battle flow");
            Assert.That(shot || died, Is.True, "expected the unit to shoot at a tower or die");
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            try
            {
                Time.timeScale = 1;
                var owner = opened.IsValid() && opened.isLoaded ? opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleSession>()).FirstOrDefault() : null;
                if (owner != null && owner.Simulation != null) owner.Simulation.SetPaused(true);
                if (initial.IsValid() && initial.isLoaded) SceneManager.SetActiveScene(initial);
                if (opened.IsValid() && opened.isLoaded) yield return SceneManager.UnloadSceneAsync(opened);
            }
            finally
            {
                // Runs even if the scene unload throws, so the PlayerPrefs scope is always restored.
                prefs?.Dispose();
            }
        }
    }
}
#endif
