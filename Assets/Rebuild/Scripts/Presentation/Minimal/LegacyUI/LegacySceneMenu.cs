using Team5.Rebuild.Session;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Keep the original scenes' persistent UnityEvent method names.
    public sealed class LegacySceneMenu : MonoBehaviour
    {
        private void Start()
        {
            if (gameObject.scene.path == BattleSceneFlow.StartScene && GetComponent<BeginningPage>() == null)
                gameObject.AddComponent<BeginningPage>();
        }
        public void StartGame()
        {
            if (gameObject.scene.path == BattleSceneFlow.TestStartScene) BattleSceneFlow.BeginTestRun();
            else BattleSceneFlow.BeginRun();
        }
        public void GoToStart() => BattleSceneFlow.ReturnToStart();
    }
}
