using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ReverseTD.Defense.EditorTools
{
    public class StageCatalogBuilder : AssetPostprocessor, IPreprocessBuildWithReport
    {
        private const string ParentFolder = "Assets/Defense";
        private const string ResourcesFolder = ParentFolder + "/Resources";
        private const string CatalogPath = ResourcesFolder + "/" + StageCatalog.ResourceName + ".asset";

        private static bool scheduled;

        public int callbackOrder => 0;

        [MenuItem("Tools/RK/Build Stage Catalog")]
        private static void BuildFromMenu()
        {
            if (!Build())
            {
                Debug.Log($"Stage catalog is up to date: {CatalogPath}");
            }
        }

        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            EditorBuildSettings.sceneListChanged -= Schedule;
            EditorBuildSettings.sceneListChanged += Schedule;
            Schedule();
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload || HasScene(importedAssets) || HasScene(deletedAssets) || HasScene(movedAssets))
            {
                Schedule();
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            Build();
        }

        private static bool HasScene(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i].EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void Schedule()
        {
            if (scheduled)
            {
                return;
            }
            scheduled = true;
            EditorApplication.delayCall += RunScheduled;
        }

        private static void RunScheduled()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunScheduled;
                return;
            }

            scheduled = false;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Build();
            }
        }

        private static bool Build()
        {
            var names = new List<string>();
            var layouts = new List<StageLayout>();
            FindStages(names, layouts);

            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
            if (catalog != null && Matches(catalog, names, layouts))
            {
                return false;
            }

            if (catalog == null)
            {
                if (AssetDatabase.AssetPathExists(CatalogPath))
                {
                    Debug.LogError($"{CatalogPath} exists but isn't a StageCatalog. Move or delete it so the map preview can find its stages.");
                    return false;
                }

                if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                {
                    AssetDatabase.CreateFolder(ParentFolder, "Resources");
                }
                catalog = ScriptableObject.CreateInstance<StageCatalog>();
                Write(catalog, names, layouts);
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            else
            {
                Write(catalog, names, layouts);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }

            Debug.Log($"Stage catalog updated: {names.Count} stages in {CatalogPath}");
            return true;
        }

        private static void FindStages(List<string> names, List<StageLayout> layouts)
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled || string.IsNullOrEmpty(scene.path))
                {
                    continue;
                }

                StageLayout found = null;
                int count = 0;
                foreach (string dependency in AssetDatabase.GetDependencies(scene.path, false))
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(dependency) == typeof(StageLayout))
                    {
                        found = AssetDatabase.LoadAssetAtPath<StageLayout>(dependency);
                        count++;
                    }
                }

                if (count == 1)
                {
                    names.Add(Path.GetFileNameWithoutExtension(scene.path));
                    layouts.Add(found);
                }
                else if (count > 1)
                {
                    Debug.LogWarning($"{scene.path} uses {count} stage layouts, so the map preview leaves it out.");
                }
            }
        }

        private static bool Matches(StageCatalog catalog, List<string> names, List<StageLayout> layouts)
        {
            IReadOnlyList<StageCatalogEntry> entries = catalog.Entries;
            if (entries.Count != names.Count)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].SceneName != names[i] || entries[i].Layout != layouts[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static void Write(StageCatalog catalog, List<string> names, List<StageLayout> layouts)
        {
            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = names.Count;
            for (int i = 0; i < names.Count; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("sceneName").stringValue = names[i];
                entry.FindPropertyRelative("layout").objectReferenceValue = layouts[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
