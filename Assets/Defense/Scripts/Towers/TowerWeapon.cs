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

        public void Initialize(TowerDefinition towerDefinition, TowerTargeting towerTargeting, TowerVisual towerVisual, TargetMarker targetMarker)
        {
            definition = towerDefinition;
            targeting = towerTargeting;
            visual = towerVisual;
            marker = targetMarker;
        }

        private void Update()
        {
            target = targeting.FindTarget();

            cooldown -= Time.deltaTime;
            if (cooldown > 0f)
            {
                if (target != null)
                {
                    visual.AimAt(target.Position);
                }
                return;
            }

            if (target == null)
            {
                cooldown = 0f;
                return;
            }

            Projectile shot = Projectile.Launch(transform.position, target, definition.Damage, definition.ProjectileSpeed);
            cooldown += 1f / definition.FireRate;
            shot.AppearFrom(visual.Fire(target.Position));
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
