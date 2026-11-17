#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class CaptainFormationDeathTests
    {
        private const string Path="Assets/Rebuild/Scenes/Level3GridBattle.unity";
        private Scene initial;
        [UnityTest] public IEnumerator CaptainDeathZerosBudgetShowsReasonAndRestartRestoresIt()
        {
            Application.runInBackground=true;initial=SceneManager.GetActiveScene();
            EditorSceneManager.LoadSceneInPlayMode(Path,new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;yield return null;yield return null;
            var scene=SceneManager.GetSceneByPath(Path);SceneManager.SetActiveScene(scene);
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>()).Single();
            var hud=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LegacyBattleHud>()).Single();
            var session=owner.Session;session.enabled=false;
            foreach(var tower in session.Towers)
            {var data=new SerializedObject(tower);data.FindProperty("range").floatValue=0;data.ApplyModifiedPropertiesWithoutUndo();}
            var attacker=session.Towers.First(t=>!t.IsWall);var originalPosition=attacker.transform.position;
            var weapon=new SerializedObject(attacker);weapon.FindProperty("range").floatValue=.75f;
            weapon.FindProperty("damage").floatValue=999;weapon.FindProperty("attackInterval").floatValue=.1f;
            weapon.FindProperty("attackArea").intValue=(int)AttackArea.Square;weapon.FindProperty("targetRule").intValue=(int)TargetRule.Nearest;
            weapon.ApplyModifiedPropertiesWithoutUndo();
            var settings=new SerializedObject(session);settings.FindProperty("initialRoster").objectReferenceValue=null;
            var members=settings.FindProperty("members");members.arraySize=2;
            for(var i=0;i<2;i++)
            {
                var member=members.GetArrayElementAtIndex(i);member.FindPropertyRelative("enabled").boolValue=true;
                member.FindPropertyRelative("memberId").intValue=901+i;member.FindPropertyRelative("slotId").intValue=i==0?4:0;
                member.FindPropertyRelative("role").intValue=(int)(i==0?UnitRole.Captain:UnitRole.Tank);member.FindPropertyRelative("spawnTime").floatValue=0;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();session.StartBattle();
            Assert.That(hud.FormationBudget.CaptainDefeated,Is.False,"An unspawned captain is not dead.");
            session.AdvancePlaybackFrame(.02f);yield return null;
            Assert.That(hud.FormationBudget.Remaining,Is.EqualTo(3));Assert.That(hud.CanEditFormation,Is.True);
            var captain=session.Simulation.Units.Single(u=>u.Role==UnitRole.Captain);
            var label=hud.SquadButton(0).GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="Captain Defeated");
            Assert.That(label.gameObject.activeSelf,Is.False);
            attacker.transform.position=captain.Position+Vector3.forward*.5f;
            for(var i=0;i<100&&captain.IsAlive;i++)session.AdvancePlaybackFrame(.02f);
            Assert.That(captain.LifeState,Is.EqualTo(UnitLifeState.Dead));
            Assert.That(session.Simulation.AliveCount,Is.EqualTo(1),"The surviving squad keeps the battle running.");
            yield return null;yield return null;
            Assert.That(hud.FormationBudget.CaptainDefeated,Is.True);Assert.That(hud.FormationBudget.Remaining,Is.Zero);
            Assert.That(hud.FormationBudget.HasChangesLeft,Is.False);Assert.That(hud.SquadButton(0).interactable,Is.False);
            Assert.That(label.gameObject.activeInHierarchy,Is.True);Assert.That(label.raycastTarget,Is.False);
            var badge=hud.SquadButton(0).GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="Count");
            Assert.That(badge.text,Is.EqualTo("0"));Assert.That(badge.enabled,Is.True);
            settings.Update();settings.FindProperty("displayedLevelNumber").intValue=1;settings.ApplyModifiedPropertiesWithoutUndo();
            yield return null;
            Assert.That(hud.FormationBudget.IsLimited,Is.False);Assert.That(hud.FormationBudget.Remaining,Is.Zero);
            Assert.That(badge.enabled,Is.True,"Even an otherwise unlimited level displays 0 after captain death.");
            settings.Update();settings.FindProperty("displayedLevelNumber").intValue=3;settings.ApplyModifiedPropertiesWithoutUndo();
            attacker.transform.position=originalPosition;session.StartBattle();session.AdvancePlaybackFrame(.02f);
            yield return null;yield return null;
            Assert.That(hud.FormationBudget.Remaining,Is.EqualTo(3));Assert.That(hud.FormationBudget.CaptainDefeated,Is.False);
            Assert.That(label.gameObject.activeSelf,Is.False);Assert.That(hud.CanEditFormation,Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1;var scene=SceneManager.GetSceneByPath(Path);
            if(initial.IsValid()&&initial.isLoaded)SceneManager.SetActiveScene(initial);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
