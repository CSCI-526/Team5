using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    // All durations are battle-time seconds (paused battles do not spend cooldown).
    public static class LegendarySkillRules
    {
        public const float CaptainCooldown = 18f;
        public const int CaptainBattlefieldCapacity = 9;
        public const float CaptainStrikeRange = 8f;
        public const float CaptainStrikeDamage = 10f;
        public const float FreezeRadius = 2.7f;
        public const float FreezeDuration = 4.5f;
        public const float FreezeCooldown = 30f;
        public const float RallyRadius = 3.0f;
        public const float RallyDuration = 7f;
        public const float RallyCooldown = 26f;
        public const float BarrierRadius = 3.0f;
        public const float BarrierDuration = 8f;
        public const float BarrierCooldown = 34f;
        public const int BarrierCharges = 2;

        public static bool HasActiveSkill(UnitRole role) =>
            role == UnitRole.Captain || role == UnitRole.Frostweaver || role == UnitRole.Warcaller || role == UnitRole.Aegis;

        public static string ShortName(UnitRole role) => role switch
        {
            UnitRole.Captain => "Call Backup",
            UnitRole.Frostweaver => "Time Lock",
            UnitRole.Warcaller => "Battle Cry",
            UnitRole.Aegis => "Guardian Ward",
            _ => ""
        };

        public static float Cooldown(UnitRole role) => role switch
        {
            UnitRole.Captain => CaptainCooldown,
            UnitRole.Frostweaver => FreezeCooldown,
            UnitRole.Warcaller => RallyCooldown,
            UnitRole.Aegis => BarrierCooldown,
            _ => 0f
        };

        // Modest level scaling keeps unique support units relevant without direct damage.
        public static float FreezeTime(int level) => FreezeDuration + Mathf.Min(2f, Mathf.Max(0, level - 1) * .2f);
        public static float RallyMultiplier(int level) => 1.35f + Mathf.Min(.25f, Mathf.Max(0, level - 1) * .025f);
        public static string Description(UnitRole role, int level) => role switch
        {
            UnitRole.Captain => $"Call in a random Attack/Tank/Healer/Sniper/Vanguard/Demolisher at your current Summon Tier (up to {CaptainBattlefieldCapacity} active). If full, strike a nearby tower for {CaptainStrikeDamage:0} damage. Cooldown {CaptainCooldown:0}s.",
            UnitRole.Frostweaver => $"Click a tower area to freeze all towers within {FreezeRadius:0.0} units for {FreezeTime(level):0.0}s. Cooldown {FreezeCooldown:0}s.",
            UnitRole.Warcaller => $"Empower nearby non-legendary attackers within {RallyRadius:0.0} units: {RallyMultiplier(level):0.00}x tower damage for {RallyDuration:0}s. Cooldown {RallyCooldown:0}s.",
            UnitRole.Aegis => $"All nearby allies within {BarrierRadius:0.0} units block their next {BarrierCharges} tower hits over {BarrierDuration:0}s. Cooldown {BarrierCooldown:0}s.",
            _ => ""
        };
    }
}
