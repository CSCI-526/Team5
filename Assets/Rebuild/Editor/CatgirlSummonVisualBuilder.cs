using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    public static class CatgirlSummonVisualBuilder
    {
        public const string CatalogPath = CatgirlRoleVisualBuilder.Folder + "/CatgirlAllRoles.asset";
        public static readonly string[] Scenes = Enumerable.Range(1,7).Select(i=>$"Assets/Rebuild/Scenes/Level{i}GridBattle.unity")
            .Concat(new[]{"Assets/Rebuild/Scenes/TestLevel.unity","Assets/Rebuild/Scenes/1.unity"}).ToArray();

        [MenuItem("Team5/Rebuild/Catgirl Front/Create summon role colors and apply")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs/CatgirlAllRoles/Before");
            var roles = ((UnitRole[])Enum.GetValues(typeof(UnitRole))).Where(r=>r!=UnitRole.Test).ToArray();
            foreach(var role in roles)
            {
                var path=CatgirlRoleVisualBuilder.PathFor(role);
                if(File.Exists(path))continue; // Preserve the original four role prefabs and manual edits.
                if(!AssetDatabase.CopyAsset(CatgirlRoleVisualBuilder.PathFor(UnitRole.Attack),path)) throw new IOException("Cannot copy Catgirl for "+role);
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.name="Catgirl "+role;
                    var rig=root.GetComponentsInChildren<Animator>(true).Single().transform;
                    var donor=rig.Find("FW Lower Body");
                    var hair=root.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>(s.name=="F_HAIR_FRONT"||s.name=="F_B_HAIR")&&!s.transform.IsChildOf(donor)).ToArray();
                    if(hair.Length!=2)throw new InvalidOperationException("Expected Catgirl front/rear hair layers.");
                    foreach(var sprite in hair)sprite.color=LegacyUnitLooks.For(role).Body;
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var catalog=AssetDatabase.LoadAssetAtPath<UnitRoleVisualSet>(CatalogPath);
            if(catalog==null){catalog=ScriptableObject.CreateInstance<UnitRoleVisualSet>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
            var catalogData=new SerializedObject(catalog);var entries=catalogData.FindProperty("entries");entries.arraySize=roles.Length;
            for(var i=0;i<roles.Length;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);entry.FindPropertyRelative("role").intValue=(int)roles[i];
                entry.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<UnitView>(CatgirlRoleVisualBuilder.PathFor(roles[i]));
            }
            catalogData.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            foreach(var path in Scenes)
            {
                var backup="Logs/CatgirlAllRoles/Before/"+Path.GetFileName(path);
                if(!File.Exists(backup))File.Copy(path,backup);
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                // Opening a scene may unload a catalog that has no saved scene references yet.
                var sceneCatalog=AssetDatabase.LoadAssetAtPath<UnitRoleVisualSet>(CatalogPath);
                if(sceneCatalog==null)throw new InvalidOperationException("Cannot load role catalog for "+path);
                var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>(true)).Single();
                var before=EditorJsonUtility.ToJson(owner.Session);
                var data=new SerializedObject(owner);data.FindProperty("roleVisuals").objectReferenceValue=sceneCatalog;data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(owner);
                if(PrefabUtility.IsPartOfPrefabInstance(owner))PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
                if(before!=EditorJsonUtility.ToJson(owner.Session))throw new InvalidOperationException("Session configuration changed in "+path);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                if(!File.ReadAllText(path).Contains(AssetDatabase.AssetPathToGUID(CatalogPath)))
                    throw new InvalidOperationException("Role catalog reference was not saved in "+path);
            }
            File.WriteAllLines("Logs/CatgirlAllRoles/RoleColors.txt",roles.Select(r=>$"{r}: #{ColorUtility.ToHtmlStringRGB(LegacyUnitLooks.For(r).Body)} | {CatgirlRoleVisualBuilder.PathFor(r)}"));
            Debug.Log($"Catgirl catalog configured: {roles.Length} roles in {Scenes.Length} battle scenes. Summon rules and original prefabs preserved.");
        }
    }
}
