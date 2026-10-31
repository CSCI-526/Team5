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
                var gold = BattleSceneFlow.LastRewardGold;
                if (!BattleSceneFlow.IsRunActive)
                {
                    var settings = Resources.Load<GameEconomySettings>("RebuildEconomy");
                    gold = settings != null ? settings.GetStageClearGold(level) : level * 50;
                }
                rewardAmount.text = gold > 0 ? $"+{gold:N0} GOLD" : "STAGE CLEARED";
                rewardDescription.text = $"Summon Tier {level} Unlocked!\nHigher unit levels & rarity odds available in Summon.";
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