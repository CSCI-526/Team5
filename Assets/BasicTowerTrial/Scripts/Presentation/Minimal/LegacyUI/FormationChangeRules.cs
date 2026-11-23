using Team5.BasicTowerTrial.Session;
using UnityEngine;

namespace Team5.BasicTowerTrial.Presentation.LegacyUI
{
    public static class FormationChangeRules
    {
        public const int Unlimited = -1;

        private static readonly int[] changesPerTryByLevel = { Unlimited, Unlimited, 3, 3, 3, 3, 3, 3 };

        public static int AllowedChanges(int level) =>
            level <= 1 ? Unlimited : changesPerTryByLevel[Mathf.Min(level, changesPerTryByLevel.Length - 1)];

        public static string EditorNote(int remaining) =>
            "Battle paused  ·  " + remaining + (remaining == 1 ? " change left" : " changes left");

        public static string NextLevelFact()
        {
            var allowed = BattleSceneFlow.IsTestRun ? Unlimited : AllowedChanges(BattleSceneFlow.NextBattleLevelNumber);
            if (allowed == Unlimited) return string.Empty;
            return "  |  " + allowed + (allowed == 1 ? " formation change" : " formation changes");
        }
    }
}
