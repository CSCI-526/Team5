using Team5.Rebuild.Session;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Keep the original scenes' persistent UnityEvent method names.
    public sealed class LegacySceneMenu : MonoBehaviour
    {
        public void StartGame()
        {
            if (gameObject.scene.path == BattleSceneFlow.TestStartScene) BattleSceneFlow.BeginTestRun();
            else BattleSceneFlow.BeginRun();
        }
        public void GoToStart() => BattleSceneFlow.ReturnToStart();
    }
}
