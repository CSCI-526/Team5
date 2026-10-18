using UnityEngine;

namespace ReverseTD.Defense
{
    public class Tower : MonoBehaviour
    {
        private const float HealthBarGap = 0.22f;
        private const float HealthBarExtraWidth = 0.2f;

        [SerializeField, Tooltip("Stats and look. Set by BoardView when the tower spawns.")]
        private TowerDefinition definition;

        private BoardView board;

        public static event System.Action<Tower> Destroyed;

        public TowerDefinition Definition => definition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Destroyed = null;
        }

        public void Initialize(TowerDefinition towerDefinition, BoardView owner, TowerAttackDirection attackDirection, float attackRange)
        {
            definition = towerDefinition;
            board = owner;

            var visual = CreateChild("Body").AddComponent<TowerVisual>();
            visual.Initialize(definition);
            Vector2 direction = TowerAttackGeometry.Direction(attackDirection);
            visual.Face(transform.position + (Vector3)direction);
            CreateChild("Range").AddComponent<RangeIndicator>().Initialize(definition, direction, attackRange);
            var marker = CreateChild("Target Marker").AddComponent<TargetMarker>();
            marker.Initialize();

            GameObject barObject = CreateChild("HP Bar");
            barObject.transform.localPosition = new Vector3(0f, definition.Size * 0.5f + HealthBarGap, 0f);
            var healthBar = barObject.AddComponent<HealthBar>();
            healthBar.Initialize(definition.Size + HealthBarExtraWidth);
            gameObject.AddComponent<TowerHealth>().Initialize(this, definition, healthBar, visual);

            var targeting = gameObject.AddComponent<TowerTargeting>();
            targeting.Initialize(definition, direction, attackRange);
            gameObject.AddComponent<TowerWeapon>().Initialize(definition, targeting, visual, marker, direction, attackRange);
        }

        internal void Die()
        {
            Destroy(gameObject);
            try
            {
                Destroyed?.Invoke(this);
            }
            finally
            {
                if (board != null)
                {
                    board.HandleTowerDestroyed(this);
                }
                CombatEffects.SpawnWreck(transform.parent, transform.position, definition.Color, definition.Size / TowerVisual.DesignSize);
            }
        }

        private GameObject CreateChild(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child;
        }
    }
}
