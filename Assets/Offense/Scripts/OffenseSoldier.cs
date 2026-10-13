using System.Collections.Generic;
using UnityEngine;
using ReverseTD.Shared;
using ReverseTD.Defense;
using UnityEngine.SceneManagement;

namespace ReverseTD.Offense
{
    public class OffenseSoldier : MonoBehaviour, IDamageable, IPathProgress
    {
        private const float Size = 0.4f;
        private const float AttackInterval = 1f;
        private const float ShotSpeed = 8f;

        private static readonly Color fullHealthColor = new Color(0.95f, 0.35f, 0.3f);
        private static readonly Color lowHealthColor = new Color(0.35f, 0.08f, 0.08f);

        [SerializeField] private float maxHp;
        [SerializeField] private float hp;
        [SerializeField] private float speed;
        [SerializeField] private float damage;
        [SerializeField] private float attackRange;

        private readonly List<IDamageable> candidates = new List<IDamageable>();
        private StagePath path;
        private StagePath lowerPath;
        private ForkCommandSlot forkCommandSlot;

        private float forkDistance;
        private bool forkDecisionMade = false;
        private SoldierVisual visual;
        private OffenseData offenseData;
        private float distanceTraveled;
        private float attackCooldown;
        private bool isAlive = true;

        // Shared Contract Implementation
        public Team Team => Team.Attacker;
        public bool IsAlive => isAlive;
        public Vector3 Position => transform.position;
        public float DistanceTraveled => distanceTraveled;

        public void Initialize(StagePath stagePath,OffenseData data,float towerAttackRange = 1.5f,StagePath alternatePath = null,ForkCommandSlot commandSlot = null)
        {
            path = stagePath;
            offenseData = data;

            lowerPath = alternatePath;
            forkCommandSlot = commandSlot;

            forkDistance = Vector2.Distance(
                stagePath.Waypoints[0],
                stagePath.Waypoints[1]
            );

            forkDecisionMade = false;

            // Pull values dynamically from persistent upgrade data
            maxHp = data != null ? data.MaxHealth : 5f;
            hp = maxHp;
            speed = 1.0f;
            damage = data != null ? data.AttackDamage : 1f;
            attackRange = towerAttackRange;

            transform.position = path.Start;

            // Create the soldier's visual (body, gun, HP bar)
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
            if (!isAlive || path == null) return;
            float nextDistance = distanceTraveled + speed * Time.deltaTime;

            // Check the route when the soldier reaches the fork
            if (!forkDecisionMade &&
                lowerPath != null &&
                forkCommandSlot != null &&
                distanceTraveled <= forkDistance &&
                nextDistance >= forkDistance)
            {
                forkDecisionMade = true;

                if (forkCommandSlot.ShouldUseLowerPath())
                {
                    path = lowerPath;
                    Debug.Log(gameObject.name + " chose LOWER path");
                }
                else
                {
                    Debug.Log(gameObject.name + " chose UPPER path");
                }
            }

            distanceTraveled = nextDistance;
            transform.position = path.GetPointAtDistance(distanceTraveled);

            // Reached the end of the track
            if (distanceTraveled >= path.TotalLength)
            {
                if (OffenseWaveManager.Instance != null)
                {
                    enabled = false;
                    OffenseWaveManager.Instance.ReportGoalReached();
                }
                else
                {
                    Debug.LogError("OffenseWaveManager is missing.");
                }

                return;
            }

            Attack();
        }

        public void TakeDamage(float amount)
        {
            if (!isAlive) return;

            hp -= amount;
            if (visual != null)
            {
                visual.ShowHealth(hp / maxHp);
            }

            if (hp <= 0f)
            {
                Despawn();
            }
        }

        private void Attack()
        {
            attackCooldown -= Time.deltaTime;
            if (attackCooldown > 0f) return;

            IDamageable target = FindNearestTower();
            if (target == null)
            {
                attackCooldown = 0f;
                return;
            }

            Projectile shot = Projectile.Launch(transform.position, target, damage, ShotSpeed, GrantGold);
            attackCooldown += AttackInterval;
            shot.AppearFrom(visual.Fire(target.Position));
        }

        // Incremental reward: grant gold whenever this unit deals damage to a tower
        private void GrantGold()
        {
            if (offenseData != null)
            {
                offenseData.gold += damage;
            }
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

            // Notify the wave manager that this unit is off the board
            if (OffenseWaveManager.Instance != null)
            {
                OffenseWaveManager.Instance.ReportSoldierDeath();
            }

            Destroy(gameObject);
        }
    }
}