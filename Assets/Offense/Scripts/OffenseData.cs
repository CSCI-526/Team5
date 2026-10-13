using UnityEngine;

[CreateAssetMenu(fileName = "OffenseData", menuName = "Scriptable Objects/OffenseData")]
public class OffenseData : ScriptableObject
{
    // Currency
    public double gold = 20;
    public float gameSpeed = 1f;

    public int currentLevel = 1;

    // Base costs
    public double baseCostHealth = 10;
    public double baseCostDamage = 15;
    public double baseCostSpawnCount = 50;
    public double costMultiplier = 1.15;

    // Levels and stats from upgraded levels
    public int healthLevel = 0;
    public int damageLevel = 0;
    public int spawnCountLevel = 0;

    public float MaxHealth => 5f + (healthLevel * 1f);
    public float AttackDamage => 1f + (damageLevel * 1f);
    public int TotalSpawnCount => 1 + spawnCountLevel;

    public double GetHealthCost() => System.Math.Floor(baseCostHealth * System.Math.Pow(costMultiplier, healthLevel));
    public double GetDamageCost() => System.Math.Floor(baseCostDamage * System.Math.Pow(costMultiplier, damageLevel));
    public double GetSpawnCountCost() => System.Math.Floor(baseCostSpawnCount * System.Math.Pow(1.35, spawnCountLevel));

    public bool BuyHealthUpgrade()
    {
        double cost = GetHealthCost();
        if (gold >= cost)
        {
            gold -= cost;
            healthLevel++;
            return true;
        }
        return false;
    }

    public bool BuyDamageUpgrade()
    {
        double cost = GetDamageCost();
        if (gold >= cost)
        {
            gold -= cost;
            damageLevel++;
            return true;
        }
        return false;
    }

    public bool BuySpawnCountUpgrade()
    {
        double cost = GetSpawnCountCost();
        if (gold >= cost)
        {
            gold -= cost;
            spawnCountLevel++;
            return true;
        }
        return false;
    }

    public void ResetLevelStats()
    {
        gold = 20;
        healthLevel = 0;
        damageLevel = 0;
        spawnCountLevel = 0;
        gameSpeed = 1f;
    }

    public void ResetGame()
    {
        ResetLevelStats();
        currentLevel = 1;
    }
}
