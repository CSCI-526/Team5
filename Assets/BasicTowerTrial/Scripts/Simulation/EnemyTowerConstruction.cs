using System;
using UnityEngine;

namespace Team5.BasicTowerTrial.Simulation
{
    public sealed partial class BattleSimulation
    {
        // Opt-in construction for the tavern prototype. Existing battles start fully built.
        public void SetTowerConstructionPending(int id)
        {
            if (stepping || ElapsedTime != 0) throw new InvalidOperationException("Reserve sites before the first tick.");
            var tower = towers.Find(t => t.Id == id) ?? throw new ArgumentException("Unknown tower.");
            tower.UnderConstruction = true;
            tower.TargetId = null;
        }

        public bool CompleteOrUpgradeTower(int id, int level)
        {
            if (stepping || IsPaused || Outcome != BattleOutcome.Running || level < 1 || level > 3) return false;
            var tower = towers.Find(t => t.Id == id);
            if (tower == null || tower.Hp <= 0 || tower.Definition.IsWall) return false;
            var old = tower.Definition;
            var hp = 18 + 10 * (level - 1);
            var fraction = tower.Hp / old.MaxHp;
            tower.Definition = new TowerDefinition(old.Id, old.Position, old.Range,
                .55f + .3f * (level - 1), 1.2f - .15f * (level - 1), old.TargetRule,
                old.FootprintSize, hp, old.Area, old.AttackWidth, old.IsAoe, old.GoldBounty);
            // Upgrading preserves damage already taken as a percentage; no free full heal.
            tower.Hp = hp * Mathf.Clamp01(fraction);
            tower.UnderConstruction = false;
            return true;
        }
    }
}
