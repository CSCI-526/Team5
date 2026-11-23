using System;
using System.Text;
using Team5.BasicTowerTrial.Simulation;

namespace Team5.BasicTowerTrial.Presentation
{
    public static class UnitDetailsText
    {
        public static string Format(UnitState unit)
        {
            if (unit == null) return string.Empty;
            var stats = unit.Definition;
            var text = new StringBuilder();
            text.AppendLine($"Role: {unit.Role.ToString().ToLowerInvariant()} (Lv.{stats.Level} {stats.Rarity})");
            text.AppendLine(FormattableString.Invariant($"HP: {unit.Hp:0.##} / {unit.MaxHp:0.##}"));
            if (stats.Rarity == UnitRarity.Legendary)
            {
                text.AppendLine($"Skill: {LegendarySkillRules.ShortName(unit.Role)}");
                text.AppendLine(LegendarySkillRules.Description(unit.Role, stats.Level));
                if (LegendarySkillRules.HasActiveSkill(unit.Role))
                    text.AppendLine(FormattableString.Invariant($"Skill cooldown: {unit.SkillCooldownRemaining:0.0}s"));
            }
            else text.AppendLine(FormattableString.Invariant($"Attack: {stats.Damage:0.##} ({stats.TowerDamageMultiplier:0.##}x vs Towers)"));
            text.AppendLine(FormattableString.Invariant($"Defense: {stats.Defense * 100:0.##}% damage reduction"));
            text.AppendLine(FormattableString.Invariant($"Move speed: {unit.Speed:0.##} units/s"));
            if (SquadCombatRules.CanAttackTowers(unit.Role))
            {
                text.AppendLine(FormattableString.Invariant($"Attack range: {stats.Range:0.##}"));
                text.AppendLine(FormattableString.Invariant($"Attack interval: {stats.AttackInterval:0.##} s"));
                text.AppendLine(FormattableString.Invariant($"Cooldown: {unit.AttackCooldown:0.##} s"));
            }
            if (unit.Role == UnitRole.Heal)
            {
                text.AppendLine(FormattableString.Invariant($"Healing: {stats.HealPerSecond * SquadCombatRules.HealInterval:0.##} HP / {SquadCombatRules.HealInterval:0.##} s"));
                text.AppendLine("Heal range: 8 neighboring slots");
            }
            if (unit.DamageBuffRemaining > 0)
                text.AppendLine(FormattableString.Invariant($"Battle Cry: {unit.DamageBuffMultiplier:0.00}x ({unit.DamageBuffRemaining:0.0}s)"));
            if (unit.BarrierRemaining > 0 && unit.BarrierHits > 0)
                text.AppendLine($"Guardian Ward: {unit.BarrierHits} hits left ({unit.BarrierRemaining:0.0}s)");
            text.AppendLine($"Member: #{unit.MemberId}");
            text.AppendLine($"Formation: row {unit.FormationSlot.y + 1}, column {unit.FormationSlot.x + 1}");
            text.Append($"Status: {(unit.LifeState == UnitLifeState.Dead ? "Dead" : unit.LifeState == UnitLifeState.ReachedGoal ? "Reached goal" : "Alive")}");
            return text.ToString();
        }
    }
}
