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
            // A real run first asks for the squad look; the choice is stored before the run begins.
            else CharacterSelectScreen.Show(_ => BattleSceneFlow.BeginRun());
        }
        public void GoToStart() => BattleSceneFlow.ReturnToStart();
    }
}
