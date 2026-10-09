using ReverseTD.Shared;
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

        private IDamageable target;
        private System.Action onHit;
        private Transform visual;
        private Color color;
        private Vector3 aimPoint;
        private Vector3 visualOffset;
        private float launchDistance;
        private float impactSize;
        private float damage;
        private float speed;
        private float age;

        public static Projectile Launch(Vector3 origin, IDamageable target, float damage, float speed, System.Action onHit = null)
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
            launchDistance = Mathf.Max((aimPoint - transform.position).magnitude, MinDistance);
            UpdateVisual();
        }

        private void Update()
        {
            bool targetAlive = CombatRegistry.IsAlive(target);
            if (targetAlive)
            {
                aimPoint = target.Position;
            }

            transform.position = Vector3.MoveTowards(transform.position, aimPoint, speed * Time.deltaTime);
            if ((transform.position - aimPoint).sqrMagnitude <= HitDistance * HitDistance)
            {
                Destroy(gameObject);
                if (targetAlive)
                {
                    target.TakeDamage(damage);
                    onHit?.Invoke();
                    CombatEffects.SpawnImpact(aimPoint, color, impactSize);
                }
                return;
            }

            age += Time.deltaTime;
            if (age >= MaxLifetime)
            {
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            Vector3 position = transform.position;
            float remaining = (aimPoint - position).magnitude;
            Vector3 shown = position + visualOffset * Mathf.Clamp01(remaining / launchDistance);

            Vector2 heading = aimPoint - shown;
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
