using System;
using System.Linq;
using Team5.BasicTowerTrial.Simulation;

namespace Team5.BasicTowerTrial.Presentation
{
    // Deterministic economy driven by simulation time, including pause and playback speed.
    public sealed class SiegeEnemyBuilder
    {
        private readonly BattleSimulation battle;
        private readonly int[] ids;
        public readonly int[] Levels;
        public float Gold { get; private set; }
        public string LastAction { get; private set; } = "Enemy builders are gathering gold...";
        public int BuiltCount => Levels.Count(l => l > 0);
        public SiegeEnemyBuilder(BattleSimulation simulation, int[] towerIds)
        {
            battle = simulation; ids = towerIds; Levels = new int[ids.Length];
            foreach (var id in ids) battle.SetTowerConstructionPending(id);
        }
        public void Tick(float simulationSeconds)
        {
            if (simulationSeconds <= 0 || battle.IsPaused || battle.Outcome != BattleOutcome.Running) return;
            Gold += simulationSeconds * 2;
            for (var i = 0; i < ids.Length; i++)
            {
                if (Levels[i] != 0) continue;
                if (Gold < 8) return;
                if (battle.CompleteOrUpgradeTower(ids[i], 1))
                { Gold -= 8; Levels[i] = 1; LastAction = $"Enemy built tower {i + 1} — level 1"; }
                return;
            }
            // Upgrade the weakest living tower first; destroyed sites stay destroyed this assault.
            var candidates = Enumerable.Range(0, ids.Length)
                .Where(i => Levels[i] < 3 && battle.Towers.Any(t => t.Id == ids[i] && t.IsAlive))
                .OrderBy(i => Levels[i]).ThenBy(i => i).ToArray();
            if (candidates.Length == 0) return;
            var next = candidates[0]; var cost = 6 + Levels[next] * 4;
            if (Gold < cost || !battle.CompleteOrUpgradeTower(ids[next], Levels[next] + 1)) return;
            Gold -= cost; Levels[next]++; LastAction = $"Enemy upgraded tower {next + 1} — level {Levels[next]}";
        }
    }
}
