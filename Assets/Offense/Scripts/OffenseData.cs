using UnityEngine;
using ReverseTD.Offense;

[CreateAssetMenu(fileName = "OffenseData", menuName = "Scriptable Objects/OffenseData")]
public class OffenseData : ScriptableObject
{
    public const int LivesPerLevel = 3;
    public const int LevelRetryCost = 10;
    // Currency & Flow
    public double gold = 20;
    public float gameSpeed = 1f;

    public int currentLevel = 1;
    // Distinguish the inserted practice stage from the original Level 2 without renumbering it.
    [HideInInspector] public bool levelOnePracticePending;

    // Per-run failure totals live outside tutorial checkpoint JSON, so rewinds cannot erase them.
    [System.NonSerialized] private int[] levelDeathCounts = new int[3];

    public int GetLevelDeathCount(int levelNumber)
    {
        if (levelDeathCounts == null || levelNumber < 1 || levelNumber > levelDeathCounts.Length) return 0;
        return levelDeathCounts[levelNumber - 1];
    }

    public void RecordLevelDeath(int levelNumber)
    {
        if (levelNumber < 1 || levelNumber > 3) return;
        if (levelDeathCounts == null) levelDeathCounts = new int[3];
        levelDeathCounts[levelNumber - 1]++;
    }

    public bool IsLevelOutOfLives(int levelNumber)
    {
        return GetLevelDeathCount(levelNumber) >= LivesPerLevel;
    }

    public bool TryBuyLevelRetry(int levelNumber)
    {
        if (levelNumber < 1 || levelNumber > 3 || !IsLevelOutOfLives(levelNumber) || gold < LevelRetryCost) return false;
        gold -= LevelRetryCost;
        levelDeathCounts[levelNumber - 1] = 0;
        return true;
    }

    [Header("Level Completion Rewards")]
    public LevelCompletionRewards completionRewards;
    [SerializeField, HideInInspector] private bool levelOneRewardGranted;
    [SerializeField, HideInInspector] private int levelOneGoldAwarded;
    [SerializeField, HideInInspector] private bool levelOneTankPlaced;
    [SerializeField, HideInInspector] private bool levelTwoRewardGranted;
    [SerializeField, HideInInspector] private int levelTwoGoldAwarded;

    public bool HasLevelOneReward => levelOneRewardGranted;
    public bool HasPendingLevelOneTankReward => levelOneRewardGranted && !levelOneTankPlaced;
    public int LevelOneGoldAwarded => levelOneGoldAwarded;
    public int ConfiguredLevelOneGold => completionRewards != null ? completionRewards.LevelOneGold : 0;
    public bool HasLevelTwoReward => levelTwoRewardGranted;
    public int LevelTwoGoldAwarded => levelTwoGoldAwarded;
    public int ConfiguredLevelTwoGold => completionRewards != null ? completionRewards.LevelTwoGold : 0;

    public bool GrantLevelOneReward()
    {
        if (levelOneRewardGranted) return false;
        levelOneGoldAwarded = ConfiguredLevelOneGold;
        gold += levelOneGoldAwarded;
        levelOneRewardGranted = true;
        return true;
    }

    public bool GrantLevelTwoReward()
    {
        if (levelTwoRewardGranted) return false;
        levelTwoGoldAwarded = ConfiguredLevelTwoGold;
        gold += levelTwoGoldAwarded;
        levelTwoRewardGranted = true;
        return true;
    }

    public bool TryPlaceLevelOneRewardTank(int cell)
    {
        EnsureSquad();
        if (!HasPendingLevelOneTankReward || cell < 0 || cell >= SquadRules.CellCount) return false;
        // A full squad may replace a unit; otherwise the free Tank goes into an empty cell.
        if (squad[cell] != UnitType.None && !IsSquadFull) return false;
        squad[cell] = UnitType.Tank;
        levelOneTankPlaced = true;
        return true;
    }

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

    [Header("Squad")]
    public UnitType[] squad = new UnitType[SquadRules.CellCount];
    public int[] upgradeLevels = new int[SquadRules.StatCount];
    public double goldEarned = 0;
    public double goldRemainder = 0;
    public int cardsTaken = 0;
    public int goldCardsTaken = 0;
    public RouteCommand lastRouteCommand = RouteCommand.None;

    public double GoldMultiplier => 1 + SquadRules.GoldBonusPerCard * goldCardsTaken;
    public bool HasNextQuota => cardsTaken < SquadRules.QuotaCount;
    public double NextQuota => HasNextQuota ? SquadRules.Quota(cardsTaken) : 0;
    public bool IsCardReady => HasNextQuota && gold >= SquadRules.Quota(cardsTaken);
    public bool CanAffordCard => IsCardReady;
    public bool IsSquadFull => CountUnits() >= SquadRules.CellCount;

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

