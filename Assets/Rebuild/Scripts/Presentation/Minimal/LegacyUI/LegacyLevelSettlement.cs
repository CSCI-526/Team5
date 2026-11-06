using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class LegacyLevelSettlement : MonoBehaviour
    {
        [SerializeField] private TMP_Text missionHeading;
        [SerializeField] private TMP_Text rewardHeading;
        [SerializeField] private TMP_Text rewardAmount;
        [SerializeField] private TMP_Text rewardDescription;

        private void OnEnable()
        {
            var victory = BattleSceneFlow.LastOutcome != BattleOutcome.Defeat;
            var level = Mathf.Max(1, BattleSceneFlow.CompletedLevelNumber);
            missionHeading.text = victory ? "MISSION COMPLETE" : "MISSION FAILED";
            rewardHeading.text = $"LEVEL {level} {(victory ? "COMPLETE" : "RETRY")}";

            if (victory)
            {
                var totalGold = BattleSceneFlow.LastRewardGold;
                var stageGold = BattleSceneFlow.LastStageClearGold;
                var towerGold = BattleSceneFlow.LastTowerBonusGold;
                var towersKilled = BattleSceneFlow.LastTowersDestroyedCount;

                if (!BattleSceneFlow.IsRunActive)
                {
                    var settings = Resources.Load<GameEconomySettings>("RebuildEconomy");
                    stageGold = settings != null ? settings.GetStageClearGold(level) : level * 45;
                    totalGold = stageGold;
                }

                rewardAmount.text = totalGold > 0 ? $"+{totalGold:N0} GOLD" : "STAGE CLEARED";
                var bountyLine = towersKilled > 0
                    ? $"Stage Clear: +{stageGold}G  |  Towers Destroyed ({towersKilled}): +{towerGold}G\n"
                    : $"Stage Clear: +{stageGold}G  |  Destroy enemy towers with Attack units for bonus Gold!\n";
                rewardDescription.text = $"{bountyLine}Summon Tier {level} Unlocked! Higher levels & rarity odds available.";
            }
            else
            {
                rewardAmount.text = "REGROUP";
                rewardDescription.text = level == 1
                    ? "Try again with your Captain. Level 1 uses its fixed squad."
                    : "Summon stronger units or rearrange your squad on the Upgrade screen.";
            }
        }

        public void ContinueToUpgrade() => BattleSceneFlow.ContinueToUpgrade();
    }
}