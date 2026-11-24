using Team5.Rebuild.Presentation.LegacyUI;
using TMPro;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    public sealed class ContinuousMovementView : MonoBehaviour
    {
        private BattlePresentation owner;
        private GameObject ui;
        private TMP_Text hint;
        private LineRenderer marker;
        private Material material;
        private float invalidUntil;
        private BattleSimulation displayedSimulation;
        private bool Tactical => owner != null && owner.Session != null && owner.Session.UsesTacticalRoad;
        private string IdleHint => Tactical ? "Click to move  •  Off-road clicks snap to the road" : "Click ground to move  •  Inspect units for abilities";
        public void Bind(BattlePresentation presentation)
        {
            owner = presentation;
            if (ui != null) return;
            ui = new GameObject("Free movement hints", typeof(RectTransform)); ui.transform.SetParent(transform, false);
            LegacyUiKit.CreateCanvas(ui, 10, new Vector2(1280, 720), .5f);
            hint = LegacyUiKit.CreateText(ui.transform, "Instructions", IdleHint,
                new Vector2(0, -325), new Vector2(1100, 35), 16, Color.white);
            hint.raycastTarget = false;
            marker = new GameObject("Move destination").AddComponent<LineRenderer>();
            marker.transform.SetParent(transform, false); marker.useWorldSpace = true; marker.loop = true;
            marker.widthMultiplier = .035f; marker.positionCount = 32;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); marker.sharedMaterial = material;
            marker.enabled = false;
        }
        public void ShowDestination(Vector3 point, bool accepted)
        {
            if (marker == null || hint == null || owner == null || owner.Session == null) return;
            displayedSimulation = owner.Session.Simulation;
            var snapped = accepted && displayedSimulation != null &&
                (point - displayedSimulation.SquadDestination).sqrMagnitude > .04f;
            if (accepted && displayedSimulation != null) point = displayedSimulation.SquadDestination;
            invalidUntil = accepted ? 0 : Time.unscaledTime + 1.5f;
            hint.text = accepted ? (Tactical ? (snapped ? "Moving to nearest reachable road position" : "Moving — click to change destination") : "Moving — click ground to change destination") : (Tactical ? "No reachable road position — try another spot" : "Cannot reach that position with the squad");
            material.SetColor("_BaseColor", accepted ? new Color(.15f, 1, .65f) : Color.red);
            for (var i = 0; i < 32; i++)
            {
                var angle = i * Mathf.PI / 16;
                marker.SetPosition(i, point + new Vector3(Mathf.Cos(angle) * .22f, .035f, Mathf.Sin(angle) * .22f));
            }
            marker.enabled = true;
        }
        private void Update()
        {
            if (marker == null || hint == null) return;
            var simulation = owner != null && owner.Session != null ? owner.Session.Simulation : null;
            if (simulation == null || simulation.Outcome != BattleOutcome.Running || simulation != displayedSimulation)
            {
                marker.enabled = false;
                invalidUntil = 0;
                displayedSimulation = simulation;
                hint.text = IdleHint;
                return;
            }
            if (invalidUntil > Time.unscaledTime) return;
            if (invalidUntil > 0)
            {
                marker.enabled = false; invalidUntil = 0;
                hint.text = owner.Session.Simulation.IsSquadMoving ? (Tactical ? "Following previous destination — click the road to change it" : "Following previous destination — click ground to change it") :
                    IdleHint;
            }
            if (!owner.Session.Simulation.IsSquadMoving)
            {
                marker.enabled = false;
                hint.text = IdleHint;
            }
        }
        private void OnEnable() { if (ui != null) ui.SetActive(true); }
        private void OnDisable() { if (ui != null) ui.SetActive(false); if (marker != null) marker.enabled = false; }
        private void OnDestroy()
        { if (ui != null) Destroy(ui); if (marker != null) Destroy(marker.gameObject); if (material != null) Destroy(material); }
    }
}
