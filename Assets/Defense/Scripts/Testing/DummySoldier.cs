using System.Collections.Generic;
using ReverseTD.Shared;
using UnityEngine;

namespace ReverseTD.Defense.Testing
{
    public class DummySoldier : MonoBehaviour, IDamageable, IPathProgress
    {
        private const float Size = 0.4f;
        private const float AttackInterval = 1f;
        private const float ShotSpeed = 8f;

        private static readonly Color fullHealthColor = new Color(0.95f, 0.35f, 0.3f);
        private static readonly Color lowHealthColor = new Color(0.35f, 0.08f, 0.08f);

        [SerializeField, Tooltip("Hit points at spawn. Set by the spawner.")]
        private float maxHp;

        [SerializeField, Tooltip("Current hit points.")]
        private float hp;

        [SerializeField, Tooltip("Walking speed, in world units per second. Set by the spawner.")]
        private float speed;

        [SerializeField, Tooltip("Damage per attack on a tower. Set by the spawner.")]
        private float damage;

        [SerializeField, Tooltip("How close a tower must be to attack it, in world units. Set by the spawner.")]
        private float attackRange;

        private readonly List<IDamageable> candidates = new List<IDamageable>();
        private StagePath path;
        private SoldierVisual visual;
        private float distanceTraveled;
        private float attackCooldown;
        private bool isAlive = true;

        public Team Team => Team.Attacker;
        public bool IsAlive => isAlive;
        public Vector3 Position => transform.position;
        public float DistanceTraveled => distanceTraveled;

        public void Initialize(StagePath stagePath, float hitPoints, float walkSpeed, float attackDamage, float towerAttackRange)
        {
            path = stagePath;
            maxHp = hitPoints;
            hp = hitPoints;
            speed = walkSpeed;
            damage = attackDamage;
            attackRange = towerAttackRange;
            transform.position = path.Start;

            visual = SoldierVisual.Create(transform, Size, fullHealthColor, lowHealthColor);
        }

        private void OnEnable()
        {
            CombatRegistry.Register(this);
        }

        private void OnDisable()
        {
            CombatRegistry.Unregister(this);
        }

        private void Update()
        {
            if (!isAlive)
            {
                return;
            }

            distanceTraveled += speed * Time.deltaTime;
            transform.position = path.GetPointAtDistance(distanceTraveled);

            if (distanceTraveled >= path.TotalLength)
            {
                Despawn();
                return;
            }

            Attack();
        }

        public void TakeDamage(float amount)
        {
            if (!isAlive)
            {
                return;
            }

            hp -= amount;
            visual.ShowHealth(hp / maxHp);
            if (hp <= 0f)
            {
                Despawn();
            }
        }

        private void Attack()
        {
            attackCooldown -= Time.deltaTime;
            if (attackCooldown > 0f)
            {
                return;
            }

            IDamageable target = FindNearestTower();
            if (target == null)
            {
                attackCooldown = 0f;
                return;
            }

            Projectile shot = Projectile.Launch(transform.position, target, damage, ShotSpeed);
            attackCooldown += AttackInterval;
            shot.AppearFrom(visual.Fire(target.Position));
        }

        private IDamageable FindNearestTower()
        {
            CombatRegistry.GetAlive(Team.Defender, candidates);

            Vector2 origin = transform.position;
            float nearestSqr = attackRange * attackRange;
            IDamageable nearest = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                float distanceSqr = ((Vector2)candidates[i].Position - origin).sqrMagnitude;
                if (distanceSqr <= nearestSqr)
                {
                    nearest = candidates[i];
                    nearestSqr = distanceSqr;
                }
            }
            return nearest;
        }

        private void Despawn()
        {
            isAlive = false;
            Destroy(gameObject);
        }
    }
}
