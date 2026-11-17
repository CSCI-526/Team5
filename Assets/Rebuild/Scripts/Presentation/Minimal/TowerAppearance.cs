using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Presentation
{
    public static class TowerAppearance
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= Apply;
            SceneManager.sceneLoaded += Apply;
        }
        private static void Apply(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var tower in root.GetComponentsInChildren<BattleTower>(true))
            {
                if (tower.IsWall || (tower.Kind == TowerKind.Standard && tower.CaptureDefinition(1).Area != AttackArea.Square)) continue;
                var tint = TowerStyles.ColorFor(tower.CaptureDefinition(1));
                var block = new MaterialPropertyBlock();
                foreach (var renderer in tower.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // Tint only energy, windows and brass details; retain stone, metal and model shading.
                    var name = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "";
                    if (!name.Contains("Energy") && !name.Contains("Window") && !name.Contains("Brass")) continue;
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", tint);
                    renderer.SetPropertyBlock(block);
                }
            }
        }
    }
}
