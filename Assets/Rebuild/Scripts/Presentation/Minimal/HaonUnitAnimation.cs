using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    // Optional visual observer. Never advances the simulation or writes UnitState.
    [DefaultExecutionOrder(150)]
    public sealed class HaonUnitAnimation : MonoBehaviour
    {
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private float maxHeight = 1.2f;
        [SerializeField] private float maxFootprint = .72f;
        [SerializeField] private float facingYawOffset;
        private UnitView view;
        private BattleSession session;
        private double sampledTime;
        private float sampledProgress;
        private Vector3 sampledAnchor, modelPosition;
        private bool wasPaused, moving;
        public bool IsMoving => moving;
        public Animator CharacterAnimator => characterAnimator;

        public void Bind(UnitView owner, BattleSession battle)
        {
            view = owner;
            session = battle;
            if (modelRoot == null || characterAnimator == null) return;
            if (battle.ContinuousSquadMovement)
            {
                maxFootprint = Mathf.Min(battle.ContinuousVisualFootprint, battle.ContinuousSoldierRadius * 2);
                maxHeight = battle.ContinuousSoldierRadius * 4.75f;
            }
            characterAnimator.applyRootMotion = false;
            characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            characterAnimator.SetBool("IsMoving", false);
            characterAnimator.Play("Idle", 0, 0);
            characterAnimator.Update(0);
            FitModel();
            modelPosition = modelRoot.localPosition;
            desiredRotation = modelRoot.rotation;
            sampledTime = session.Simulation != null ? session.Simulation.ElapsedTime : 0;
            sampledProgress = view.State.RouteProgress;
            sampledAnchor = session.ContinuousSquadMovement ? view.State.Position : view.State.AnchorPosition;
            moving = false;
        }

        private void FitModel()
        {
            var envelope = default(Bounds);
            var found = false;
            var baked = new Mesh { name = "Haon temporary fit bounds", hideFlags = HideFlags.HideAndDontSave };
            // Include both locomotion poses so a swinging arm does not enlarge the fitted footprint.
            foreach (var state in new[] { "Idle", "Run" })
                for (var sample = 0; sample < 8; sample++)
                {
                    characterAnimator.SetBool("IsMoving", state == "Run");
                    characterAnimator.Play(state, 0, sample / 8f);
                    characterAnimator.Update(0);
                    if (!TryGetPoseBounds(baked, out var pose)) continue;
                    if (!found) { envelope = pose; found = true; }
                    else envelope.Encapsulate(pose);
                }
            characterAnimator.SetBool("IsMoving", false);
            characterAnimator.Play("Idle", 0, 0);
            characterAnimator.Update(0);
            if (!found) { Destroy(baked); return; }
            var width = Mathf.Max(envelope.size.x, envelope.size.z);
            if (session != null && session.ContinuousSquadMovement)
                width = new Vector2(envelope.size.x, envelope.size.z).magnitude;
            var factor = Mathf.Min(maxHeight / Mathf.Max(.001f, envelope.size.y), maxFootprint / Mathf.Max(.001f, width));
            modelRoot.localScale *= factor;
            characterAnimator.Update(0);
            if (TryGetPoseBounds(baked, out var bounds))
                modelRoot.localPosition += Vector3.up * ((view.transform.position.y - bounds.min.y) / view.transform.lossyScale.y);
            Destroy(baked);
        }

        private bool TryGetPoseBounds(Mesh baked, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is LineRenderer) continue;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    // Imported skinned culling bounds can be oversized or stale before the first render.
                    // Bake only during initial fitting to measure the actual posed mesh instead.
                    skin.BakeMesh(baked);
                    baked.RecalculateBounds();
                    var local = baked.bounds;
                    for (var z = -1; z <= 1; z += 2)
                    for (var y = -1; y <= 1; y += 2)
                    for (var x = -1; x <= 1; x += 2)
                    {
                        var point = skin.transform.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z)));
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                else if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private void LateUpdate()
        {
            if (view == null || view.State == null || session == null || characterAnimator == null || modelRoot == null) return;
            var simulation = session.Simulation;
            var paused = simulation == null || simulation.IsPaused || simulation.Outcome != BattleOutcome.Running || !session.isActiveAndEnabled;
            if (paused)
            {
                moving = false;
                characterAnimator.SetBool("IsMoving", false);
                // Pause immediately in an Idle pose, rather than freezing a running stride.
                if (!wasPaused)
                {
                    characterAnimator.Play("Idle", 0, 0);
                    characterAnimator.Update(0);
                }
                characterAnimator.speed = 0;
            }
            else
            {
                characterAnimator.speed = session.EffectivePlaybackSpeed;
                if (simulation.ElapsedTime != sampledTime)
                {
                    // Hold the last tick's decision across render frames with no new FixedUpdate.
                    // Formation teleports change offsets but do not count as running.
                    moving = view.State.IsAlive && view.State.RouteProgress > sampledProgress + .000001f;
                    var actual = session.ContinuousSquadMovement ? view.State.Position : view.State.AnchorPosition;
                    var direction = actual - sampledAnchor;
                    direction.y = 0;
                    if (moving && direction.sqrMagnitude > .00000001f)
                        desiredRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, facingYawOffset, 0);
                    sampledProgress = view.State.RouteProgress;
                    sampledAnchor = actual;
                    sampledTime = simulation.ElapsedTime;
                }
                characterAnimator.SetBool("IsMoving", moving);
                if (moving)
                    modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, desiredRotation, 1 - Mathf.Exp(-14 * Time.unscaledDeltaTime));
            }
            wasPaused = paused;
            // Root motion is disabled; also preserve the visual child's foot offset defensively.
            modelRoot.localPosition = modelPosition;
        }

        private Quaternion desiredRotation = Quaternion.identity;
    }
}
