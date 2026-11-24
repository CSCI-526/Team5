using Team5.BasicTowerTrial.Session;
using TMPro;
using UnityEngine;

namespace Team5.BasicTowerTrial.Presentation.LegacyUI
{
    // Keep the original scenes' persistent UnityEvent method names.
    public sealed class LegacySceneMenu : MonoBehaviour
    {
        [SerializeField] private bool startRandomMuster;
        [SerializeField] private bool returnToRandomMusterStart;
        [SerializeField] private TMP_Text musterResult;

        private void Start()
        {
            var state = BattlegroundsUpgradeScene.State;
            if (musterResult != null && state != null && state.Finished)
                musterResult.text = state.CaptainHealth > 0 ? "Expedition Victory" : "Expedition Defeated";
        }

        public void StartGame()
        {
            if (startRandomMuster)
            {
                BattlegroundsUpgradeScene.NewGame();
                FormationReserveBattle.Open(BattlegroundsUpgradeScene.ScenePath);
            }
            else if (gameObject.scene.path == BattleSceneFlow.TestStartScene) BattleSceneFlow.BeginTestRun();
            else BattleSceneFlow.BeginRun();
        }
        public void GoToStart()
        {
            if (returnToRandomMusterStart) FormationReserveBattle.Open(FormationReserveBattle.StartScenePath);
            else BattleSceneFlow.ReturnToStart();
        }
    }
}
