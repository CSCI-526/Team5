using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor
{
    public static class FWVisualPrototypeBuilder
    {
        public const string Folder = "Assets/Rebuild/FWTest";
        public const string Visual = Folder + "/FWUnitVisual.prefab";
        public const string TestScene = "Assets/Rebuild/Scenes/FWUnitVisualTest.unity";

        [MenuItem("Team5/Rebuild/FW Visual/Build prototype assets")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FW_MIHO/prefabs/Miho.prefab");
            if (source == null) throw new InvalidOperationException("Import FW and resolve 2D dependencies first.");
            if (File.Exists(Visual) || File.Exists(TestScene))
                throw new InvalidOperationException("Prototype assets already exist; they are never silently overwritten.");
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("URP sprite shader is missing.");
            var material = new Material(shader) { name = "FW Sprite Depth Tested" };
            material.SetFloat("_ZWrite", 0);
            AssetDatabase.CreateAsset(material, Folder + "/FWSprite.mat");
            var root = new GameObject("FW SD Unit Visual");
            try
            {
                root.AddComponent<UnitView>();
                var driver = root.AddComponent<FWUnitVisual>();
                var billboard = new GameObject("Camera Facing Visual").transform;
                billboard.SetParent(root.transform, false);
                var facing = new GameObject("Mirror Entire Character").transform;
                facing.SetParent(billboard, false);
                var group = billboard.gameObject.AddComponent<SortingGroup>();
                var character = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(character, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                character.transform.SetParent(facing, false);
                character.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                character.transform.localScale = Vector3.one;
                // Remove only the demo movement behaviour from our copy. Keep every SpriteSkin.
                foreach (var behaviour in character.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null && behaviour.GetType().FullName == "FreewrokGame.AniManagers")
                        UnityEngine.Object.DestroyImmediate(behaviour);
                var shadow = character.transform.Find("shadow");
                if (shadow != null) UnityEngine.Object.DestroyImmediate(shadow.gameObject);
                var animators = character.GetComponentsInChildren<Animator>(true);
                var front = animators.Single(a => a.name == "miho_forward");
                var back = animators.Single(a => a.name == "miho_BACK");
                Configure(front, "F_IDLE01", "F_RUN01", "Front");
                Configure(back, "B_IDLE01", "B_RUN01", "Back");
                foreach (var sprite in character.GetComponentsInChildren<SpriteRenderer>(true))
                    sprite.sharedMaterial = material;
                // Fit each view to the same visible height and foot origin. No collision data is read or written.
                Fit(front.transform, 1.65f);
                Fit(back.transform, 1.65f);
                front.gameObject.SetActive(true);
                back.gameObject.SetActive(false);
                var serialized = new SerializedObject(driver);
                Set(serialized, "billboard", billboard);
                Set(serialized, "facingRoot", facing);
                Set(serialized, "front", front);
                Set(serialized, "back", back);
                Set(serialized, "sortingGroup", group);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Visual);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            CreateTestScene();
            AssetDatabase.SaveAssets();
            Debug.Log("FW prototype built: " + TestScene);
        }

        private static void Configure(Animator animator, string idle, string run, string suffix)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/FW" + suffix + ".controller");
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var idleState = machine.AddState("Idle");
            idleState.motion = Clip(idle);
            var runState = machine.AddState("Run");
            runState.motion = Clip(run);
            machine.defaultState = idleState;
            Transition(idleState, runState, true);
            Transition(runState, idleState, false);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FW_MIHO/Animation/" + name + ".anim")
            ?? throw new InvalidOperationException("Missing FW clip: " + name);

        private static void Transition(AnimatorState from, AnimatorState to, bool moving)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = .12f;
            transition.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "IsMoving");
        }

        private static void Fit(Transform rig, float height)
        {
            rig.gameObject.SetActive(true);
            var sprites = rig.GetComponentsInChildren<SpriteRenderer>().Where(s => s.enabled && s.sprite != null).ToArray();
            if (sprites.Length == 0) throw new InvalidOperationException("PSD import produced no sprites for " + rig.name);
            var bounds = sprites[0].bounds;
            foreach (var sprite in sprites.Skip(1)) bounds.Encapsulate(sprite.bounds);
            var factor = height / Mathf.Max(bounds.size.y, .001f);
            // All generated roots are axis aligned here. Align the actual visible feet, not PSB canvas bounds.
            var center = new Vector3(bounds.center.x, bounds.min.y, 0);
            rig.localScale *= factor;
            rig.localPosition = -center * factor;
        }

        private static void CreateTestScene()
        {
            if (!AssetDatabase.CopyAsset("Assets/Rebuild/Scenes/HaonUnitTest.unity", TestScene))
                throw new IOException("Cannot copy existing test scene.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Additive);
            try
            {
                var presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattlePresentation>(true)).Single();
                var visual = AssetDatabase.LoadAssetAtPath<UnitView>(Visual);
                var serialized = new SerializedObject(presentation);
                Set(serialized, "testUnitPrefab", visual);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var toggle = presentation.gameObject.AddComponent<FWVisualTestSwitch>();
                serialized = new SerializedObject(toggle);
                Set(serialized, "presentation", presentation);
                Set(serialized, "visual2D", visual);
                Set(serialized, "visual3D", AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Rebuild/HaonTest/HaonUnitVisual.prefab"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Set(SerializedObject target, string name, UnityEngine.Object value) => target.FindProperty(name).objectReferenceValue = value;
    }
}
