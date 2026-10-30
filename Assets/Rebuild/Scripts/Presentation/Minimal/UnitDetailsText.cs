using System;
using System.Text;
using Team5.Rebuild.Simulation;

namespace Team5.Rebuild.Presentation
{
    // Formatting only: values come from the selected member's read-only state.
    public static class UnitDetailsText
    {
        public static string Format(UnitState unit)
        {
            if (unit == null) return string.Empty;
            var stats = unit.Definition;
            var text = new StringBuilder();
            text.AppendLine($"Role: {unit.Role.ToString().ToLowerInvariant()}");
            text.AppendLine(FormattableString.Invariant($"HP: {unit.Hp:0.##} / {unit.MaxHp:0.##}"));
            text.AppendLine(FormattableString.Invariant($"Attack: {stats.Damage:0.##}"));
            text.AppendLine(FormattableString.Invariant($"Defense: {stats.Defense * 100:0.##}% damage reduction"));
            text.AppendLine(FormattableString.Invariant($"Move speed: {unit.Speed:0.##} units/s"));
            if (unit.Role == UnitRole.Attack || unit.Role == UnitRole.Captain)
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
            text.AppendLine($"Member: #{unit.MemberId}");
            text.AppendLine($"Formation: row {unit.FormationSlot.y + 1}, column {unit.FormationSlot.x + 1}");
            text.Append($"Status: {(unit.LifeState == UnitLifeState.Dead ? "Dead" : unit.LifeState == UnitLifeState.ReachedGoal ? "Reached goal" : "Alive")}");
            return text.ToString();
        }
    }
}
