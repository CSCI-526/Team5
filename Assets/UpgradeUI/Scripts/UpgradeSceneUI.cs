using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UpgradeSceneUI : MonoBehaviour
{
    [Header("Data Asset")]
    [SerializeField] private OffenseData offenseData;

    [Header("Scene Loading")]
    [SerializeField] private string gameplaySceneName = "DefenseSandbox";
    [SerializeField] private string level2SceneName = "DefenseLevel2";
    [SerializeField] private Button startGameButton;

    [Header("Gold Display")]
    [SerializeField] private TMP_Text goldText;

    [Header("Tree Containers & Prefabs")]
    [SerializeField] private RectTransform treeContainer;
    [SerializeField] private UpgradeTreeNodeUI nodePrefab;
    [SerializeField] private UILineConnector linePrefab;

    [Header("Tree Configuration")]
    [SerializeField] private Vector2 rootStartPosition = new Vector2(-300f, 0f);
    [SerializeField] private float horizontalBranchDistance = 220f;
    [SerializeField] private float verticalChildSpacing = 110f;
    [SerializeField] private UpgradeNodeData rootUpgrade;

    private readonly Dictionary<UpgradeNodeData, UpgradeTreeNodeUI> spawnedNodes = new();
    private readonly List<UILineConnector> spawnedLines = new();

    private void Start()
    {
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);

        BuildDefaultTreeIfEmpty();

        // Load tree and rebuild unlocked branches
        SpawnRootNode();
        RestoreTreeProgress(rootUpgrade, spawnedNodes[rootUpgrade]);

        RefreshUI();
    }

    private void SpawnRootNode()
    {
        rootUpgrade.isUnlocked = true;
        // Load saved level for root
        rootUpgrade.currentLevel = PlayerPrefs.GetInt($"UpgradeNode_{rootUpgrade.id}", 0);
        SpawnNodeUI(rootUpgrade, rootStartPosition, null);
    }

    // Recursively loads purchased levels and spawns child branches
    private void RestoreTreeProgress(UpgradeNodeData parentData, UpgradeTreeNodeUI parentUI)
    {
        if (parentData.currentLevel > 0)
        {
            RevealChildren(parentData, parentUI);

            foreach (var child in parentData.children)
            {
                if (spawnedNodes.TryGetValue(child, out var childUI))
                {
                    child.currentLevel = PlayerPrefs.GetInt($"UpgradeNode_{child.id}", 0);
                    RestoreTreeProgress(child, childUI);
                }
            }
        }
    }

    private UpgradeTreeNodeUI SpawnNodeUI(UpgradeNodeData data, Vector2 localPos, UpgradeTreeNodeUI parentUI)
    {
        UpgradeTreeNodeUI nodeUI = Instantiate(nodePrefab, treeContainer);
        nodeUI.RectTransform.anchoredPosition = localPos;
        nodeUI.Setup(data, OnNodeClicked);
        spawnedNodes[data] = nodeUI;

        if (parentUI != null)
        {
            UILineConnector line = Instantiate(linePrefab, treeContainer);
            line.transform.SetAsFirstSibling();
            line.Connect(parentUI.RectTransform, nodeUI.RectTransform);
            spawnedLines.Add(line);
        }

        return nodeUI;
    }

    private void OnNodeClicked(UpgradeTreeNodeUI nodeUI)
    {
        UpgradeNodeData data = nodeUI.Data;
        if (offenseData == null || data.IsMaxLevel) return;

        double cost = data.GetCurrentCost();
        if (offenseData.gold < cost) return;

        // 1. Deduct cost & level up
        offenseData.gold -= cost;
        data.currentLevel++;

        // 2. Save progress to disk
        PlayerPrefs.SetInt($"UpgradeNode_{data.id}", data.currentLevel);
        PlayerPrefs.Save();

        // 3. Apply stats to OffenseData
        ApplyStatUpgrade(data);

        // 4. Reveal branches if first purchase
        if (data.currentLevel == 1)
        {
            RevealChildren(data, nodeUI);
        }

        // 5. Refresh UI
        RefreshUI();
    }

    private void RevealChildren(UpgradeNodeData parentData, UpgradeTreeNodeUI parentUI)
    {
        int childCount = parentData.children.Count;
        if (childCount == 0) return;

        float startY = ((childCount - 1) * verticalChildSpacing) / 2f;
        Vector2 parentPos = parentUI.RectTransform.anchoredPosition;

        for (int i = 0; i < childCount; i++)
        {
            UpgradeNodeData childData = parentData.children[i];
            if (spawnedNodes.ContainsKey(childData)) continue;

            childData.isUnlocked = true;
            childData.currentLevel = PlayerPrefs.GetInt($"UpgradeNode_{childData.id}", 0);

            Vector2 targetOffset = childData.manualOffset != Vector2.zero
                ? childData.manualOffset
                : new Vector2(horizontalBranchDistance, startY - (i * verticalChildSpacing));

            SpawnNodeUI(childData, parentPos + targetOffset, parentUI);
        }
    }

    private void ApplyStatUpgrade(UpgradeNodeData data)
    {
        switch (data.upgradeType)
        {
            case UpgradeType.Attack:
                offenseData.AddDamage(data.amount);
                break;
            case UpgradeType.Health:
                offenseData.AddHealth(data.amount);
                break;
            case UpgradeType.Units:
                offenseData.AddSpawnCount((int)data.amount);
                break;
        }
    }

    private void RefreshUI()
    {
        if (offenseData == null) return;

        if (goldText != null)
            goldText.text = $"$ {offenseData.gold:N0}";

        foreach (var kvp in spawnedNodes)
        {
            kvp.Value.Refresh(offenseData.gold);
        }
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
        else
        {
            Debug.LogError("Invalid level: " + offenseData.currentLevel);
        }
    }


    private void BuildDefaultTreeIfEmpty()
    {
        if (rootUpgrade != null && !string.IsNullOrEmpty(rootUpgrade.id)) return;

        rootUpgrade = new UpgradeNodeData
        {
            id = "atk_1",
            title = "+1 ATK",
            baseCost = 2,
            maxLevel = 5,
            costMultiplier = 2,
            upgradeType = UpgradeType.Attack,
            amount = 1f,
            children = new List<UpgradeNodeData>
            {
                new UpgradeNodeData
                {
                    id = "atk_2",
                    title = "+2 ATK",
                    baseCost = 25,
                    maxLevel = 3,
                    costMultiplier = 2.0,
                    upgradeType = UpgradeType.Attack,
                    amount = 2f
                },
                new UpgradeNodeData
                {
                    id = "hp_1",
                    title = "+4 HP",
                    baseCost = 10,
                    maxLevel = 5,
                    costMultiplier = 1.4,
                    upgradeType = UpgradeType.Health,
                    amount = 4f
                },
                new UpgradeNodeData
                {
                    id = "units_1",
                    title = "+1 Unit",
                    baseCost = 40,
                    maxLevel = 3,
                    costMultiplier = 2.5,
                    upgradeType = UpgradeType.Units,
                    amount = 1f
                }
            }
        };
    }

    private void OnDestroy()
    {
        if (startGameButton != null)
            startGameButton.onClick.RemoveAllListeners();
    }
}