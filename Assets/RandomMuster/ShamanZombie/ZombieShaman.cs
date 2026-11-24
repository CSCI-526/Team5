using System.Collections;
using System.Linq;
using Team5.BasicTowerTrial.Presentation;
using UnityEngine;

namespace RandomMuster
{
    [RequireComponent(typeof(ZombieUnit), typeof(Animator))]
    public sealed class ZombieShaman : MonoBehaviour
    {
        [SerializeField, Min(.3f)] private float cooldown = 8f;
        [SerializeField, Min(0)] private float radius = 3.392857f;
        [SerializeField, Min(1)] private int maxTargets = 3;
        [SerializeField, Min(1)] private float healAmount = 50f;

        private static readonly int HealTrigger = Animator.StringToHash("Heal");
        private const float ReleaseTime = .25f;
        private const float CastDuration = .62f;
        private ZombieUnit unit;
        private Animator animator;
        private HealingRangeEffect healEffect;
        private bool effectVisible;
        private float nextCheck;

        private void Awake()
        {
            unit = GetComponent<ZombieUnit>();
            animator = GetComponent<Animator>();
        }

        private void OnEnable() => nextCheck = Time.time + cooldown;

        private void OnDisable() => SetEffectVisible(false);

        private void OnDestroy() => healEffect?.DestroyMaterials();

        private void Update()
        {
            if (!unit.IsAlive || Time.time < nextCheck) return;
            nextCheck = Time.time + .2f;

            // Zombie units in this scene belong to the same side.
            var targets = FindObjectsByType<ZombieUnit>(FindObjectsSortMode.None)
                .Where(CanHeal)
                .OrderBy(DistanceSquared)
                .Take(maxTargets)
                .ToArray();
            if (targets.Length == 0) return;

            nextCheck = Time.time + cooldown;
            unit.DelayAttack(CastDuration);
            animator.ResetTrigger("Attack");
            animator.SetTrigger(HealTrigger);
            StartCoroutine(ReleaseHeal(targets));
        }

        private void LateUpdate()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != HealTrigger && animator.IsInTransition(0))
                state = animator.GetNextAnimatorStateInfo(0);

            var healing = unit.IsAlive && state.shortNameHash == HealTrigger && state.normalizedTime < 1f;
            SetEffectVisible(healing);
            if (!healing) return;

            var phase = Mathf.Clamp01(state.normalizedTime);
            healEffect.Draw(radius, phase, 1f - phase);
        }

        private void SetEffectVisible(bool visible)
        {
            if (effectVisible == visible) return;
            if (visible && healEffect == null)
            {
                healEffect = new HealingRangeEffect(transform, new Color(1f, .15f, .16f), new Color(1f, .31f, .29f));
                healEffect.SetScale(1f / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.x)));
            }
            healEffect.SetVisible(visible);
            effectVisible = visible;
        }

        private IEnumerator ReleaseHeal(ZombieUnit[] targets)
        {
            yield return new WaitForSeconds(ReleaseTime);
            if (!unit.IsAlive) yield break;
            foreach (var target in targets)
            {
                if (!CanHeal(target)) continue;
                var previousHealth = target.Health;
                target.Heal(healAmount);
                if (target.Health > previousHealth)
                    ShamanHealFeedback.Show(target.transform);
            }
        }

        private bool CanHeal(ZombieUnit target) =>
            target != null && target.isActiveAndEnabled && target.gameObject.scene == gameObject.scene &&
            target.IsAlive && target.Health < target.MaxHealth && DistanceSquared(target) <= radius * radius;

        private float DistanceSquared(ZombieUnit target)
        {
            var delta = target.transform.position - transform.position;
            return delta.x * delta.x + delta.z * delta.z;
        }
    }
}
