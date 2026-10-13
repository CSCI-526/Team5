using System;
using System.Collections.Generic;
using UnityEngine;

public enum UpgradeType
{
    Attack,
    Health,
    Units
}

[Serializable]
public class UpgradeNodeData
{
    public string id;
    public string title = "+1 ATK";
    public double baseCost = 1;
    public UpgradeType upgradeType = UpgradeType.Attack;
    public float amount = 1f;

    [Header("Levels & Cost Scaling")]
    [Tooltip("Maximum times this node can be bought (e.g. 3, 5)")]
    public int maxLevel = 3;

    [Tooltip("Multiplier for cost on each level")]
    public double costMultiplier = 1.5;

    [HideInInspector] public int currentLevel = 0;
    [HideInInspector] public bool isUnlocked = false;

    public bool IsMaxLevel => maxLevel > 0 && currentLevel >= maxLevel;

    // Calculates the cost based on the current level
    public double GetCurrentCost()
    {
        return Math.Round(baseCost * Math.Pow(costMultiplier, currentLevel));
    }

    [Header("Layout (Optional - leave (0,0) for auto fan-out)")]
    public Vector2 manualOffset = Vector2.zero;

    [Header("Branches")]
    public List<UpgradeNodeData> children = new List<UpgradeNodeData>();
}