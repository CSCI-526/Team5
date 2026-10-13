using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ReverseTD.Defense.EditorTools
{
    public static class DefenseSandboxBuilder
    {
        private const string DataFolder = "Assets/Defense/Data";
        private const string ScenesFolder = "Assets/Defense/Scenes";
        private const string TowerAssetPath = DataFolder + "/DefaultTower.asset";
        private const string StageAssetPath = DataFolder + "/DefaultStage.asset";
        private const string ScenePath = ScenesFolder + "/DefenseSandbox.unity";

        private static readonly Color backgroundColor = new Color(0.12f, 0.14f, 0.17f);

        private static readonly Vector2[] defaultWaypoints =
        {
            new Vector2(-8f, 3f),
            new Vector2(5f, 3f),
            new Vector2(5f, 0f),
            new Vector2(-5f, 0f),
            new Vector2(-5f, -3f),
            new Vector2(8f, -3f),
        };

        private static readonly Vector2[] defaultTowerPositions =
        {
            new Vector2(-1.5f, 1.5f),
            new Vector2(3f, 1.5f),
            new Vector2(-3f, -1.5f),
            new Vector2(1.5f, -1.5f),
        };

        [MenuItem("Tools/RK/Build Defense Sandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Exit Play mode before building the Defense sandbox.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder(DataFolder);
            EnsureFolder(ScenesFolder);

            TowerDefinition tower = LoadOrCreateTower();
            if (tower == null)
            {
                return;
            }

            if (LoadOrCreateStage(tower) == null)
            {
                return;
            }

            BuildScene();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            int slash = folderPath.LastIndexOf('/');
            AssetDatabase.CreateFolder(folderPath.Substring(0, slash), folderPath.Substring(slash + 1));
        }

        private static TowerDefinition LoadOrCreateTower()
        {
            var tower = AssetDatabase.LoadAssetAtPath<TowerDefinition>(TowerAssetPath);
            if (tower != null)
            {
                return tower;
            }

            if (!IsPathFree<TowerDefinition>(TowerAssetPath))
            {
                return null;
            }

            tower = ScriptableObject.CreateInstance<TowerDefinition>();
            AssetDatabase.CreateAsset(tower, TowerAssetPath);
            return tower;
        }

        private static StageLayout LoadOrCreateStage(TowerDefinition tower)
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageLayout>(StageAssetPath);
            if (stage != null)
            {
                return stage;
            }

            if (!IsPathFree<StageLayout>(StageAssetPath))
            {
                return null;
            }

            stage = ScriptableObject.CreateInstance<StageLayout>();
            var serialized = new SerializedObject(stage);

            SerializedProperty waypoints = FindField(serialized, "waypoints");
            waypoints.arraySize = defaultWaypoints.Length;
            for (int i = 0; i < defaultWaypoints.Length; i++)
            {
                waypoints.GetArrayElementAtIndex(i).vector2Value = defaultWaypoints[i];
            }

            SerializedProperty slots = FindField(serialized, "towerSlots");
            slots.arraySize = defaultTowerPositions.Length;
            for (int i = 0; i < defaultTowerPositions.Length; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                FindField(slot, "position").vector2Value = defaultTowerPositions[i];
                FindField(slot, "definition").objectReferenceValue = tower;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(stage, StageAssetPath);
            return stage;
        }

        private static bool IsPathFree<T>(string assetPath)
        {
            if (!AssetDatabase.AssetPathExists(assetPath))
            {
                return true;
            }

            Debug.LogError($"{assetPath} exists but isn't a {typeof(T).Name}. Move or delete it, then run the builder again.");
            return false;
        }

        private static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var stage = AssetDatabase.LoadAssetAtPath<StageLayout>(StageAssetPath);
            if (stage == null)
            {
                Debug.LogError($"Couldn't load {StageAssetPath}.");
                return;
            }

            var board = new GameObject("Board");
            var boardView = board.AddComponent<BoardView>();
            SetReference(boardView, "layout", stage);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            cameraObject.AddComponent<AudioListener>();
            var boardCamera = cameraObject.AddComponent<BoardCamera>();
            SetReference(boardCamera, "board", boardView);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"Couldn't save {ScenePath}.");
                return;
            }

            Selection.activeGameObject = board;
            FrameSceneView(stage.CalculateBounds());
            Debug.Log($"Defense sandbox built: {ScenePath}");
        }

        private static void FrameSceneView(Bounds bounds)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                return;
            }

            view.in2DMode = true;
            view.drawGizmos = true;
            view.Frame(bounds, true);
        }

        private static void SetReference(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = FindField(serialized, fieldName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (property.objectReferenceValue == null)
            {
                throw new InvalidOperationException($"Couldn't set {target.GetType().Name}.{fieldName}.");
            }
        }

        private static SerializedProperty FindField(SerializedObject owner, string fieldName)
        {
            SerializedProperty property = owner.FindProperty(fieldName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{owner.targetObject.GetType().Name} has no serialized field '{fieldName}'. Update DefenseSandboxBuilder.");
            }
            return property;
        }

        private static SerializedProperty FindField(SerializedProperty owner, string fieldName)
        {
            SerializedProperty property = owner.FindPropertyRelative(fieldName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{owner.propertyPath} has no serialized field '{fieldName}'. Update DefenseSandboxBuilder.");
            }
            return property;
        }
    }
}
