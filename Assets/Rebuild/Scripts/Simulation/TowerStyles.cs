using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public static class TowerStyles
    {
        public static Color ColorFor(TowerDefinition tower) => tower.Kind switch
        {
            TowerKind.Laser => new Color(1f, .12f, .2f),
            TowerKind.Frost => new Color(.18f, .78f, 1f),
            TowerKind.Blast => new Color(1f, .43f, .08f),
            _ => tower.Area == AttackArea.Square ? new Color(.68f, .3f, 1f) : new Color(.25f, .55f, 1f)
        };
        public static string Name(TowerDefinition tower) => tower.Kind switch
        {
            TowerKind.Laser => "LASER",
            TowerKind.Frost => "FROST",
            TowerKind.Blast => "BLAST",
            _ => tower.Area == AttackArea.Square ? "SENTRY" : "NORMAL"
        };
        public static string Description(TowerDefinition tower) => tower.Kind switch
        {
            TowerKind.Laser => "0.8s warning, then pierces every unit in its lane.",
            TowerKind.Frost => "Single target in all directions. Slows movement 25% for 2s; refreshes, never stacks.",
            TowerKind.Blast => "Targets in all directions. Deals normal sentry damage in a 2x2 square at impact.",
            _ => tower.Area == AttackArea.Square ? "All-direction coverage; hits one unit at a time." : "Directional coverage; hits one unit at a time."
        };
    }
}