    public void EnsureSquad()
    {
        if (squad == null || squad.Length != SquadRules.CellCount)
        {
            squad = new UnitType[SquadRules.CellCount];
        }
        if (upgradeLevels == null || upgradeLevels.Length != SquadRules.StatCount)
        {
            upgradeLevels = new int[SquadRules.StatCount];
        }
        if (CountUnits() == 0)
        {
            squad[SquadRules.CenterCell] = UnitType.Attack;
        }
    }

    public void ResetSquad()
    {
        squad = new UnitType[SquadRules.CellCount];
        squad[SquadRules.CenterCell] = UnitType.Attack;
        upgradeLevels = new int[SquadRules.StatCount];
        goldEarned = 0;
        goldRemainder = 0;
        cardsTaken = 0;
        goldCardsTaken = 0;
        lastRouteCommand = RouteCommand.None;
    }

    public int CountUnits()
    {
        int count = 0;
        if (squad == null) return count;
        for (int i = 0; i < squad.Length; i++)
        {
            if (squad[i] != UnitType.None) count++;
        }
        return count;
    }

    public int CountUnits(UnitType type)
    {
        int count = 0;
        if (squad == null) return count;
        for (int i = 0; i < squad.Length; i++)
        {
            if (squad[i] == type) count++;
        }
        return count;
    }

    public bool IsUnlocked(UnitType type)
    {
        return type != UnitType.None && CountUnits(type) > 0;
    }

    public int GetLevel(UnitStat stat)
    {
        EnsureSquad();
        return upgradeLevels[(int)stat];
    }

    public bool IsMaxed(UnitStat stat)
    {
        return GetLevel(stat) >= SquadRules.MaxLevel(stat);
    }

    public double GetUpgradeCost(UnitStat stat)
    {
        return SquadRules.UpgradeCost(stat, GetLevel(stat));
    }

    public bool TryBuyUpgrade(UnitStat stat)
    {
        if (!IsUnlocked(SquadRules.OwnerOf(stat)) || IsMaxed(stat)) return false;

        double cost = GetUpgradeCost(stat);
        if (gold < cost) return false;

        gold -= cost;
        upgradeLevels[(int)stat]++;
        return true;
    }

    public void AddEarnedGold(float amount)
    {
        goldRemainder += amount * GoldMultiplier;
        double whole = System.Math.Floor(goldRemainder + 0.000001);
        if (whole <= 0) return;

        goldRemainder -= whole;
        gold += whole;
        goldEarned += whole;
    }

    public bool TryTakeGoldCard()
    {
        if (!CanAffordCard) return false;

        gold -= NextQuota;
        cardsTaken++;
        goldCardsTaken++;
        return true;
    }

    public bool TryTakeUnitCard(UnitType type, int cell)
    {
        EnsureSquad();
        if (type == UnitType.None || cell < 0 || cell >= SquadRules.CellCount) return false;
        if (squad[cell] != UnitType.None || !CanAffordCard) return false;

        gold -= NextQuota;
        cardsTaken++;
        squad[cell] = type;
        return true;
    }

    public void SwapCells(int first, int second)
    {
        EnsureSquad();
        if (first == second || first < 0 || second < 0 || first >= SquadRules.CellCount || second >= SquadRules.CellCount) return;

        UnitType moved = squad[first];
        squad[first] = squad[second];
        squad[second] = moved;
    }

    [ContextMenu("Test: Mockup Squad")]
    private void FillMockupSquad()
    {
        ResetSquad();
        squad = new[]
        {
            UnitType.Tank, UnitType.Attack, UnitType.Tank,
            UnitType.Attack, UnitType.Heal, UnitType.Attack,
            UnitType.None, UnitType.Attack, UnitType.None
        };
        lastRouteCommand = RouteCommand.Split;
    }

    public void ResetLevelStats()
    {
        levelOnePracticePending = false;
        levelOneRewardGranted = false;
        levelOneGoldAwarded = 0;
        levelOneTankPlaced = false;
        levelTwoRewardGranted = false;
        levelTwoGoldAwarded = 0;
        gold = 0;
        bonusHealth = 0f;
        bonusDamage = 0f;
        bonusSpawnCount = 0;
        gameSpeed = 1f;
        ResetSquad();
        PlayerPrefs.DeleteAll();
    }

    public void ResetGame()
    {
        ResetLevelStats();
        levelDeathCounts = new int[3];
        currentLevel = 1;
    }
}
