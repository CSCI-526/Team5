using System.Linq;
using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Reuses Team5 Level2Intro's authored layout and persistent Continue button.
    public sealed class LegacyLevelSettlement : MonoBehaviour
    {
        [SerializeField] private TMP_Text missionHeading;
        [SerializeField] private TMP_Text rewardHeading;
        [SerializeField] private TMP_Text rewardAmount;
        [SerializeField] private TMP_Text rewardDescription;
        private readonly List<LegacyUnitIconUI> rewardIcons = new List<LegacyUnitIconUI>();
        private float rewardFontSize;

        private void OnEnable()
        {
            foreach (var icon in rewardIcons)
                if (icon != null) { icon.gameObject.SetActive(false); Destroy(icon.gameObject); }
            rewardIcons.Clear();
            var victory = BattleSceneFlow.LastOutcome != BattleOutcome.Defeat;
            var level = Mathf.Max(1, BattleSceneFlow.CompletedLevelNumber);
            missionHeading.text = victory ? "MISSION COMPLETE" : "MISSION FAILED";
            rewardHeading.text = $"LEVEL {level} {(victory ? "COMPLETE" : "RETRY")}";
            if (victory)
            {
                var gold = BattleSceneFlow.LastRewardGold;
                var units = BattleSceneFlow.LastRewardUnits;
                // Direct Scene Play previews configured rewards without awarding anything.
                if (!BattleSceneFlow.IsRunActive)
                {
                    var settings = Resources.Load<GameEconomySettings>("RebuildEconomy");
                    var reward = settings != null ? settings.RewardForLevel(level) : null;
                    gold = reward != null ? reward.gold : 0;
                    units = reward != null ? reward.CaptureUnitRoles() : System.Array.Empty<UnitRole>();
                }
                rewardAmount.text = gold > 0 ? $"+{gold:N0} GOLD" : "REWARDS";
                rewardDescription.text = string.Join("\n", units.GroupBy(role => role).Select(group =>
                    $"+{group.Count()} {LegacyUnitLooks.Name(group.Key)} {(group.Count() == 1 ? "UNIT" : "UNITS")}"));
                if (gold == 0 && units.Count == 0) rewardDescription.text = "No rewards for this level.";
                if (units.Count > 0)
                {
                    rewardFontSize = rewardAmount.fontSizeMax;
                    rewardAmount.enableAutoSizing = rewardDescription.enableAutoSizing = false;
                    rewardAmount.fontSize = rewardDescription.fontSize = rewardFontSize;
                    rewardDescription.font = rewardAmount.font;
                    rewardDescription.fontStyle = rewardAmount.fontStyle;
                    rewardDescription.textWrappingMode = TextWrappingModes.NoWrap;
                    foreach (var group in units.GroupBy(role => role))
                    {
                        var size = rewardFontSize * 0.8f;
                        var icon = LegacyUnitIconUI.Create(rewardDescription.transform, Vector2.zero,
                            size / LegacyUnitLooks.For(group.Key).OuterSize);
                        icon.SetType(group.Key);
                        rewardIcons.Add(icon);
                    }
                }
            }
            else
            {
                rewardAmount.text = "REGROUP";
                rewardDescription.text = level == 1 ? "Try again with your Captain. Level 1 uses its fixed squad."
                    : "Rearrange your squad on the Upgrade screen, then retry this level.";
            }
        }

        private void LateUpdate()
        {
            if (rewardIcons.Count == 0) return;
            // Use one font size for both rewards, fitting the text and trailing icon together.
            rewardDescription.fontSize = rewardFontSize;
            rewardDescription.margin = Vector4.zero;
            var width = rewardDescription.GetPreferredValues(rewardDescription.text).x;
            var iconSize = rewardFontSize * 0.8f;
            var gap = rewardFontSize * 0.2f;
            var available = rewardDescription.rectTransform.rect.width;
            var scale = Mathf.Min(1, available / Mathf.Max(1, width + iconSize + gap));
            rewardAmount.fontSize = rewardDescription.fontSize = rewardFontSize * scale;
            iconSize *= scale; gap *= scale;
            rewardDescription.margin = new Vector4(0, 0, iconSize + gap, 0);
            rewardDescription.ForceMeshUpdate();
            var info = rewardDescription.textInfo;
            for (var i = 0; i < rewardIcons.Count && i < info.lineCount; i++)
            {
                var line = info.lineInfo[i];
                var end = info.characterInfo[line.lastVisibleCharacterIndex].topRight.x;
                rewardIcons[i].Rect.anchoredPosition = new Vector2(end + gap + iconSize / 2,
                    (line.ascender + line.descender) / 2);
                rewardIcons[i].Rect.localScale = Vector3.one * scale;
            }
        }

        public void ContinueToUpgrade() => BattleSceneFlow.ContinueToUpgrade();
    }
}
