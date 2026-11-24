using System;
using System.Linq;
using UnityEngine;

namespace Team5.BasicTowerTrial.Simulation
{
    public sealed partial class BattleSimulation
    {
        private void TickMusterZombies(float deltaTime)
        {
            foreach (var zombie in units)
            {
                if (!zombie.IsAlive || !MusterZombieRules.IsZombie(zombie.Role)) continue;
                zombie.AttackCooldown = Mathf.Max(0f, zombie.AttackCooldown - deltaTime);
                if (zombie.Role != UnitRole.ShamanZombie) continue;
                zombie.ShamanCooldown -= deltaTime;
                if (zombie.ShamanTargets != null)
                {
                    zombie.ShamanWindup -= deltaTime;
                    if (zombie.ShamanWindup > 0f) continue;
                    foreach (var id in zombie.ShamanTargets)
                    {
                        var member = units.Find(candidate => candidate.Id == id);
                        if (member == null || !CanShamanHeal(zombie, member)) continue;
                        var amount = Math.Min(member.MaxHp - member.Hp, 50f * zombie.Definition.Level);
                        member.Hp += amount;
                        UnitHealed?.Invoke(new HealEvent(zombie.Id, member, amount));
                    }
                    zombie.ShamanTargets = null;
                }
                if (zombie.ShamanCooldown > 0f) continue;
                var targetLimit = zombie.Definition.Level > 1 ? MusterZombieRules.UpgradedHealTargetLimit : MusterZombieRules.HealTargetLimit;
                var targets = units.Where(member => CanShamanHeal(zombie, member))
                    .OrderBy(member => HorizontalDistanceSquared(zombie.Position, member.Position))
                    .ThenBy(member => member.Id).Take(targetLimit)
                    .Select(member => member.Id).ToArray();
                if (targets.Length == 0) { zombie.ShamanCooldown = .2f; continue; }
                zombie.ShamanCooldown = MusterZombieRules.HealCooldown;
                zombie.ShamanWindup = MusterZombieRules.HealReleaseTime;
                zombie.ShamanTargets = targets;
                zombie.AttackCooldown = Mathf.Max(zombie.AttackCooldown, MusterZombieRules.HealAnimationTime);
                HealerPulsed?.Invoke(zombie.Id);
            }
        }

        private static bool CanShamanHeal(UnitState healer, UnitState member) =>
            member.IsAlive && member.Hp < member.MaxHp &&
            HorizontalDistanceSquared(healer.Position, member.Position) <= MusterZombieRules.HealRadius * MusterZombieRules.HealRadius;

        private float ZombieMelee(UnitState zombie, Vector3 target)
        {
            zombie.AttackCooldown = zombie.Definition.AttackInterval;
            ZombieMeleeStarted?.Invoke(zombie.Id, target);
            return Mathf.Min(zombie.Definition.MaximumDamage,
                Mathf.Floor(RollDefenseDamage(zombie.Definition.Damage, zombie.Definition.MaximumDamage + 1f)));
        }
    }
}
