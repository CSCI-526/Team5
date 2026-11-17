#if UNITY_EDITOR
using System;
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
using Object=UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    public sealed class CatgirlSummonPlayModeTests
    {
        // These scenes are authored for Catgirl; pin that set so the player's stored choice cannot change them.
        private CharacterSetPrefsScope characterSet;
        [SetUp] public void PinCatgirlSet() => characterSet = new CharacterSetPrefsScope(CharacterSet.Catgirl);
        [TearDown] public void RestoreCharacterSet() => characterSet?.Dispose();
        private Scene initial,opened;
        [UnityTest] public IEnumerator DynamicMemberIdsUseRoleColorsAndIdleRunWithoutChangingBattleMode()
        {
            initial=SceneManager.GetActiveScene();Time.timeScale=1;
            const string path="Assets/Rebuild/Scenes/Level2GridBattle.unity";
            EditorSceneManager.LoadSceneInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;yield return null;
            opened=SceneManager.GetSceneByPath(path);SceneManager.SetActiveScene(opened);
            var owner=opened.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>()).Single();
            var session=owner.Session;var originalMode=session.ContinuousSquadMovement;
            var roles=((UnitRole[])Enum.GetValues(typeof(UnitRole))).Where(r=>r!=UnitRole.Test).ToArray();
            for(var batch=0;batch<2;batch++)
            {
                var selected=roles.Skip(batch*6).Take(6).ToArray();
                var fixture=new SerializedObject(session);fixture.FindProperty("initialRoster").objectReferenceValue=null;
                var members=fixture.FindProperty("members");members.arraySize=selected.Length;
                for(var i=0;i<selected.Length;i++)
                {
                    var member=members.GetArrayElementAtIndex(i);
                    member.FindPropertyRelative("enabled").boolValue=true;
                    member.FindPropertyRelative("memberId").intValue=700+batch*100+i;
                    member.FindPropertyRelative("slotId").intValue=i;
                    member.FindPropertyRelative("role").intValue=(int)selected[i];
                    member.FindPropertyRelative("spawnTime").floatValue=0;
                }
                fixture.ApplyModifiedPropertiesWithoutUndo();session.StartBattle();session.Simulation.SetPaused(false);session.AdvancePlaybackFrame(.08f);
                yield return new WaitForSecondsRealtime(.06f);yield return null;
                for(var i=0;i<selected.Length;i++)
                {
                    Assert.That(owner.TryGetView(700+batch*100+i,out var view),Is.True);
                    Assert.That(view.State.Role,Is.EqualTo(selected[i]));Assert.That(view.State.SlotId,Is.EqualTo(i));
                    var driver=view.GetComponent<FWUnitVisual>();Assert.That(driver,Is.Not.Null);
                    Assert.That(view.transform.Find("Body"),Is.Null);
                    var hair=view.GetComponentsInChildren<SpriteRenderer>().First(s=>s.name=="F_HAIR_FRONT");
                    Assert.That(hair.color,Is.EqualTo(LegacyUnitLooks.For(selected[i]).Body));
                    Assert.That(driver.ActiveAnimator.applyRootMotion,Is.False);Assert.That(driver.IsMoving,Is.EqualTo(!view.IsRidingPlatform));
                    Assert.That(session.ContinuousSquadMovement,Is.EqualTo(originalMode));
                }
                session.Simulation.SetPaused(true);yield return null;yield return null;
                foreach(var unit in session.Simulation.Units)
                {
                    Assert.That(owner.TryGetView(unit.Id,out var view),Is.True);
                    var driver=view.GetComponent<FWUnitVisual>();Assert.That(driver.IsMoving,Is.False);
                    Assert.That(driver.ActiveAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),Is.True);
                }
            }
        }
        [UnityTearDown] public IEnumerator Close()
        {
            Time.timeScale=1;if(initial.IsValid()&&initial.isLoaded)SceneManager.SetActiveScene(initial);
            if(opened.IsValid()&&opened.isLoaded)yield return SceneManager.UnloadSceneAsync(opened);
        }
    }
}
#endif
