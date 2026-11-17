using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    public enum PaintSquadAnimationState { Idle, Run, Shoot, Hit, Die, Cheer }

    // Optional visual observer for the Paint Squad role prefabs. Never advances the simulation or writes UnitState.
    // Drives the ported Soldier/SoldierHeavy/Hero controllers: Speed (Idle/Run blend), Shoot, Hit, Die, Cheer.
    [DefaultExecutionOrder(150)]
    public sealed class PaintSquadUnitAnimation : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int ShootId = Animator.StringToHash("Shoot");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int DieId = Animator.StringToHash("Die");
        private static readonly int CheerId = Animator.StringToHash("Cheer");
        private const float ActionHoldSeconds = .3f;
        private const float AimHoldSeconds = .6f;

        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform blobShadow;
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject hitSplashPrefab;
        [Tooltip("Height above the foot point where hit splashes appear.")]
        [SerializeField] private float hitHeight = .5f;
        [SerializeField] private float fallenBodySeconds = 1.1f;
        private UnitView view;
        private BattleSession session;
        private BattleSimulation simulation;
        private double sampledTime;
        private float sampledProgress, sampledCooldown, actionUntil, aimUntil;
        private Vector3 sampledAnchor, sampledPosition, modelPosition;
        private Quaternion desiredRotation = Quaternion.identity;
        private PaintSquadAnimationState action;
        private bool wasPaused, moving, cheering, dead;

        public Animator CharacterAnimator => characterAnimator;
        public Transform ModelRoot => modelRoot;
        public Transform Muzzle => muzzle;
        public bool IsMoving => moving;
        // Most recent high-level state requested from the Animator (Shoot/Hit read back for a short hold).
        public PaintSquadAnimationState State =>
            dead ? PaintSquadAnimationState.Die : cheering ? PaintSquadAnimationState.Cheer :
            Time.time < actionUntil ? action : moving ? PaintSquadAnimationState.Run : PaintSquadAnimationState.Idle;

        public void Bind(UnitView owner, BattleSession battle)
        {
            Unsubscribe();
            view = owner;
            session = battle;
            if (modelRoot == null || characterAnimator == null || view == null || view.State == null) return;
            simulation = battle.Simulation;
            if (simulation != null)
            {
                simulation.UnitDamaged += OnUnitDamaged;
                simulation.LegendarySkillUsed += OnSkillUsed;
            }
            characterAnimator.applyRootMotion = false;
            characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            characterAnimator.SetFloat(SpeedId, 0);
            characterAnimator.SetBool(CheerId, false);
            // Desynchronise idle cycles so a squad does not breathe in lockstep.
            characterAnimator.Play("Locomotion", 0, Random.value);
            characterAnimator.Update(0);
            modelPosition = modelRoot.localPosition;
            desiredRotation = modelRoot.rotation;
            sampledTime = simulation != null ? simulation.ElapsedTime : 0;
            sampledProgress = view.State.RouteProgress;
            sampledPosition = view.State.Position;
            sampledAnchor = battle.ContinuousSquadMovement ? view.State.Position : view.State.AnchorPosition;
            sampledCooldown = view.State.AttackCooldown;
            moving = cheering = dead = false;
            actionUntil = aimUntil = 0;
        }

        private void OnUnitDamaged(DamageEvent value)
        {
            if (dead || view == null || view.State == null || value.UnitId != view.State.Id || value.Amount <= 0) return;
            characterAnimator.SetTrigger(HitId);
            SetAction(PaintSquadAnimationState.Hit);
            if (hitSplashPrefab != null)
                Spawn(hitSplashPrefab, transform.position + Vector3.up * hitHeight, Quaternion.identity, .6f);
        }

        private void OnSkillUsed(UnitRole role, int memberId, Vector3 point)
        {
            if (dead || view == null || view.State == null || memberId != view.State.MemberId) return;
            Shoot(point, point != view.State.Position);
        }

        private void Shoot(Vector3 target, bool hasTarget)
        {
            characterAnimator.SetTrigger(ShootId);
            SetAction(PaintSquadAnimationState.Shoot);
            if (hasTarget)
            {
                aimUntil = Time.time + AimHoldSeconds;
                var direction = target - view.transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude > .0001f) desiredRotation = Quaternion.LookRotation(direction);
            }
            if (muzzleFlashPrefab != null && muzzle != null)
                Spawn(muzzleFlashPrefab, muzzle.position, Quaternion.LookRotation(modelRoot.forward), .2f);
        }

        private void SetAction(PaintSquadAnimationState state)
        { action = state; actionUntil = Time.time + ActionHoldSeconds; }

        // Effects live beside the unit views so they never enlarge a unit's visible bounds.
        private void Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
        {
            var effect = Instantiate(prefab, position, rotation, view.transform.parent);
            Destroy(effect, lifetime);
        }

        // Called by BattlePresentation when the unit dies, just before its view is destroyed.
        // Moves the model and shadow into a short-lived holder so the Die take can play out.
        public GameObject LeaveFallenBody(Transform parent)
        {
            if (dead || modelRoot == null || characterAnimator == null) return null;
            dead = true;
            Unsubscribe();
            var body = new GameObject("PaintSquad Fallen " + (view != null && view.State != null ? view.State.Id : 0));
            body.transform.SetParent(parent, false);
            body.transform.SetPositionAndRotation(transform.position, transform.rotation);
            modelRoot.SetParent(body.transform, true);
            if (blobShadow != null) blobShadow.SetParent(body.transform, true);
            characterAnimator.speed = session != null ? Mathf.Max(1, session.EffectivePlaybackSpeed) : 1;
            characterAnimator.SetFloat(SpeedId, 0);
            characterAnimator.SetBool(CheerId, false);
            characterAnimator.SetTrigger(DieId);
            characterAnimator.Update(0);
            Destroy(body, fallenBodySeconds);
            return body;
        }

        private void LateUpdate()
        {
            if (dead || view == null || view.State == null || session == null || characterAnimator == null || modelRoot == null) return;
            var current = session.Simulation;
            if (current == null) return;
            if (current.Outcome == BattleOutcome.Victory && view.State.IsAlive)
            {
                if (!cheering)
                {
                    cheering = true;
                    moving = false;
                    characterAnimator.SetFloat(SpeedId, 0);
                    characterAnimator.SetBool(CheerId, true);
                }
                characterAnimator.speed = 1;
                modelRoot.localPosition = modelPosition;
                return;
            }
            var paused = current.IsPaused || current.Outcome != BattleOutcome.Running || !session.isActiveAndEnabled;
            if (paused)
            {
                moving = false;
                characterAnimator.SetFloat(SpeedId, 0);
                // Pause in an Idle pose, rather than freezing a running stride.
                if (!wasPaused)
                {
                    characterAnimator.Play("Locomotion", 0, 0);
                    characterAnimator.Update(0);
                }
                characterAnimator.speed = 0;
            }
            else
            {
                characterAnimator.speed = session.EffectivePlaybackSpeed;
                if (current.ElapsedTime != sampledTime) SampleTick(current);
                if (view.IsRidingPlatform)
                {
                    moving = view.IsPlatformRepositioning;
                    var localDirection = view.State.FormationOffset - view.PlatformRelativeOffset;
                    localDirection.y = 0;
                    if (moving && Time.time >= aimUntil && localDirection.sqrMagnitude > .00000001f)
                        desiredRotation = Quaternion.LookRotation(localDirection);
                }
                characterAnimator.SetFloat(SpeedId, moving ? 1 : 0, .08f, Time.deltaTime);
                modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, desiredRotation, 1 - Mathf.Exp(-14 * Time.unscaledDeltaTime));
            }
            wasPaused = paused;
            // Root motion is disabled; also preserve the model's foot offset defensively.
            modelRoot.localPosition = modelPosition;
        }

        private void SampleTick(BattleSimulation current)
        {
            var state = view.State;
            var continuous = session.ContinuousSquadMovement;
            var position = state.Position;
            var travelled = position - sampledPosition;
            travelled.y = 0;
            // Formation teleports change offsets but do not count as running (same rule as HaonUnitAnimation).
            moving = state.IsAlive && (state.RouteProgress > sampledProgress + .000001f ||
                (continuous && travelled.sqrMagnitude > .00000001f));
            var actual = continuous ? position : state.AnchorPosition;
            var direction = actual - sampledAnchor;
            direction.y = 0;
            if (moving && Time.time >= aimUntil && direction.sqrMagnitude > .00000001f)
                desiredRotation = Quaternion.LookRotation(direction);
            // A unit fires when its attack cooldown is re-armed; face the tower it shot at.
            if (state.IsAlive && state.AttackCooldown > sampledCooldown + .0001f)
            {
                var hasTarget = TryFindShotTarget(current, state.Id, out var target);
                Shoot(target, hasTarget);
            }
            sampledProgress = state.RouteProgress;
            sampledPosition = position;
            sampledAnchor = actual;
            sampledCooldown = state.AttackCooldown;
            sampledTime = current.ElapsedTime;
        }

        private static bool TryFindShotTarget(BattleSimulation current, int unitId, out Vector3 target)
        {
            var shots = current.Projectiles;
            // The newest projectile is at the end; stop at the first one from this unit.
            for (var i = shots.Count - 1; i >= 0 && i >= shots.Count - 64; i--)
            {
                var shot = shots[i];
                if (shot.SourceId != unitId || shot.TargetKind != ProjectileTargetKind.Tower) continue;
                foreach (var tower in current.Towers)
                    if (tower.Id == shot.TargetId) { target = tower.Position; return true; }
                break;
            }
            target = default;
            return false;
        }

        private void Unsubscribe()
        {
            if (simulation == null) return;
            simulation.UnitDamaged -= OnUnitDamaged;
            simulation.LegendarySkillUsed -= OnSkillUsed;
            simulation = null;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
