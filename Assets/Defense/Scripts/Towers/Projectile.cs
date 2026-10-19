using ReverseTD.Shared;
using ReverseTD.Offense;
using UnityEngine;

namespace ReverseTD.Defense
{
    public class Projectile : MonoBehaviour
    {
        private const string ObjectName = "Projectile";
        private const float HitDistance = 0.05f;
        private const float MaxLifetime = 5f;
        private const float AtTowerScale = 0.75f;
        private const float TrailAlpha = 0.2f;
        private const float CoreAlpha = 0.9f;
        private const float TowerImpactSize = 0.62f;
        private const float SoldierImpactSize = 0.42f;
        private const float MinDistance = 0.0001f;
        private System.Action<float> onHit;

        private IDamageable target;
        private OffenseWaveManager targetFormation;
        private int targetCell = -1;
        private LanePath targetLane;
        private float targetDistance;
        private Transform visual;
        private Color color;
        private Vector3 aimPoint;
        private Vector3 visualOffset;
        private float launchDistance;
        private float impactSize;
        private float damage;
        private float speed;
        private float age;
        private bool fliesStraight;
        private Vector2 flightDirection;
        private Vector3 flightOrigin;
        private float flightRange;
        private float flightWidth;
        private float flightDistance;

        public static Projectile LaunchStraight(Vector3 origin, IDamageable target, float damage, float speed,
            Vector2 direction, float range, float width)
        {
            Projectile projectile = Launch(origin, target, damage, speed);
            projectile.fliesStraight = true;
            projectile.flightDirection = direction.normalized;
            projectile.flightOrigin = origin;
            projectile.flightRange = range;
            projectile.flightWidth = width;
            projectile.launchDistance = Mathf.Max(range, MinDistance);
            projectile.UpdateVisual();
            return projectile;
        }

        public static Projectile Launch(Vector3 origin, IDamageable target, float damage, float speed, System.Action<float> onHit = null)
        {
            bool atTower = target.Team == Team.Defender;
            Color shotColor = Palette.ShotColor(target.Team);
            float scale = atTower ? AtTowerScale : 1f;

            var projectileObject = new GameObject(ObjectName);
            projectileObject.transform.position = origin;

            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(projectileObject.transform, false);
            visual.localScale = new Vector3(scale, scale, 1f);

            int order = SortingOrders.Projectile;
            ShapeSprites.AddPart(visual, "Trail", ShapeSprites.Circle, new Vector2(-0.2f, 0f), new Vector2(0.34f, 0.06f), Palette.WithAlpha(shotColor, TrailAlpha), order);
            ShapeSprites.AddPart(visual, "Body", ShapeSprites.Circle, Vector2.zero, new Vector2(0.3f, 0.11f), shotColor, order + 1);
            ShapeSprites.AddPart(visual, "Core", ShapeSprites.Circle, new Vector2(0.04f, 0f), new Vector2(0.14f, 0.05f), new Color(1f, 1f, 1f, CoreAlpha), order + 2);

            var projectile = projectileObject.AddComponent<Projectile>();
            projectile.target = target;
            // Tower shots lock the cell at launch, not the unit occupying it.
            OffenseWaveManager formation = OffenseWaveManager.Instance;
            if (target is OffenseSoldier soldier && formation != null)
            {
                int cell = formation.GetSoldierCell(soldier);
                if (cell >= 0)
                {
                    projectile.targetFormation = formation;
                    projectile.targetCell = cell;
                    projectile.targetLane = soldier.CurrentLane;
                    projectile.targetDistance = soldier.DistanceTraveled;
                }
            }
            projectile.aimPoint = target.Position;
            projectile.damage = damage;
            projectile.speed = speed;
            projectile.onHit = onHit;
            projectile.visual = visual;
            projectile.color = shotColor;
            projectile.impactSize = atTower ? TowerImpactSize : SoldierImpactSize;
            projectile.launchDistance = Mathf.Max((projectile.aimPoint - origin).magnitude, MinDistance);
            projectile.UpdateVisual();
            return projectile;
        }

        public void AppearFrom(Vector3 worldPoint)
        {
            visualOffset = worldPoint - transform.position;
            launchDistance = Mathf.Max(fliesStraight ? flightRange : (aimPoint - transform.position).magnitude, MinDistance);
            UpdateVisual();
        }

