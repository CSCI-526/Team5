using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

[ExecuteAlways]
public class Level2IntroUI : MonoBehaviour
{
    [SerializeField] private OffenseData offenseData;
    [SerializeField, Range(1, 2)] private int completedLevel = 1;
    [SerializeField] private TMP_Text rewardHeading;
    [SerializeField] private TMP_Text rewardAmount;
    [SerializeField] private TMP_Text rewardDescription;

    private void OnEnable() => RefreshReward();
    private void Update() => RefreshReward();

    private void RefreshReward()
    {
        if (offenseData == null) return;
        // Preview the current configuration in the Editor; display the actual award in play.
        int level = Mathf.Clamp(completedLevel, 1, 2);
        bool isLevelTwo = level == 2;
        bool awarded = Application.isPlaying && (isLevelTwo ? offenseData.HasLevelTwoReward : offenseData.HasLevelOneReward);
        int amount = awarded
            ? (isLevelTwo ? offenseData.LevelTwoGoldAwarded : offenseData.LevelOneGoldAwarded)
            : (isLevelTwo ? offenseData.ConfiguredLevelTwoGold : offenseData.ConfiguredLevelOneGold);
        string heading = $"LEVEL {level} REWARD";
        if (rewardHeading != null && rewardHeading.text != heading) rewardHeading.text = heading;
        string label = $"+{amount:N0} GOLD";
        if (!isLevelTwo) label += "\n+1 TANK UNIT";
        if (rewardAmount != null && rewardAmount.text != label) rewardAmount.text = label;
        string description = awarded ? "Added to your gold balance.\nSpend it on upgrades next!"
            : $"Complete Level {level} to earn this reward.";
        if (!isLevelTwo)
        {
            description = awarded
                ? (offenseData.HasPendingLevelOneTankReward ? "Gold added. Place your Tank\non the next Upgrade screen." : "Gold and Tank reward received.")
                : "Complete Level 1 to earn these rewards.";
        }
        if (rewardDescription != null && rewardDescription.text != description) rewardDescription.text = description;
    }

    public void ContinueToUpgrade()
    {
        SceneManager.LoadScene("UpgradeScene");
    }
}
