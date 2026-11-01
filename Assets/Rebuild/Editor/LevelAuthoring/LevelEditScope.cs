using System;
using System.Collections.Generic;
using Team5.Rebuild.Session;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Editor.LevelAuthoring
{
    // One user action = one Undo group. Scene-only; never records shared project assets.
    public sealed class LevelEditScope : IDisposable
    {
        private readonly Scene scene;
        private readonly int group;
        private readonly string label;
        private readonly HashSet<Object> recorded = new HashSet<Object>();
        private bool committed, disposed;

        public LevelEditScope(Scene scene, string label)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Level edits require a loaded scene in Edit Mode.");
            if (LevelTemplateUtility.IsTemplate(scene.path))
                throw new InvalidOperationException("Create a copy before editing the template.");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene == scene)
                throw new InvalidOperationException("Edit a level scene, not a prefab asset.");
            this.scene = scene;
            this.label = label;
            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
        }

        private void Check(Object target)
        {
            if (disposed || committed) throw new ObjectDisposedException(nameof(LevelEditScope));
            var go = target as GameObject ?? (target as Component)?.gameObject;
            if (go == null || EditorUtility.IsPersistent(target) || go.scene != scene)
                throw new ArgumentException("Only objects belonging to this level scene can be edited.");
        }

        public void Record(Object target)
        {
            Check(target);
            if (recorded.Add(target)) Undo.RegisterCompleteObjectUndo(target, label);
        }

        public void RegisterCreated(GameObject target)
        {
            Check(target);
            Undo.RegisterCreatedObjectUndo(target, label);
        }

        public void Destroy(GameObject target)
        {
            Check(target);
            Undo.DestroyObjectImmediate(target);
        }

        public void Commit()
        {
            if (disposed || committed) throw new ObjectDisposedException(nameof(LevelEditScope));
            foreach (var target in recorded)
                if (target != null && PrefabUtility.IsPartOfPrefabInstance(target))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            committed = true;
        }

        public void Dispose()
        {
            if (disposed) return;
            if (!committed) Undo.RevertAllDownToGroup(group);
            disposed = true;
        }

        public static void SetDisplayedLevelNumber(BattleSession session, int number)
        {
            if (session == null || number < 1) throw new ArgumentException("A session and positive level number are required.");
            if (session.DisplayedLevelNumber == number) return;
            using (var edit = new LevelEditScope(session.gameObject.scene, "Set Level Number"))
            {
                edit.Record(session);
                var serialized = new SerializedObject(session);
                serialized.FindProperty("displayedLevelNumber").intValue = number;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                edit.Commit();
            }
        }
    }
}
