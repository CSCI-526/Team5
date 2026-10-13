using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeTreeNodeUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text buttonText;

    [Header("Colors")]
    [SerializeField] private Color affordableColor = new Color(0.2f, 0.6f, 0.2f);
    [SerializeField] private Color unaffordableColor = new Color(0.25f, 0.25f, 0.25f);
    [SerializeField] private Color maxedColor = new Color(0.15f, 0.35f, 0.7f);

    public UpgradeNodeData Data { get; private set; }
    public RectTransform RectTransform => (RectTransform)transform;

    private Action<UpgradeTreeNodeUI> onNodeClicked;

    public void Setup(UpgradeNodeData data, Action<UpgradeTreeNodeUI> onClick)
    {
        Data = data;
        onNodeClicked = onClick;

        button.onClick.AddListener(() => onNodeClicked?.Invoke(this));
    }

    public void Refresh(double currentGold)
    {
        string levelStr = Data.maxLevel > 0
            ? $"Lv {Data.currentLevel}/{Data.maxLevel}"
            : $"Lv {Data.currentLevel}";

        if (Data.IsMaxLevel)
        {
            if (buttonText != null)
                buttonText.text = $"{Data.title} ({levelStr})\n(MAX)";

            if (background != null)
                background.color = maxedColor;

            button.interactable = false;
        }
        else
        {
            double nextCost = Data.GetCurrentCost();

            if (buttonText != null)
                buttonText.text = $"{Data.title} ({levelStr})\nCost: ${nextCost:N0}";

            bool canAfford = currentGold >= nextCost;
            if (background != null)
                background.color = canAfford ? affordableColor : unaffordableColor;

            button.interactable = canAfford;
        }
    }
}