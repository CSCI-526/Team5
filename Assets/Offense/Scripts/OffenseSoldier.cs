using System.Collections.Generic;
using UnityEngine;
using ReverseTD.Shared;
using ReverseTD.Defense;
using UnityEngine.SceneManagement;

namespace ReverseTD.Offense
{
    public class OffenseSoldier : MonoBehaviour, IDamageable, IPathProgress
    {
        private const float AttackInterval = 1f;
        private const float ShotSpeed = 8f;
        private const float DeathThreshold = 0.001f;
        private const int CenterColumn = 1;
        private const int RightColumn = 2;

        [SerializeField] private float maxHp;
        [SerializeField] private float hp;
        [SerializeField] private float speed;
        [SerializeField] private float damage;
        [SerializeField] private float defense;
        [SerializeField] private float attackRange;

        private readonly List<IDamageable> candidates = new List<IDamageable>();
        private LanePath lane;
        private LanePath lowerLane;
        private ForkCommandSlot forkCommandSlot;

        private float forkDistance;
        private bool forkDecisionMade = false;
        private SoldierVisual visual;
        private OffenseData offenseData;
        private UnitType type;
        private int column;
        private float sideOffset;
        private float distanceTraveled;
        private float attackCooldown;
        private bool isAlive = true;

        // Shared Contract Implementation
        public Team Team => Team.Attacker;
        public bool IsAlive => isAlive;
        public Vector3 Position => transform.position;
        public float DistanceTraveled => distanceTraveled;
        public UnitType Type => type;

        public void Initialize(OffenseData data, UnitType unitType, int unitColumn, float startDistance, float laneOffset, float towerAttackRange, LanePath upperLane, LanePath alternateLane, float forkAt, ForkCommandSlot commandSlot)
        {
            offenseData = data;
            type = unitType;
            column = unitColumn;
            distanceTraveled = startDistance;
            sideOffset = laneOffset;

            lane = upperLane;
            lowerLane = alternateLane;
            forkCommandSlot = commandSlot;
            forkDistance = forkAt;

            forkDecisionMade = false;

            // Pull values dynamically from persistent upgrade data
            int[] levels = data != null ? data.upgradeLevels : null;
            maxHp = SquadRules.MaxHealth(type, levels);
            hp = maxHp;
            speed = SquadRules.Speed;
            damage = SquadRules.AttackDamage(levels);
            defense = SquadRules.Defense(type, levels);
            attackRange = towerAttackRange;

            transform.position = lane.Sample(distanceTraveled, sideOffset);

            // Create the soldier's visual (body, gun, HP bar)
            visual = SoldierVisual.Create(transform, SquadRules.UnitSize, UnitLooks.For(type));
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
            if (!isAlive || lane == null) return;
            float nextDistance = distanceTraveled + speed * Time.deltaTime;

            // Check the route when the soldier reaches the fork
            if (!forkDecisionMade &&
                lowerLane != null &&
                nextDistance >= forkDistance)
            {
                forkDecisionMade = true;

                if (ShouldUseLowerPath())
                {
                    lane = lowerLane;
                    Debug.Log(gameObject.name + " chose LOWER path");
                }
                else
                {
                    Debug.Log(gameObject.name + " chose UPPER path");
                }
            }

            distanceTraveled = nextDistance;
            transform.position = lane.Sample(distanceTraveled, sideOffset);

            // Reached the end of the track
            if (distanceTraveled >= lane.TotalLength)
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

            if (type == UnitType.Attack)
            {
                Attack();
            }
        }

        public float TakeDamage(float amount)
        {
            if (!isAlive) return 0f;

            float applied = Mathf.Min(amount * (1f - defense), hp);
            hp -= applied;
            if (visual != null) visual.ShowHealth(hp / maxHp);
            if (hp <= DeathThreshold) Despawn();
            return applied;
        }

        public void Heal(float amount)
        {
            if (!isAlive || amount <= 0f || hp >= maxHp) return;

            hp = Mathf.Min(maxHp, hp + amount);
            if (visual != null) visual.ShowHeal(hp / maxHp);
        }

        private bool ShouldUseLowerPath()
        {
            if (forkCommandSlot == null) return false;

            if (forkCommandSlot.CurrentCommand == RouteCommand.Split && column != CenterColumn)
            {
                return column == RightColumn;
            }
            return forkCommandSlot.ShouldUseLowerPath();
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
        private void GrantGold(float damageDealt)
        {
            if (offenseData != null)
            {
                offenseData.AddEarnedGold(damageDealt);
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