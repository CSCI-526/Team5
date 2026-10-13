using UnityEngine;

[CreateAssetMenu(fileName = "OffenseData", menuName = "Scriptable Objects/OffenseData")]
public class OffenseData : ScriptableObject
{
    // Currency & Flow
    public double gold = 20;
    public float gameSpeed = 1f;

    public int currentLevel = 1;

    // Base costs
    public double baseCostHealth = 10;
    public double baseCostDamage = 15;
    public double baseCostSpawnCount = 50;
    public double costMultiplier = 1.15;
    [Header("Base Stats")]
    public float baseHealth = 5f;
    public float baseDamage = 1f;
    public int baseSpawnCount = 1;

    [Header("Bonus Amounts From Upgrades")]
    public float bonusHealth = 0f;
    public float bonusDamage = 0f;
    public int bonusSpawnCount = 0;

    // The game reads these properties - they now add your exact bonus amounts directly
    public float MaxHealth => baseHealth + bonusHealth;
    public float AttackDamage => baseDamage + bonusDamage;
    public int TotalSpawnCount => baseSpawnCount + bonusSpawnCount;

    public void AddHealth(float amount)
    {
        bonusHealth += amount;
    }

    public void AddDamage(float amount)
    {
        bonusDamage += amount;
    }

    public void AddSpawnCount(int amount)
    {
        bonusSpawnCount += amount;
    }

    public void ResetLevelStats()
    {
        gold = 0;
        bonusHealth = 0f;
        bonusDamage = 0f;
        bonusSpawnCount = 0;
        gameSpeed = 1f;
        PlayerPrefs.DeleteAll();
    }

    public void ResetGame()
    {
        ResetLevelStats();
        currentLevel = 1;
    }
}
