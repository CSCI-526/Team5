using Team5.BasicTowerTrial.Presentation.LegacyUI;
using TMPro;
using UnityEngine;

namespace Team5.BasicTowerTrial.Presentation
{
    public sealed class ContinuousMovementView : MonoBehaviour
    {
        private BattlePresentation owner;
        private GameObject ui;
        private TMP_Text hint;
        private LineRenderer marker;
        private Material material;
        private float invalidUntil;
        public void Bind(BattlePresentation presentation)
        {
            owner = presentation;
            if (ui != null) return;
            ui = new GameObject("Free movement hints", typeof(RectTransform)); ui.transform.SetParent(transform, false);
            LegacyUiKit.CreateCanvas(ui, 10, new Vector2(1280, 720), .5f);
            hint = LegacyUiKit.CreateText(ui.transform, "Instructions", "Click ground to move | Click a unit to inspect | Squad icon: edit formation",
                new Vector2(0, -325), new Vector2(1180, 35), 17, Color.white);
            hint.raycastTarget = false;
            marker = new GameObject("Move destination").AddComponent<LineRenderer>();
            marker.transform.SetParent(transform, false); marker.useWorldSpace = true; marker.loop = true;
            marker.widthMultiplier = .035f; marker.positionCount = 32;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); marker.sharedMaterial = material;
            marker.enabled = false;
        }
        public void ShowDestination(Vector3 point, bool accepted)
        {
            invalidUntil = accepted ? 0 : Time.unscaledTime + 1.5f;
            hint.text = accepted ? "Moving — click ground to change destination" : "Cannot reach that position with the squad";
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
            if (owner == null || owner.Session.Simulation == null) return;
            if (invalidUntil > Time.unscaledTime) return;
            if (invalidUntil > 0)
            {
                marker.enabled = false; invalidUntil = 0;
                hint.text = owner.Session.Simulation.IsSquadMoving ? "Following previous destination — click ground to change it" :
                    "Click ground to move | Click a unit to inspect | Squad icon: edit formation";
            }
            if (!owner.Session.Simulation.IsSquadMoving)
            {
                marker.enabled = false;
                hint.text = "Click ground to move | Click a unit to inspect | Squad icon: edit formation";
            }
        }
        private void OnEnable() { if (ui != null) ui.SetActive(true); }
        private void OnDisable() { if (ui != null) ui.SetActive(false); if (marker != null) marker.enabled = false; }
        private void OnDestroy()
        { if (ui != null) Destroy(ui); if (marker != null) Destroy(marker.gameObject); if (material != null) Destroy(material); }
    }
}
