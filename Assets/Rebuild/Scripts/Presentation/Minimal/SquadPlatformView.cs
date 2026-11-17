using System.Collections;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    // Scene opt-in: fixed-grid platform transport, separate from continuous free-squad movement.
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class SquadPlatformView : MonoBehaviour
    {
        [SerializeField] private BattlePresentation owner;
        [SerializeField] private Transform model;
        [SerializeField] private Transform wheelRig;
        [SerializeField] private Transform[] rollingWheels = new Transform[0];
        public const float DeckThickness = BattleCell.LayerHeight;
        public const float BodyLift = .32f;
        public const float WheelRadius = .18f;
        public const float DeckHeight = DeckThickness + BodyLift;
        private BattleSimulation sampledSimulation;
        private Vector3 lastAnchor;
        private float wheelAngle;
        private bool hasAnchor;
        public bool IsPlatformMode => isActiveAndEnabled && model != null && owner != null &&
            owner.Session != null && !owner.Session.ContinuousSquadMovement;
        public Vector3 RenderAnchor => model.position;
        public Transform Model => model;
        public float PlaybackSpeed => owner.Session.Simulation != null && !owner.Session.Simulation.IsPaused &&
            owner.Session.Simulation.Outcome == BattleOutcome.Running && owner.Session.isActiveAndEnabled
            ? owner.Session.EffectivePlaybackSpeed : 0;

        private void OnEnable()
        {
            if (model != null) model.gameObject.SetActive(IsPlatformMode);
        }

        // Finish just the approved formation edit while tutorial-owned battle time stays frozen.
        // No persistent override flag: skipping/cancelling the tutorial immediately stops this work.
        public IEnumerator CompleteTutorialReposition()
        {
            var sim = owner.Session.Simulation;
            while (IsPlatformMode && owner.Session.Simulation == sim && sim != null && sim.Outcome == BattleOutcome.Running)
            {
                var pending = false;
                foreach (var unit in sim.Units)
                    if (unit.IsAlive && owner.TryGetView(unit.Id, out var view) && view.IsPlatformRepositioning)
                    {
                        pending = true;
                        if (sim.IsPaused) view.AdvancePlatformReposition(Time.unscaledDeltaTime);
                    }
                if (!pending) yield break;
                yield return null;
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || !IsPlatformMode) return;
            var sim = owner.Session.Simulation;
            if (sim == null) return;
            if (sampledSimulation != sim) { sampledSimulation = sim; hasAnchor = false; wheelAngle = 0; }
            foreach (var unit in sim.Units)
                if (unit.IsAlive && owner.TryGetView(unit.Id, out var view))
                {
                    var anchor = hasAnchor && sim.IsPaused ? lastAnchor : view.InterpolatedAnchor;
                    var travel = anchor - lastAnchor; travel.y = 0;
                    if (hasAnchor && !sim.IsPaused && wheelRig != null && travel.sqrMagnitude > .000001f)
                    {
                        wheelRig.localRotation = Quaternion.Euler(0, Mathf.Atan2(-travel.z, travel.x) * Mathf.Rad2Deg, 0);
                        wheelAngle = (wheelAngle - travel.magnitude / WheelRadius * Mathf.Rad2Deg) % 360;
                        foreach (var wheel in rollingWheels)
                            if (wheel != null) wheel.localRotation = Quaternion.AngleAxis(wheelAngle, Vector3.forward);
                    }
                    lastAnchor = anchor; hasAnchor = true;
                    model.position = anchor;
                    model.rotation = Quaternion.identity;
                    break;
                }
        }

        private void OnDisable()
        {
            if (model != null) model.gameObject.SetActive(false);
        }
    }
}
