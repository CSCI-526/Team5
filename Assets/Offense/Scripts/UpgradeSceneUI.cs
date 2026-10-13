using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UpgradeSceneUI : MonoBehaviour
{
    [Header("Data Asset")]
    [SerializeField] private OffenseData offenseData;

    [Header("Scene Loading")]
    [SerializeField] private string gameplaySceneName = "DefenseSandbox";
    [SerializeField] private string level2SceneName = "DefenseLevel2";

    [Header("Gold Display")]
    [SerializeField] private TMP_Text goldText;

    [Header("Buttons")]
    [SerializeField] private Button hpUpgradeButton;
    [SerializeField] private TMP_Text hpButtonText;

    [SerializeField] private Button atkUpgradeButton;
    [SerializeField] private TMP_Text atkButtonText;

    [SerializeField] private Button unitUpgradeButton;
    [SerializeField] private TMP_Text unitButtonText;

    [SerializeField] private Button startGameButton;

    private void Start()
    {
        // Hook up button click listeners
        hpUpgradeButton.onClick.AddListener(OnUpgradeHPClicked);
        atkUpgradeButton.onClick.AddListener(OnUpgradeATKClicked);
        unitUpgradeButton.onClick.AddListener(OnUpgradeUnitsClicked);
        startGameButton.onClick.AddListener(OnStartGameClicked);

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (offenseData == null) return;

        // Gold display
        goldText.text = $"$ {offenseData.gold:N0}";

        // Costs
        double hpCost = offenseData.GetHealthCost();
        double atkCost = offenseData.GetDamageCost();
        double unitCost = offenseData.GetSpawnCountCost();

        // Button labels
        if (hpButtonText != null)
            hpButtonText.text = $"HP (Lv {offenseData.healthLevel})\nCost: {hpCost:N0}";

        if (atkButtonText != null)
            atkButtonText.text = $"ATK (Lv {offenseData.damageLevel})\nCost: {atkCost:N0}";

        if (unitButtonText != null)
            unitButtonText.text = $"Units (Lv {offenseData.spawnCountLevel})\nCost: {unitCost:N0}";

        // Button interactability based on current gold
        hpUpgradeButton.interactable = offenseData.gold >= hpCost;
        atkUpgradeButton.interactable = offenseData.gold >= atkCost;
        unitUpgradeButton.interactable = offenseData.gold >= unitCost;
    }

    private void OnUpgradeHPClicked()
    {
        if (offenseData.BuyHealthUpgrade())
            RefreshUI();
    }

    private void OnUpgradeATKClicked()
    {
        if (offenseData.BuyDamageUpgrade())
            RefreshUI();
    }

    private void OnUpgradeUnitsClicked()
    {
        if (offenseData.BuySpawnCountUpgrade())
            RefreshUI();
    }

    private void OnStartGameClicked()
    {
        if (offenseData == null)
        {
            Debug.LogError("OffenseData is missing.");
            return;
        }

        if (offenseData.currentLevel == 1)
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
        else if (offenseData.currentLevel == 2)
        {
            SceneManager.LoadScene(level2SceneName);
        }
    }

    private void OnDestroy()
    {
        hpUpgradeButton.onClick.RemoveAllListeners();
        atkUpgradeButton.onClick.RemoveAllListeners();
        unitUpgradeButton.onClick.RemoveAllListeners();
        startGameButton.onClick.RemoveAllListeners();
    }
}