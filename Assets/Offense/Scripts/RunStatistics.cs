/// <summary>Session-only totals; the completed snapshot survives scene changes.</summary>
public sealed class RunStatistics
{
    public bool IsRunning { get; private set; }
    public bool HasPreviousRun { get; private set; }
    public double DamageDealt { get; private set; }
    public double GoldEarned { get; private set; }
    public double PreviousDamage { get; private set; }
    public double PreviousGold { get; private set; }
    public bool PreviousSucceeded { get; private set; }

    public void Begin()
    {
        DamageDealt = 0;
        GoldEarned = 0;
        IsRunning = true;
    }

    public void RecordDamage(double applied)
    {
        if (IsRunning && applied > 0 && !double.IsInfinity(applied))
            DamageDealt += applied;
    }

    public void RecordGold(double awarded)
    {
        if (IsRunning && awarded > 0 && !double.IsInfinity(awarded))
            GoldEarned += awarded;
    }

    public bool Complete(bool succeeded)
    {
        if (!IsRunning) return false;
        IsRunning = false;
        PreviousDamage = DamageDealt;
        PreviousGold = GoldEarned;
        PreviousSucceeded = succeeded;
        HasPreviousRun = true;
        return true;
    }
}
