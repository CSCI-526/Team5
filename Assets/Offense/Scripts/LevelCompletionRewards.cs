using UnityEngine;

namespace ReverseTD.Offense
{
    [CreateAssetMenu(fileName = "LevelCompletionRewards", menuName = "Reverse TD/Level Completion Rewards")]
    public sealed class LevelCompletionRewards : ScriptableObject
    {
        [SerializeField, Min(0), Tooltip("Gold awarded once when Level 1 is completed. Does not use the combat gold multiplier.")]
        private int levelOneGold = 100;

        [SerializeField, Min(0), Tooltip("Gold awarded once when the second battle (Level 1 practice copy) is completed.")]
        private int levelTwoGold = 100;

        public int LevelOneGold => Mathf.Max(0, levelOneGold);
        public int LevelTwoGold => Mathf.Max(0, levelTwoGold);
    }
}
