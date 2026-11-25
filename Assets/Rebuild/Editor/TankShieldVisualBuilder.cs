using System;
using System.Linq;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    public static class TankShieldVisualBuilder
    {
        private const int PortraitPixels = 256;
        private const float PortraitPitch = 16f;
        private const float PortraitYaw = 200f;
        private const float PortraitMargin = 1.04f;
        private const string PortraitPosePath = PaintSquadRoleVisualBuilder.Folder + "/PaintSquadTankPortraitPose.prefab";
        private const string ShieldPrefix = "Riot Shield";
        private const float PortraitShieldTurn = -45f;
        private static readonly Vector3 PortraitShieldShift = new Vector3(-.44f, 0f, -.28f);

        [MenuItem("Team5/Rebuild/Paint Squad/Build Tank shield")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var openScenes = EditorSceneManager.GetSceneManagerSetup();
            PaintSquadRoleVisualBuilder.BuildRoles(UnitRole.Tank);
            SavePortraitPose();
            try
            {
                PaintSquadCharacterSetBuilder.Render(PortraitPosePath, PaintSquadCharacterSetBuilder.PortraitPath(UnitRole.Tank),
                    PortraitPixels, PortraitPitch, PortraitYaw, PortraitMargin);
                AssetDatabase.Refresh();
            }
            finally
            {
                if (openScenes.Length > 0 && openScenes.All(scene => !string.IsNullOrEmpty(scene.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(openScenes);
                AssetDatabase.DeleteAsset(PortraitPosePath);
            }
            Debug.Log("PAINT_SQUAD_TANK_SHIELD_BUILT " + PaintSquadRoleVisualBuilder.PrefabPath(UnitRole.Tank));
        }

        private static void SavePortraitPose()
        {
            var contents = PrefabUtility.LoadPrefabContents(PaintSquadRoleVisualBuilder.PrefabPath(UnitRole.Tank));
            try
            {
                var parts = contents.GetComponentsInChildren<Transform>(true)
                    .Where(part => part.name.StartsWith(ShieldPrefix, StringComparison.Ordinal)).ToArray();
                var rim = parts.FirstOrDefault(part => part.name == ShieldPrefix + " Rim");
                if (rim == null) throw new InvalidOperationException("The Tank prefab has no riot shield.");
                var pivot = rim.localPosition;
                var turn = Quaternion.Euler(0f, PortraitShieldTurn, 0f);
                foreach (var part in parts)
                {
                    part.localPosition = pivot + PortraitShieldShift + turn * (part.localPosition - pivot);
                    part.localRotation = turn * part.localRotation;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, PortraitPosePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }
}
