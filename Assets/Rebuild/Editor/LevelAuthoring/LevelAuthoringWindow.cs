using System;
using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Session;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor.LevelAuthoring
{
    public sealed class LevelAuthoringWindow : EditorWindow
    {
        private List<LevelValidationIssue> issues;
        private Vector2 scroll;
        private int number = 1;
        private BattleSession selectedSession;

        [MenuItem("Team5/Rebuild/Level Authoring (Stage 1)")]
        public static void Open() => GetWindow<LevelAuthoringWindow>("Level Authoring");
        private void OnEnable() => Undo.undoRedoPerformed += Refresh;
        private void OnDisable() => Undo.undoRedoPerformed -= Refresh;
        private void OnHierarchyChange() => Refresh();
        private void Refresh() { issues = null; selectedSession = null; Repaint(); }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Stage 1: scene template, safe copies, basic validation and Undo. Map brushes are not included.", MessageType.Info);
            if (GUILayout.Button("Open 2D Map Builder (custom map size)")) MapBuilderWindow.Open();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.LabelField("Level Template", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Template", AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelTemplateUtility.TemplateScenePath), typeof(SceneAsset), false);
                if (GUILayout.Button("Open LevelTemplate"))
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorSceneManager.OpenScene(LevelTemplateUtility.TemplateScenePath);
                }
                if (GUILayout.Button("Create Level from Template…")) CreateCopy();
                EditorGUILayout.Space();
                var scene = SceneManager.GetActiveScene();
                EditorGUILayout.LabelField("Active Scene", scene.name);
                EditorGUILayout.LabelField("Asset Path", string.IsNullOrEmpty(scene.path) ? "Not saved yet" : scene.path);
                var sessions = scene.IsValid() && scene.isLoaded ? LevelValidation.Components<BattleSession>(scene) : Array.Empty<BattleSession>();
                var session = sessions.Length == 1 ? sessions[0] : null;
                if (session != selectedSession) { selectedSession = session; number = session != null ? session.DisplayedLevelNumber : 1; }
                var template = LevelTemplateUtility.IsTemplate(scene.path);
                if (template) EditorGUILayout.HelpBox("The template is read-only in this tool. Create a copy to edit level settings. You can still validate it or use Unity Play.", MessageType.Info);
                using (new EditorGUI.DisabledScope(template || session == null))
                {
                    number = EditorGUILayout.IntField("Displayed Level Number", number);
                    if (GUILayout.Button("Apply Level Number (Undo)"))
                    {
                        try { LevelEditScope.SetDisplayedLevelNumber(session, number); }
                        catch (Exception error) { EditorUtility.DisplayDialog("Level settings", error.Message, "OK"); }
                    }
                }
                EditorGUILayout.LabelField("Ctrl+Z / Ctrl+Y undo or redo a scene edit. Save the scene with Ctrl+S.", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Validate Active Level")) issues = LevelValidation.Validate(scene);
                if (issues != null)
                {
                    if (!issues.Any(issue => issue.IsError)) EditorGUILayout.HelpBox("Basic validation passed. This does not register the scene with a game flow.", MessageType.Info);
                    foreach (var issue in issues)
                    {
                        EditorGUILayout.HelpBox(issue.Message, issue.IsError ? MessageType.Error : MessageType.Warning);
                        if (issue.Context != null && GUILayout.Button("Select: " + issue.Context.name))
                        { Selection.activeObject = issue.Context; EditorGUIUtility.PingObject(issue.Context); }
                    }
                }
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Future Test Target Interface", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Stage 1 defines GUID-based scene references and provider/selection contracts only. Set as Test Level is not connected yet. The existing test flow is unchanged.", MessageType.Info);
                EditorGUILayout.LabelField("Current legacy target", BattleSceneFlow.TestLevelScene);
            }
            EditorGUILayout.EndScrollView();
        }

        private void CreateCopy()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Level from Template", "NewLevel", "unity", "Choose a NEW scene name. Existing files and TestLevel are protected.", "Assets/Rebuild/Scenes");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var asset = LevelTemplateUtility.CreateLevelFromTemplate(path);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                if (EditorUtility.DisplayDialog("Level created", "Created " + AssetDatabase.GetAssetPath(asset) + ".\nThe test target and build list were not changed. Open the new scene?", "Open", "Keep current scene") &&
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(asset));
            }
            catch (Exception error) { EditorUtility.DisplayDialog("Level not created", error.Message, "OK"); }
        }
    }
}
