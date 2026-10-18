using ReverseTD.Shared;
using UnityEngine;

namespace ReverseTD.Defense
{
    public class TowerWeapon : MonoBehaviour
    {
        private TowerDefinition definition;
        private TowerTargeting targeting;
        private TowerVisual visual;
        private TargetMarker marker;
        private IDamageable target;
        private float cooldown;
        private Vector2 direction;
        private float range;

        public void Initialize(TowerDefinition towerDefinition, TowerTargeting towerTargeting, TowerVisual towerVisual, TargetMarker targetMarker, Vector2 attackDirection, float attackRange)
        {
            definition = towerDefinition;
            targeting = towerTargeting;
            visual = towerVisual;
            marker = targetMarker;
            direction = attackDirection;
            range = attackRange;
        }

        private void Update()
        {
            if (ReverseTD.Offense.OffenseWaveManager.Instance != null && ReverseTD.Offense.OffenseWaveManager.Instance.IsSquadEditing) return;
            target = targeting.FindTarget();

            cooldown -= Time.deltaTime;
            if (cooldown > 0f)
            {
                return;
            }

            if (target == null)
            {
                cooldown = 0f;
                return;
            }

            Projectile shot = Projectile.LaunchStraight(transform.position, target, definition.Damage, definition.ProjectileSpeed,
                direction, range, definition.AttackWidth);
            cooldown += 1f / definition.FireRate;
            shot.AppearFrom(visual.Fire(transform.position + (Vector3)direction));
        }

        private void LateUpdate()
        {
            if (CombatRegistry.IsAlive(target))
            {
                marker.Show(target.Position);
            }
            else
            {
                marker.Hide();
            }
        }
    }
}
