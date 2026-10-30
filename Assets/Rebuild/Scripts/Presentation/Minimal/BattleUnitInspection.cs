using System.Collections.Generic;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.Session;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation
{
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class BattleUnitInspection : MonoBehaviour
    {
        private BattlePresentation owner;
        private BattleSimulation selectedSimulation;
        private BattleSession inspectionSession;
        private UnitState selected;
        private UnitView selectedView;
        private Camera focusCamera;
        private Vector3 savedPosition, focusPoint;
        private Quaternion savedRotation;
        private float savedOrthoSize, focusDepth, focusOrthoSize;
        private bool squadSelected;
        private LegacyFormationEditor squadEditor;
        private UnitView highlightedCaptain;
        private Renderer[] highlightedRenderers;
        private MaterialPropertyBlock[] savedCaptainProperties;
        private MaterialPropertyBlock captainGlow;
        private bool hasCameraSnapshot, restoring;
        private GameObject uiRoot, ownedEventSystem;
        private RectTransform panel;
        private TMP_Text details, heading, hint;
        private LegacyUnitIconUI icon;
        private RectTransform unitSkills;
        private readonly Button[] skillButtons = new Button[3];
        private readonly TMP_Text[] skillLabels = new TMP_Text[3];
        private TMP_Text skillFeedback;
        private LegacyBattleHud battleHud;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        public UnitState SelectedUnit => selected;
        public TMP_Text DetailsLabel => details;
        public RectTransform Panel => panel;
        public bool IsSquadSelected => squadSelected;
        public bool IsInputSuppressed { get; set; }
        public UnitView HighlightedCaptain => highlightedCaptain;

        public void Initialize(BattlePresentation presentation) => owner = presentation;

        private void Update()
        {
            if (owner == null || !owner.isActiveAndEnabled || IsInputSuppressed) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseInspection();
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            var point = Mouse.current.position.ReadValue();
            if (PointerOverUI(point)) return;
            if (!TrySelectAt(point)) CloseInspection();
        }

        private bool PointerOverUI(Vector2 point)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, uiHits);
            return uiHits.Count > 0;
        }

        public bool TrySelectAt(Vector2 screenPoint)
        {
            var camera = Camera.main;
            var simulation = owner != null && owner.Session != null ? owner.Session.Simulation : null;
            if (camera == null || simulation == null || !owner.isActiveAndEnabled) return false;
            var ray = camera.ScreenPointToRay(screenPoint);
            var nearest = float.PositiveInfinity;
            var id = 0;
            foreach (var unit in simulation.Units)
            {
                if (!unit.IsAlive || !owner.TryGetView(unit.Id, out var view) || view == null || !view.TryGetVisibleBounds(out var bounds)) continue;
                if (!bounds.IntersectRay(ray, out var distance) || distance >= nearest) continue;
                nearest = distance; id = unit.Id;
            }
            return id != 0 && SelectUnit(id);
        }

        public bool SelectUnit(int memberId)
        {
            var camera = Camera.main;
            if (owner == null || !owner.isActiveAndEnabled || owner.Session == null || owner.Session.Simulation == null || camera == null ||
                !owner.TryGetView(memberId, out var view) || view == null || view.State == null || !view.State.IsAlive ||
                !view.TryGetVisibleBounds(out var bounds)) return false;
            EndSquadEdit();
            if (hasCameraSnapshot && focusCamera != camera) CloseImmediately();
            if (!hasCameraSnapshot)
            {
                focusCamera = camera;
                savedPosition = camera.transform.position;
                savedRotation = camera.transform.rotation;
                savedOrthoSize = camera.orthographicSize;
                hasCameraSnapshot = true;
            }
            selected = view.State; selectedView = view; selectedSimulation = owner.Session.Simulation;
            focusPoint = bounds.center;
            var originalDepth = Vector3.Dot(focusPoint - savedPosition, savedRotation * Vector3.forward);
            focusDepth = camera.orthographic ? Mathf.Max(originalDepth, camera.nearClipPlane * 2) :
                Mathf.Max(Mathf.Clamp(originalDepth * 0.35f, 4, 8) * 4f, camera.nearClipPlane * 2);
            focusOrthoSize = Mathf.Max(1.5f, savedOrthoSize * 0.4f) * 2f;
            restoring = false;
            EnsurePanel();
            skillFeedback.text = string.Empty;
            uiRoot.SetActive(true);
            details.gameObject.SetActive(true);
            hint.text = "Battle speed: 0.1x  |  Esc / X to close";
            icon.SetType(selected.Role);
            RefreshText();
            if (inspectionSession != null && inspectionSession != owner.Session) inspectionSession.SetUnitInspectionActive(false);
            inspectionSession = owner.Session;
            inspectionSession.SetUnitInspectionActive(true);
            return true;
        }

        public bool SelectSquad(LegacyBattleHud hud)
        {
            var camera = Camera.main;
            if (squadSelected) return true;
            if (owner == null || !owner.isActiveAndEnabled || owner.Session == null || hud == null || hud.Session != owner.Session ||
                !hud.CanEditFormation || camera == null || !TryGetSquadBounds(out var bounds)) return false;
            if (hasCameraSnapshot && focusCamera != camera) CloseImmediately();
            if (!hasCameraSnapshot)
            {
                focusCamera = camera; savedPosition = camera.transform.position; savedRotation = camera.transform.rotation;
                savedOrthoSize = camera.orthographicSize; hasCameraSnapshot = true;
            }
            selected = null; selectedView = null; selectedSimulation = owner.Session.Simulation;
            squadSelected = true; restoring = false; battleHud = hud;
            EnsurePanel(); uiRoot.SetActive(true);
            heading.text = "SQUAD 1";
            details.gameObject.SetActive(false); unitSkills.gameObject.SetActive(false);
            hint.text = "Battle paused  |  Esc / X to cancel";
            UnitState captain = null;
            foreach (var unit in selectedSimulation.Units)
            { if (captain == null) captain = unit; if (unit.Role == UnitRole.Captain) { captain = unit; break; } }
            icon.SetType(captain?.Role ?? UnitRole.Test);
            SetSquadFocus(bounds);
            UpdateCaptainHighlight();
            inspectionSession = owner.Session; inspectionSession.SetUnitInspectionActive(true);
            squadEditor = hud.OpenSquadEditorInPanel(panel);
            if (squadEditor != null) return true;
            CloseInspection(); return false;
        }

        public bool TryGetSquadBounds(out Bounds bounds)
        {
            bounds = default;
            if (owner == null || owner.Session == null || owner.Session.Simulation == null) return false;
            foreach (var unit in owner.Session.Simulation.Units)
            {
                if (!unit.IsAlive) continue;
                var anchor = unit.AnchorPosition;
                if (owner.TryGetView(unit.Id, out var view) && view != null) anchor = view.transform.position - unit.FormationOffset;
                // Include all nine slots, including empty/dead slots. The center is the formation anchor.
                bounds = new Bounds(anchor, new Vector3(3, 0.56f, 3));
                return true;
            }
            return false;
        }

        private void UpdateCaptainHighlight()
        {
            UnitView captain = null;
            if (owner != null && selectedSimulation != null)
                foreach (var unit in selectedSimulation.Units)
                    if (unit.IsAlive && unit.Role == UnitRole.Captain)
                    { owner.TryGetView(unit.Id, out captain); break; }
            if (captain != highlightedCaptain)
            {
                ClearCaptainHighlight();
                if (captain == null) return;
                highlightedCaptain = captain;
                var bodyParts = new List<Renderer>();
                foreach (var renderer in captain.GetComponentsInChildren<Renderer>())
                    if (renderer.name == "Body" || renderer.name == "Outline") bodyParts.Add(renderer);
                highlightedRenderers = bodyParts.ToArray();
                savedCaptainProperties = new MaterialPropertyBlock[highlightedRenderers.Length];
                for (var i = 0; i < highlightedRenderers.Length; i++)
                {
                    savedCaptainProperties[i] = new MaterialPropertyBlock();
                    highlightedRenderers[i].GetPropertyBlock(savedCaptainProperties[i]);
                }
            }
            if (captain == null) return;
            if (captainGlow == null) captainGlow = new MaterialPropertyBlock();
            // Bright gold pulses also work with the prototype's Unlit material, without Bloom.
            var pulse = (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f;
            var gold = Color.Lerp(new Color(1f, 0.64f, 0.08f), new Color(1f, 0.94f, 0.48f), pulse);
            foreach (var renderer in highlightedRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(captainGlow);
                captainGlow.SetColor("_BaseColor", gold);
                captainGlow.SetColor("_Color", gold);
                renderer.SetPropertyBlock(captainGlow);
            }
        }

        private void ClearCaptainHighlight()
        {
            if (highlightedRenderers != null)
                for (var i = 0; i < highlightedRenderers.Length; i++)
                    if (highlightedRenderers[i] != null) highlightedRenderers[i].SetPropertyBlock(savedCaptainProperties[i]);
            highlightedCaptain = null; highlightedRenderers = null; savedCaptainProperties = null;
        }

        private void SetSquadFocus(Bounds bounds)
        {
            focusPoint = bounds.center;
            var extent = Vector3.zero;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = Quaternion.Inverse(savedRotation) * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                extent = Vector3.Max(extent, new Vector3(Mathf.Abs(corner.x), Mathf.Abs(corner.y), Mathf.Abs(corner.z)));
            }
            var halfHeight = Mathf.Max(extent.y / 0.85f, extent.x / (focusCamera.aspect * 0.42f));
            focusOrthoSize = Mathf.Max(2, halfHeight);
            focusDepth = focusCamera.orthographic
                ? Mathf.Max(Vector3.Dot(focusPoint - savedPosition, savedRotation * Vector3.forward), focusCamera.nearClipPlane + extent.z + 1)
                : Mathf.Max(6, halfHeight / Mathf.Tan(focusCamera.fieldOfView * Mathf.Deg2Rad / 2) + extent.z,
                    focusCamera.nearClipPlane + extent.z + 1);
        }

        private void EndSquadEdit()
        {
            ClearCaptainHighlight();
            if (squadEditor != null && squadEditor.gameObject.activeSelf) squadEditor.CancelAndClose();
            squadEditor = null; squadSelected = false;
        }

        private void LateUpdate()
        {
            if (selected != null || squadSelected)
            {
                if (owner == null || owner.Session == null || owner.Session.Simulation != selectedSimulation || focusCamera == null)
                { CloseImmediately(); return; }
                if (squadSelected)
                {
                    if (squadEditor == null || !squadEditor.gameObject.activeSelf || !TryGetSquadBounds(out var squadBounds))
                    { CloseInspection(); return; }
                    SetSquadFocus(squadBounds);
                    UpdateCaptainHighlight();
                }
                else
                {
                    if (selectedView != null && selectedView.TryGetVisibleBounds(out var bounds)) focusPoint = bounds.center;
                    RefreshText();
                }
                var alpha = 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime);
                if (focusCamera.orthographic)
                    focusCamera.orthographicSize = Mathf.Lerp(focusCamera.orthographicSize, focusOrthoSize, alpha);
                // Place the model at the center of the right half of the actual Game View.
                var viewport = focusCamera.ScreenToViewportPoint(new Vector3(Screen.width * 0.75f, Screen.height * 0.5f, 0));
                var offset = focusCamera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, focusDepth)) - focusCamera.transform.position;
                focusCamera.transform.position = Vector3.Lerp(focusCamera.transform.position, focusPoint - offset, alpha);
            }
            else if (restoring && focusCamera != null)
            {
                var alpha = 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime);
                focusCamera.transform.position = Vector3.Lerp(focusCamera.transform.position, savedPosition, alpha);
                focusCamera.transform.rotation = Quaternion.Slerp(focusCamera.transform.rotation, savedRotation, alpha);
                focusCamera.orthographicSize = Mathf.Lerp(focusCamera.orthographicSize, savedOrthoSize, alpha);
                if (Vector3.Distance(focusCamera.transform.position, savedPosition) < 0.001f &&
                    Quaternion.Angle(focusCamera.transform.rotation, savedRotation) < 0.01f &&
                    Mathf.Abs(focusCamera.orthographicSize - savedOrthoSize) < 0.001f) RestoreCamera();
            }
        }

        private void RefreshText()
        {
            heading.text = $"{LegacyUnitLooks.Name(selected.Role)}  #{selected.MemberId}";
            details.text = UnitDetailsText.Format(selected);
            var isCaptain = selected.Role == UnitRole.Captain;
            unitSkills.gameObject.SetActive(true);
            details.rectTransform.offsetMin = new Vector2(32, 220);
            if (battleHud == null) battleHud = FindFirstObjectByType<LegacyBattleHud>();
            skillLabels[0].text = isCaptain ? "Edit\nFormation" : "Skill 1";
            skillButtons[0].targetGraphic.color = isCaptain ? new Color(0.56f, 0.42f, 0.20f) : LegacyUiKit.PanelLineColor;
            skillButtons[0].interactable = selected.IsAlive && (!isCaptain ||
                (battleHud != null && owner != null && battleHud.Session == owner.Session && battleHud.CanEditFormation));
            skillButtons[1].interactable = skillButtons[2].interactable = selected.IsAlive;
        }

        private void ClickUnitSkill(int index)
        {
            if (selected == null || !selected.IsAlive) return;
            if (index == 0 && selected.Role == UnitRole.Captain)
            {
                if (battleHud != null && owner != null && battleHud.Session == owner.Session && battleHud.CanEditFormation)
                { battleHud.OpenSquadEditor(); skillFeedback.text = "Edit Formation"; }
                return;
            }
            skillFeedback.text = $"Skill {index + 1}: not assigned yet";
        }

        public void CloseInspection()
        {
            EndSquadEdit();
            if (inspectionSession != null) inspectionSession.SetUnitInspectionActive(false);
            inspectionSession = null;
            selected = null; selectedView = null; selectedSimulation = null;
            if (uiRoot != null) uiRoot.SetActive(false);
            restoring = hasCameraSnapshot;
        }

        public void CloseImmediately() { CloseInspection(); RestoreCamera(); }
        private void RestoreCamera()
        {
            if (hasCameraSnapshot && focusCamera != null)
            {
                focusCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
                focusCamera.orthographicSize = savedOrthoSize;
            }
            hasCameraSnapshot = false; restoring = false; focusCamera = null;
        }

        private void EnsurePanel()
        {
            if (uiRoot != null) return;
            uiRoot = new GameObject("Unit Inspection UI", typeof(RectTransform));
            uiRoot.transform.SetParent(transform, false);
            LegacyUiKit.CreateCanvas(uiRoot, 100, new Vector2(1280, 720), 0.5f);
            var background = LegacyUiKit.CreateImage(uiRoot.transform, "Unit Details Panel", Vector2.zero, Vector2.zero,
                new Color(0.08f, 0.11f, 0.17f, 0.97f));
            background.raycastTarget = true;
            panel = background.rectTransform;
            panel.anchorMin = Vector2.zero; panel.anchorMax = new Vector2(0.5f, 1);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            heading = LegacyUiKit.CreateText(panel, "Heading", "", new Vector2(120, -45), new Vector2(380, 40),
                28, LegacyUiKit.TextColor, FontStyles.Bold);
            AnchorTopLeft(heading.rectTransform, new Vector2(120, -45));
            heading.alignment = TextAlignmentOptions.Left;
            icon = LegacyUnitIconUI.Create(panel, Vector2.zero, 110);
            icon.Rect.anchorMin = icon.Rect.anchorMax = new Vector2(0, 1);
            icon.Rect.anchoredPosition = new Vector2(65, -65);
            var close = LegacyUiKit.CreateButton(panel, "Close Details", Vector2.zero, new Vector2(48, 40), LegacyUiKit.PanelLineColor, 6);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-20, -20);
            LegacyUiKit.CreateText(close.transform, "Label", "X", Vector2.zero, new Vector2(48, 40), 24, Color.white);
            close.onClick.AddListener(CloseInspection);
            details = LegacyUiKit.CreateText(panel, "Live Stats", "", Vector2.zero, Vector2.zero, 22, LegacyUiKit.TextColor);
            details.alignment = TextAlignmentOptions.TopLeft;
            details.textWrappingMode = TextWrappingModes.Normal;
            details.rectTransform.anchorMin = Vector2.zero; details.rectTransform.anchorMax = Vector2.one;
            details.rectTransform.offsetMin = new Vector2(32, 60);
            details.rectTransform.offsetMax = new Vector2(-28, -135);
            details.lineSpacing = 12;
            unitSkills = LegacyUiKit.CreateRect(panel, "Unit Skills", Vector2.zero, new Vector2(336, 136));
            unitSkills.anchorMin = unitSkills.anchorMax = unitSkills.pivot = Vector2.zero;
            unitSkills.anchoredPosition = new Vector2(32, 80);
            for (var i = 0; i < skillButtons.Length; i++)
            {
                var index = i;
                var button = LegacyUiKit.CreateButton(unitSkills, "Unit Skill " + (i + 1), Vector2.zero,
                    new Vector2(96, 96), i == 0 ? new Color(0.56f, 0.42f, 0.20f) : LegacyUiKit.PanelLineColor, 0);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(i * 112, 36);
                var label = LegacyUiKit.CreateText(button.transform, "Label", i == 0 ? "Edit\nFormation" : "Skill " + (i + 1),
                    Vector2.zero, new Vector2(88, 88), 17, LegacyUiKit.TextColor, FontStyles.Bold);
                label.textWrappingMode = TextWrappingModes.Normal;
                skillLabels[i] = label;
                button.onClick.AddListener(() => ClickUnitSkill(index));
                skillButtons[i] = button;
            }
            skillFeedback = LegacyUiKit.CreateText(unitSkills, "Skill Feedback", "", Vector2.zero,
                new Vector2(336, 28), 15, LegacyUiKit.MutedColor);
            skillFeedback.rectTransform.anchorMin = skillFeedback.rectTransform.anchorMax = skillFeedback.rectTransform.pivot = Vector2.zero;
            skillFeedback.rectTransform.anchoredPosition = Vector2.zero;
            hint = LegacyUiKit.CreateText(panel, "Close Hint", "Battle speed: 0.1x  |  Esc / X to close", Vector2.zero,
                new Vector2(500, 28), 16, LegacyUiKit.MutedColor);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0);
            hint.rectTransform.anchoredPosition = new Vector2(0, 28);
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("Inspection EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(transform, false);
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private static void AnchorTopLeft(RectTransform rect, Vector2 point)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = point;
        }

        private void OnDisable()
        {
            CloseImmediately();
            if (uiRoot != null) { uiRoot.SetActive(false); Destroy(uiRoot); uiRoot = null; }
            if (ownedEventSystem != null) { ownedEventSystem.SetActive(false); Destroy(ownedEventSystem); ownedEventSystem = null; }
        }
    }
}