        private void Update()
        {
            if (OffenseWaveManager.Instance != null && OffenseWaveManager.Instance.IsCombatPaused) return;
            if (targetCell >= 0)
            {
                target = targetFormation != null ? targetFormation.GetSoldierAt(targetCell) : null;
                if (target is OffenseSoldier occupant)
                {
                    targetLane = occupant.CurrentLane;
                    targetDistance = occupant.DistanceTraveled;
                }
                else if (targetLane != null)
                {
                    // An empty cell still moves with the formation; a hit there deals no damage.
                    targetDistance += SquadRules.Speed * Time.deltaTime;
                    aimPoint = targetLane.Sample(targetDistance, 0f) + SquadRules.CellOffset(targetCell, SquadRules.Spacing);
                }
            }
            bool targetAlive = CombatRegistry.IsAlive(target);
            if (targetAlive)
            {
                aimPoint = target.Position;
            }

            if (fliesStraight)
            {
                TickStraightFlight(targetAlive);
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, aimPoint, speed * Time.deltaTime);
            if ((transform.position - aimPoint).sqrMagnitude <= HitDistance * HitDistance)
            {
                LevelOneTutorialRewind.Retire(gameObject);
                if (targetAlive)
                {
                    float applied = target.TakeDamage(damage);
                    onHit?.Invoke(applied);
                    CombatEffects.SpawnImpact(aimPoint, color, impactSize);
                }
                return;
            }

            age += Time.deltaTime;
            if (age >= MaxLifetime)
            {
                LevelOneTutorialRewind.Retire(gameObject);
            }
        }

        private void LateUpdate()
        {
            UpdateVisual();
        }

        private void TickStraightFlight(bool targetAlive)
        {
            float previousDistance = flightDistance;
            flightDistance = Mathf.Min(flightRange, flightDistance + speed * Time.deltaTime);
            transform.position = flightOrigin + (Vector3)(flightDirection * flightDistance);

            if (targetAlive)
            {
                Vector2 offset = target.Position - flightOrigin;
                float forward = Vector2.Dot(offset, flightDirection);
                bool withinLane = TowerAttackGeometry.Contains(offset, flightDirection, flightRange, flightWidth);
                // Sweep the distance travelled this frame so fast shots do not skip a unit.
                if (withinLane && forward >= previousDistance - HitDistance && forward <= flightDistance + HitDistance)
                {
                    float applied = target.TakeDamage(damage);
                    onHit?.Invoke(applied);
                    CombatEffects.SpawnImpact(aimPoint, color, impactSize);
                    LevelOneTutorialRewind.Retire(gameObject);
                    return;
                }
            }

            age += Time.deltaTime;
            if (flightDistance >= flightRange || age >= MaxLifetime) LevelOneTutorialRewind.Retire(gameObject);
        }

        internal System.Action CaptureTutorialState()
        {
            // Launch configuration, slot lock and hit callback stay on this retained object.
            IDamageable savedTarget = target;
            LanePath savedLane = targetLane;
            float savedTargetDistance = targetDistance, savedAge = age, savedFlightDistance = flightDistance;
            Vector3 savedPosition = transform.position, savedAim = aimPoint;
            return () =>
            {
                target = savedTarget;
                targetLane = savedLane;
                targetDistance = savedTargetDistance;
                age = savedAge;
                flightDistance = savedFlightDistance;
                aimPoint = savedAim;
                transform.position = savedPosition;
                UpdateVisual();
                gameObject.SetActive(true);
            };
        }

        private void UpdateVisual()
        {
            Vector3 position = transform.position;
            float remaining = fliesStraight ? flightRange - flightDistance : (aimPoint - position).magnitude;
            Vector3 shown = position + visualOffset * Mathf.Clamp01(remaining / launchDistance);

            Vector2 heading = fliesStraight ? flightDirection : (Vector2)(aimPoint - shown);
            if (heading.sqrMagnitude > MinDistance)
            {
                visual.SetPositionAndRotation(shown, Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg));
            }
            else
            {
                visual.position = shown;
            }
        }
    }
}
