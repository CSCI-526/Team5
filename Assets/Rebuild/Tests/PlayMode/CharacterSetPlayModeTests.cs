#if UNITY_EDITOR
using System.Collections;
using System.Linq;
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

namespace Team5.Rebuild.Tests
{
    public sealed class CharacterSetPlayModeTests
    {
        private const string Level1 = "Assets/Rebuild/Scenes/Level1GridBattle.unity";
        private const string CatgirlCatalog = "Assets/Rebuild/CatgirlFrontTest/RoleVisuals/CatgirlAllRoles.asset";
        private const string PaintSquadCatalog = "Assets/Rebuild/PaintSquad/RoleVisuals/PaintSquadAllRoles.asset";
        private Scene initial, opened;
        private CharacterSetPrefsScope prefs;

        [SetUp]
        public void SavePrefs() => prefs = new CharacterSetPrefsScope();

        // Level 1 serializes the Catgirl catalog plus a member override forcing the Catgirl Captain (member 110).
        [UnityTest]
        public IEnumerator Level1UsesTheSelectedSetIncludingTheOverriddenCaptain(
            [Values(CharacterSet.PaintSquad, CharacterSet.Catgirl)] CharacterSet set)
        {
            CharacterSetSelection.Set(set);
            initial = SceneManager.GetActiveScene(); Time.timeScale = 1;
            EditorSceneManager.LoadSceneInPlayMode(Level1, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; yield return null;
            opened = SceneManager.GetSceneByPath(Level1); SceneManager.SetActiveScene(opened);
            foreach (var guide in opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelOneTutorial>())) guide.Skip();
            var owner = opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattlePresentation>()).Single();
            var session = owner.Session;
            var fixture = new SerializedObject(session);
            fixture.FindProperty("initialRoster").objectReferenceValue = null;
            var members = fixture.FindProperty("members"); members.arraySize = 2;
            for (var i = 0; i < 2; i++)
            {
                var member = members.GetArrayElementAtIndex(i);
                member.FindPropertyRelative("enabled").boolValue = true;
                member.FindPropertyRelative("memberId").intValue = 110 + i;
                member.FindPropertyRelative("slotId").intValue = i == 0 ? 4 : 3;
                member.FindPropertyRelative("role").intValue = (int)(i == 0 ? UnitRole.Captain : UnitRole.Attack);
                member.FindPropertyRelative("spawnTime").floatValue = 0;
            }
            fixture.ApplyModifiedPropertiesWithoutUndo();
            session.StartBattle();
            foreach (var guide in opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelOneTutorial>())) guide.Skip();
            session.Simulation.SetPaused(false); session.AdvancePlaybackFrame(.05f);
            yield return null; yield return null;
            session.Simulation.SetPaused(true);

            var serialized = new SerializedObject(owner).FindProperty("roleVisuals").objectReferenceValue;
            Assert.That(AssetDatabase.GetAssetPath(serialized), Is.EqualTo(CatgirlCatalog), "the scene asset itself is unchanged");
            Assert.That(AssetDatabase.GetAssetPath(owner.EffectiveRoleVisuals),
                Is.EqualTo(set == CharacterSet.PaintSquad ? PaintSquadCatalog : CatgirlCatalog));
            Assert.That(owner.TryGetView(110, out var captain), Is.True);
            Assert.That(owner.TryGetView(111, out var attack), Is.True);
            foreach (var view in new[] { captain, attack })
            {
                if (set == CharacterSet.PaintSquad)
                {
                    Assert.That(view.GetComponent<PaintSquadUnitAnimation>(), Is.Not.Null, view.State.Role.ToString());
                    Assert.That(view.GetComponent<FWUnitVisual>(), Is.Null);
                }
                else
                {
                    Assert.That(view.GetComponent<FWUnitVisual>(), Is.Not.Null, view.State.Role.ToString());
                    Assert.That(view.GetComponent<PaintSquadUnitAnimation>(), Is.Null);
                }
            }
            if (set == CharacterSet.PaintSquad)
                Assert.That(captain.GetComponent<PaintSquadUnitAnimation>().CharacterAnimator.runtimeAnimatorController.name, Is.EqualTo("Hero"),
                    "the Captain must be the Paint Squad Captain (Hero), not the scene's Catgirl override");
        }

        [UnityTest]
        public IEnumerator SelectScreenStoresTheTappedSetAndContinues()
        {
            CharacterSetSelection.Set(CharacterSet.PaintSquad);
            CharacterSet? continued = null;
            var screen = CharacterSelectScreen.Show(set => continued = set);
            yield return null;
            Assert.That(CharacterSelectScreen.Active, Is.SameAs(screen));
            Assert.That(CharacterSelectScreen.Show(_ => { }), Is.SameAs(screen), "a second Start Game tap reuses the open screen");
            var texts = screen.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text).ToArray();
            Assert.That(texts, Does.Contain(CharacterSelectScreen.Title));
            Assert.That(texts, Does.Contain("PAINT SQUAD"));
            Assert.That(texts, Does.Contain("CATGIRL"));
            foreach (var set in new[] { CharacterSet.PaintSquad, CharacterSet.Catgirl })
            {
                var card = screen.CardButton(set);
                Assert.That(card, Is.Not.Null, set.ToString());
                var preview = card.transform.GetComponentsInChildren<UnityEngine.UI.Image>().FirstOrDefault(i => i.name == "Captain Preview");
                Assert.That(preview, Is.Not.Null); Assert.That(preview.sprite, Is.Not.Null);
            }
            screen.CardButton(CharacterSet.Catgirl).onClick.Invoke();
            yield return null;
            Assert.That(continued, Is.EqualTo(CharacterSet.Catgirl));
            Assert.That(CharacterSetSelection.Current, Is.EqualTo(CharacterSet.Catgirl));
            Assert.That(PlayerPrefs.GetString(CharacterSetSelection.PrefsKey), Is.EqualTo("Catgirl"));
            Assert.That(CharacterSelectScreen.Active, Is.Null);
            Assert.That(screen == null, Is.True, "the screen closes after a pick");
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            try
            {
                Time.timeScale = 1;
                if (CharacterSelectScreen.Active != null) CharacterSelectScreen.Active.Close();
                var owner = opened.IsValid() && opened.isLoaded ? opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleSession>()).FirstOrDefault() : null;
                if (owner != null && owner.Simulation != null) owner.Simulation.SetPaused(true);
                if (initial.IsValid() && initial.isLoaded) SceneManager.SetActiveScene(initial);
                if (opened.IsValid() && opened.isLoaded) yield return SceneManager.UnloadSceneAsync(opened);
                opened = default;
            }
            finally
            {
                // Runs even if the scene unload throws, so the PlayerPrefs scope is always restored.
                prefs.Dispose();
            }
        }
    }
}
#endif
