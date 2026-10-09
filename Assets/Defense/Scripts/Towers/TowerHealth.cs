using ReverseTD.Shared;
using UnityEngine;

namespace ReverseTD.Defense
{
    public class TowerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Tooltip("Current hit points. Starts at the definition's max HP.")]
        private float hp;

        private Tower tower;
        private TowerDefinition definition;
        private HealthBar healthBar;
        private TowerVisual visual;
        private bool isAlive = true;

        public Team Team => Team.Defender;
        public bool IsAlive => isAlive;
        public Vector3 Position => transform.position;

        public void Initialize(Tower owner, TowerDefinition towerDefinition, HealthBar bar, TowerVisual towerVisual)
        {
            tower = owner;
            definition = towerDefinition;
            healthBar = bar;
            visual = towerVisual;
            hp = definition.MaxHp;
        }

        private void OnEnable()
        {
            CombatRegistry.Register(this);
        }

        private void OnDisable()
        {
            CombatRegistry.Unregister(this);
        }

        public void TakeDamage(float amount)
        {
            if (!isAlive)
            {
                return;
            }

            hp = Mathf.Max(0f, hp - amount);
            healthBar.SetFraction(hp / definition.MaxHp);
            if (hp <= 0f)
            {
                isAlive = false;
                tower.Die();
                return;
            }

            visual.FlashHit();
        }
    }
}
