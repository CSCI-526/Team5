using UnityEngine;

namespace RandomMuster
{
    [RequireComponent(typeof(ZombieVisual))]
    public sealed class ZombieUnit : MonoBehaviour
    {
        [field: SerializeField, Min(1)] public int MaxHealth { get; private set; } = 20;
        [field: SerializeField, Min(.01f)] public float MoveSpeed { get; private set; } = 1.2f;
        [field: SerializeField, Min(1)] public int MinimumDamage { get; private set; } = 1;
        [field: SerializeField, Min(1)] public int MaximumDamage { get; private set; } = 4;
        [field: SerializeField, Min(.01f)] public float AttackInterval { get; private set; } = 1f;
        [field: SerializeField, Range(0, 1)] public float Armor { get; private set; }
        [field: SerializeField, Range(0, 1)] public float MagicResistance { get; private set; }
        [field: SerializeField, Min(0)] public int KillGold { get; private set; } = 3;
        [field: SerializeField, Min(0)] public int GoalLifeCost { get; private set; } = 1;

        public float Health { get; private set; }
        public bool IsAlive => Health > 0f;
        public ZombieVisual Visual { get; private set; }
        private float nextAttackTime;

        private void Awake()
        {
            Visual = GetComponent<ZombieVisual>();
            ResetHealth();
        }

        public void ResetHealth()
        {
            Health = MaxHealth;
            nextAttackTime = 0f;
        }

        public void TakeDamage(float damage, bool magical = false)
        {
            var resistance = magical ? MagicResistance : Armor;
            Health = Mathf.Max(0, Health - Mathf.Max(0, damage) * (1f - resistance));
        }

        public void Heal(float amount)
        {
            if (IsAlive && amount > 0f)
                Health = Mathf.Min(MaxHealth, Health + amount);
        }

        internal void DelayAttack(float duration) => nextAttackTime = Mathf.Max(nextAttackTime, Time.time + duration);

        public int Attack()
        {
            if (!IsAlive || Time.time < nextAttackTime) return 0;
            nextAttackTime = Time.time + AttackInterval;
            Visual.PlayAttack();
            return Random.Range(MinimumDamage, Mathf.Max(MinimumDamage, MaximumDamage) + 1);
        }
    }
}
