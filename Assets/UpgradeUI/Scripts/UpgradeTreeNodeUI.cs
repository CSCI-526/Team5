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
    [SerializeField] private Color affordableColor = new Color(0.18f, 0.65f, 0.22f);
    [SerializeField] private Color unaffordableColor = new Color(0.25f, 0.25f, 0.25f);
    [SerializeField] private Color maxedColor = new Color(0.15f, 0.35f, 0.7f);

    public UpgradeNodeData Data { get; private set; }
    public RectTransform RectTransform => (RectTransform)transform;

    private Action<UpgradeTreeNodeUI> onNodeClicked;

    public void Setup(UpgradeNodeData data, Action<UpgradeTreeNodeUI> onClick)
    {
        Data = data;
        onNodeClicked = onClick;

        // Make node card slightly larger so bigger text fits comfortably
        if (RectTransform.sizeDelta.x < 190f || RectTransform.sizeDelta.y < 90f)
        {
            RectTransform.sizeDelta = new Vector2(
                Mathf.Max(RectTransform.sizeDelta.x, 195f),
                Mathf.Max(RectTransform.sizeDelta.y, 92f)
            );
        }

        if (buttonText != null)
        {
            buttonText.richText = true;
            buttonText.enableAutoSizing = true;
            buttonText.fontSizeMin = 14f;
            buttonText.fontSizeMax = 24f;
        }

        button.onClick.AddListener(() => onNodeClicked?.Invoke(this));
    }

    public void Refresh(double currentGold)
    {
        string levelStr = Data.maxLevel > 0
            ? $"Lv {Data.currentLevel}/{Data.maxLevel}"
            : $"Lv {Data.currentLevel}";

        string specificTitle = GetSpecificDescription(Data);

        if (Data.IsMaxLevel)
        {
            if (buttonText != null)
                buttonText.text = $"<b><size=115%>{specificTitle}</size></b>\n<size=85%>{levelStr} — MAXED</size>";

            if (background != null)
                background.color = maxedColor;

            button.interactable = false;
        }
        else
        {
            double nextCost = Data.GetCurrentCost();

            if (buttonText != null)
                buttonText.text = $"<b><size=115%>{specificTitle}</size></b>\n<size=90%>{levelStr} | <color=#FFD93D>Cost: ${nextCost:N0}</color></size>";

            bool canAfford = currentGold >= nextCost;
            if (background != null)
                background.color = canAfford ? affordableColor : unaffordableColor;

            button.interactable = canAfford;
        }
    }

    private static string GetSpecificDescription(UpgradeNodeData data)
    {
        switch (data.upgradeType)
        {
            case UpgradeType.Attack:
                return $"+{data.amount:0.#} Soldier DMG";
            case UpgradeType.Health:
                return $"+{data.amount:0.#} Soldier HP";
            case UpgradeType.Units:
                return $"+{data.amount:0} Soldier / Wave";
            default:
                return data.title;
        }
    }
}