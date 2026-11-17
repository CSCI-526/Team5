using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor
{
    public static class Level1CatgirlBuilder
    {
        private const string ScenePath = "Assets/Rebuild/Scenes/Level1GridBattle.unity";
        [MenuItem("Team5/Rebuild/Level 1/Use Catgirl unit visuals")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs/Level1Catgirl");
            var backup = "Logs/Level1Catgirl/Before.unity";
            if (!File.Exists(backup)) File.Copy(ScenePath,backup);
            var scene = EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var owner = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattlePresentation>(true)).Single();
            var session = owner.Session;
            var sessionBefore = EditorJsonUtility.ToJson(session);
            var platform = owner.GetComponent<SquadPlatformView>();
            if (platform == null || session.ContinuousSquadMovement) throw new InvalidOperationException("Preserve Level1's fixed platform movement mode.");
            var fallback = AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Rebuild/CatgirlFrontTest/FWLowerBody/CatgirlFWLowerBody.prefab");
            if (fallback == null) throw new InvalidOperationException("Missing original Catgirl hybrid prefab.");
            var members = session.CaptureDefinition().Members.ToArray();
            var data = new SerializedObject(owner);
            data.FindProperty("unitPrefab").objectReferenceValue = fallback;
            data.FindProperty("useTestUnitVisual").boolValue = members.Length > 0;
            var list = data.FindProperty("additionalTestVisuals"); list.arraySize = Mathf.Max(0,members.Length-1);
            for (var i=0;i<members.Length;i++)
            {
                var role = members[i].Unit.Role;
                var prefab = role == UnitRole.Attack || role == UnitRole.Tank || role == UnitRole.Heal || role == UnitRole.Captain
                    ? AssetDatabase.LoadAssetAtPath<UnitView>(CatgirlRoleVisualBuilder.PathFor(role)) : fallback;
                if (prefab == null) throw new InvalidOperationException("Missing Catgirl role prefab: "+role);
                if (i==0)
                {
                    data.FindProperty("testVisualMemberId").intValue=members[i].MemberId;
                    data.FindProperty("testUnitPrefab").objectReferenceValue=prefab;
                }
                else
                {
                    var entry=list.GetArrayElementAtIndex(i-1);
                    entry.FindPropertyRelative("memberId").intValue=members[i].MemberId;
                    entry.FindPropertyRelative("prefab").objectReferenceValue=prefab;
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            if(sessionBefore!=EditorJsonUtility.ToJson(session)) throw new InvalidOperationException("Battle session configuration changed.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Logs/Level1Catgirl/BuildChecks.txt", "Level1 fixed platform mode preserved.\nDefault visual: original Catgirl hybrid.\nAuthored members: "+string.Join(", ",members.Select(m=>$"{m.MemberId}/{m.SlotId}/{m.Unit.Role}"))+"\nOnly BattlePresentation visual prefab bindings changed.\n");
            Debug.Log("Level1 Catgirl unit visuals configured; platform, roster and fixed-route system retained.");
        }
    }
}
