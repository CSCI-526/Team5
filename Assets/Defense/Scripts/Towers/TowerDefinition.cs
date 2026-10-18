using UnityEngine;

namespace ReverseTD.Defense
{
    [CreateAssetMenu(menuName = "ReverseTD/Tower Definition", fileName = "TowerDefinition")]
    public class TowerDefinition : ScriptableObject
    {
        [SerializeField, Min(1f), Tooltip("Hit points.")]
        private float maxHp = 100f;

        [SerializeField, Min(0f), Tooltip("Length of the forward attack lane, in world units.")]
        private float range = 3f;

        [SerializeField, Min(0.01f), Tooltip("Width of the attack lane. Keep below the squad spacing (0.36) to cover a single aligned row or column.")]
        private float attackWidth = 0.3f;

        [SerializeField, Min(0f), Tooltip("Damage per projectile hit.")]
        private float damage = 10f;

        [SerializeField, Min(0.01f), Tooltip("Shots per second.")]
        private float fireRate = 1f;

        [SerializeField, Min(0.1f), Tooltip("Projectile speed, in world units per second.")]
        private float projectileSpeed = 8f;

        [SerializeField, Tooltip("Which soldier in range to shoot.")]
        private TargetingMode targetingMode = TargetingMode.First;

        [SerializeField, Min(0.1f), Tooltip("Side length of the tower square, in world units.")]
        private float size = 0.8f;

        [SerializeField, Tooltip("Tower color.")]
        private Color color = new Color(0.30f, 0.55f, 0.96f);

        public float MaxHp => maxHp;
        public float Range => range;
        public float AttackWidth => attackWidth;
        public float Damage => damage;
        public float FireRate => fireRate;
        public float ProjectileSpeed => projectileSpeed;
        public TargetingMode TargetingMode => targetingMode;
        public float Size => size;
        public Color Color => color;
    }
}
