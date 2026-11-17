using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation
{
    // Presentation only: observes the simulation; never writes positions, slots or collision sizes.
    [DefaultExecutionOrder(160)]
    public sealed class FWUnitVisual : MonoBehaviour
    {
        [SerializeField] private Transform billboard;
        [SerializeField] private Transform facingRoot;
        [SerializeField] private Animator front;
        [SerializeField] private Animator back;
        [Tooltip("Always use the front Idle/Run animations; horizontal mirroring still follows movement.")]
        [SerializeField] private bool frontOnly;
        [Tooltip("Enable for artwork whose neutral pose faces screen right instead of FW's screen left.")]
        [SerializeField] private bool artworkFacesRight;
        [SerializeField] private SortingGroup sortingGroup;
        [Tooltip("Visual tilt toward world upright. 0 faces the camera directly; does not change the Camera or unit position.")]
        [SerializeField, Range(0, 45)] private float displayTiltDegrees;
        [Header("Optional scene-specific world Z priority")]
        [SerializeField] private bool worldZPriority;
        [SerializeField] private Material worldZMaterial;
        [SerializeField] private bool matchRoleHairColor;
        private Material worldZInstance;
        private Material hairInstance, faceHairInstance;
        private static readonly int FootZ = Shader.PropertyToID("_FWFootZ");
        private UnitView view;
        private BattleSession session;
        private Vector3 sampledPosition;
        private Vector3 sampledReference;
        private BattleSimulation observedSimulation;
        private bool formationFacingPending, wasFormationAdjusting;
        private double sampledTime;
        private bool moving, facingBack, mirrored, initialized;
        private static readonly int Moving = Animator.StringToHash("IsMoving");
        public bool IsMoving => moving;
        public bool FacingBack => facingBack;
        public bool Mirrored => mirrored;
        public Animator ActiveAnimator => facingBack ? back : front;
        public bool WorldZPriority => worldZPriority;
        public float DisplayTiltDegrees { get => displayTiltDegrees; set => displayTiltDegrees = Mathf.Clamp(value, 0, 45); }

        public void Bind(UnitView owner, BattleSession battle)
        {
            if (observedSimulation != null) observedSimulation.FormationChanged -= OnFormationChanged;
            view = owner;
            session = battle;
            observedSimulation = battle.Simulation;
            if (observedSimulation != null) observedSimulation.FormationChanged += OnFormationChanged;
            sampledReference = observedSimulation != null ? observedSimulation.SquadReferencePosition : Vector3.zero;
            formationFacingPending = wasFormationAdjusting = false;
            sampledPosition = owner.State.Position;
            sampledTime = battle.Simulation?.ElapsedTime ?? 0;
            foreach (var animator in new[] { front, back })
            {
                if (animator == null) continue;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.keepAnimatorStateOnDisable = true;
            }
            front.gameObject.SetActive(true);
            if (back != null && back != front) back.gameObject.SetActive(false);
            facingBack = moving = mirrored = false;
            front.Play("Idle", 0, 0);
            front.Update(0);
            initialized = true;
            if (worldZPriority && battle.GetComponent<WorldZSceneSorting>() == null)
                battle.gameObject.AddComponent<WorldZSceneSorting>();
        }

        private void OnFormationChanged()
        {
            formationFacingPending = true;
            // Starting a reform may recenter the logical reference without moving the squad.
            if (observedSimulation != null) sampledReference = observedSimulation.SquadReferencePosition;
        }

        // Also used by the prototype checks; direction is in world space, independent of navigation.
        public void SetLocomotion(Vector3 direction, bool isMoving, bool paused, Camera camera, float speed = 1)
        {
            if (front == null || (!frontOnly && back == null)) return;
            direction.y = 0;
            var nextBack = !frontOnly && facingBack;
            if (camera != null)
            {
                var displayRotation = camera.transform.rotation;
                var horizontalForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
                if (displayTiltDegrees > 0 && horizontalForward.sqrMagnitude > .000001f)
                    displayRotation = Quaternion.RotateTowards(displayRotation,
                        Quaternion.LookRotation(horizontalForward, Vector3.up), displayTiltDegrees);
                billboard.rotation = displayRotation;
                if (isMoving && direction.sqrMagnitude > .00000001f)
                {
                    var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
                    var away = Vector3.Dot(direction.normalized, forward);
                    if (!frontOnly && Mathf.Abs(away) > .12f) nextBack = away > 0;
                    var side = Vector3.Dot(direction.normalized, camera.transform.right);
                    if (Mathf.Abs(side) > .12f) mirrored = artworkFacesRight ? side < 0 : side > 0;
                }
                // Keep all parts of one character together; still depth-test against the 3D world.
                var depth = Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward);
                sortingGroup.sortingOrder = worldZPriority ? WorldZSceneSorting.Order(transform.position.z)
                    : Mathf.Clamp(Mathf.RoundToInt(-depth * 100), -30000, 30000);
            }
            if (worldZPriority && worldZMaterial != null)
            {
                if (worldZInstance == null)
                {
                    // GPU SpriteSkin requires the SRP Batcher. Property blocks disable that path,
                    // leaving animated bones with an undeformed on-screen sprite. Share one owned
                    // material across this character's parts instead of overriding renderer blocks.
                    worldZInstance = new Material(worldZMaterial) { name = worldZMaterial.name + " (Unit Instance)" };
                    if (matchRoleHairColor && view != null && view.State != null)
                    {
                        var color = LegacyUI.LegacyUnitLooks.For(view.State.Role).Body;
                        hairInstance = new Material(worldZMaterial) { name = "FW Role Hair " + view.State.Role };
                        hairInstance.SetColor("_FWHairColor", color); hairInstance.SetFloat("_FWHairMode", 1);
                        faceHairInstance = new Material(hairInstance) { name = "FW Face Hair " + view.State.Role };
                        faceHairInstance.SetFloat("_FWHairMode", 2);
                    }
                    foreach (var sprite in GetComponentsInChildren<SpriteRenderer>(true))
                        sprite.sharedMaterial = hairInstance != null && (sprite.name == "F_B_HAIR" || sprite.name == "B_HAIR")
                            ? hairInstance : faceHairInstance != null && sprite.name == "F_FACE" ? faceHairInstance : worldZInstance;
                }
                worldZInstance.SetFloat(FootZ, transform.position.z);
                if (hairInstance != null) hairInstance.SetFloat(FootZ, transform.position.z);
                if (faceHairInstance != null) faceHairInstance.SetFloat(FootZ, transform.position.z);
            }
            var scale = facingRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * (mirrored ? -1 : 1);
            facingRoot.localScale = scale;
            moving = isMoving && !paused;
            if (nextBack != facingBack)
            {
                var previous = ActiveAnimator;
                var info = previous.IsInTransition(0) ? previous.GetNextAnimatorStateInfo(0) : previous.GetCurrentAnimatorStateInfo(0);
                var phase = Mathf.Repeat(info.normalizedTime, 1);
                previous.gameObject.SetActive(false);
                facingBack = nextBack;
                ActiveAnimator.gameObject.SetActive(true);
                ActiveAnimator.SetBool(Moving, moving);
                ActiveAnimator.Play(moving ? "Run" : "Idle", 0, phase);
                ActiveAnimator.Update(0);
            }
            var active = ActiveAnimator;
            active.SetBool(Moving, moving);
            if (paused)
            {
                if (!active.GetCurrentAnimatorStateInfo(0).IsName("Idle") || active.IsInTransition(0))
                { active.Play("Idle", 0, 0); active.Update(0); }
                active.speed = 0;
            }
            else active.speed = speed;
        }

        private void LateUpdate()
        {
            if (!initialized || view == null || view.State == null || session == null) return;
            var simulation = session.Simulation;
            var paused = simulation == null || simulation.IsPaused || simulation.Outcome != BattleOutcome.Running || !session.isActiveAndEnabled;
            var direction = Vector3.zero;
            if (simulation != null && simulation.ElapsedTime != sampledTime)
            {
                direction = view.State.Position - sampledPosition;
                direction.y = 0;
                moving = view.State.IsAlive && direction.sqrMagnitude > .00000001f;
                var referenceMotion = simulation.SquadReferencePosition - sampledReference;
                referenceMotion.y = 0;
                // Local slot corrections still animate Run, but must not turn the character.
                // Include the finishing tick (and very short exchanges between render frames).
                if (simulation.ContinuousMovementEnabled && (simulation.IsFormationAdjusting || wasFormationAdjusting || formationFacingPending))
                    direction = referenceMotion;
                sampledPosition = view.State.Position;
                sampledReference = simulation.SquadReferencePosition;
                sampledTime = simulation.ElapsedTime;
                wasFormationAdjusting = simulation.IsFormationAdjusting;
                formationFacingPending = false;
            }
            if (view.IsRidingPlatform)
            { moving = view.IsPlatformRepositioning; direction = view.State.FormationOffset - view.PlatformRelativeOffset; }
            SetLocomotion(direction, moving, paused, Camera.main, session.EffectivePlaybackSpeed);
        }

        private void OnDestroy()
        {
            if (observedSimulation != null) observedSimulation.FormationChanged -= OnFormationChanged;
            if (worldZInstance != null) Destroy(worldZInstance);
            if (hairInstance != null) Destroy(hairInstance);
            if (faceHairInstance != null) Destroy(faceHairInstance);
        }
    }
}
