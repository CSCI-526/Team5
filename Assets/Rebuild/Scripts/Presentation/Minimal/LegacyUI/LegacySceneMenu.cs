using Team5.Rebuild.Session;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Keep the original scenes' persistent UnityEvent method names.
    public sealed class LegacySceneMenu : MonoBehaviour
    {
        // Paint Squad look for the authored title and end screens (appearance only).
        private void Start() => PaintSquadSceneSkin.StyleMenuScene(gameObject.scene);

        public void StartGame()
        {
            if (gameObject.scene.path == BattleSceneFlow.TestStartScene) BattleSceneFlow.BeginTestRun();
            // A real run first asks for the squad look; the choice is stored before the run begins.
            else CharacterSelectScreen.Show(_ => BattleSceneFlow.BeginRun());
        }
        public void GoToStart() => BattleSceneFlow.ReturnToStart();
    }
}
