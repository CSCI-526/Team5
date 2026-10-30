using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Team5's authored HUD is preserved in the scene. This replaces only its owner bindings.
    public sealed class LegacyBattleHud : MonoBehaviour
    {
        [SerializeField] private BattleSession session;
        [SerializeField] private Slider speedSlider;
        [SerializeField] private Button editButton;
        private TextMeshProUGUI levelLabel, livesLabel;
        private Canvas canvas;
        private TMP_FontAsset hudFont;
        private BattleCombatControls debugControls;
        private bool previousDebugPanel;
        private bool previousEditButtonActive;
        private GameObject ownedEventSystem;
        private LegacyFormationEditor editor;
        private BattleSimulation bound;
        private LegacyHealthBars healthBars;
        private Button pauseButton;
        private TMP_Text pauseLabel;
        private readonly Button[] squadButtons = new Button[3];
        private LegacyUnitIconUI squadIcon;
        private BattleUnitInspection inspection;
        private bool HasOpenEditor => editor != null && editor.gameObject.activeSelf;
        public Button SquadButton(int index) => squadButtons[index];
        public BattleSession Session => session;
        public Slider SpeedSlider => speedSlider;
        public Button EditButton => editButton;
        public Button PauseButton => pauseButton;
        public bool TutorialOwnsPause { get; set; }
        public TMP_Text LevelLabel => levelLabel;
        public TMP_Text LivesLabel => livesLabel;
        public LegacyFormationEditor FormationEditor => editor;
        public bool CanEditFormation => isActiveAndEnabled && bound != null && bound.Outcome == BattleOutcome.Running &&
            bound.AliveCount > 0 && bound.PendingSpawnCount == 0 && !HasOpenEditor;

        private void OnEnable()
        {
            if (BattleSceneFlow.IsPreviewScene(gameObject.scene)) return;
            canvas = GetComponent<Canvas>(); canvas.enabled = true;
            if (session == null || speedSlider == null)
            { Debug.LogError("Legacy HUD needs its scene bindings.", this); return; }
            var authoredText = editButton != null ? editButton.GetComponentInChildren<TMP_Text>(true) : GetComponentInChildren<TMP_Text>(true);
            hudFont = authoredText != null && authoredText.font != null ? authoredText.font : TMP_Settings.defaultFontAsset;
            if (editButton != null)
            {
                previousEditButtonActive = editButton.gameObject.activeSelf;
                editButton.gameObject.SetActive(false); // Optional legacy entry; squad/skill buttons now handle editing.
            }
            debugControls = session.GetComponent<BattleCombatControls>();
            if (debugControls != null) { previousDebugPanel = debugControls.ShowDebugPanel; debugControls.ShowDebugPanel = false; }
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("Battle UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(session.transform, false);
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            EnsureLevelLabel();
            EnsurePauseButton();
            EnsureSquadButtons();
            var barsRoot = LegacyUiKit.CreateRect(transform, "World Health Bars", Vector2.zero, Vector2.zero);
            LegacyUiKit.Stretch(barsRoot);
            barsRoot.pivot = Vector2.zero;
            barsRoot.SetAsFirstSibling();
            healthBars = barsRoot.gameObject.AddComponent<LegacyHealthBars>();
            session.SimulationCreated += OnSimulationCreated;
            OnSimulationCreated(session.Simulation);
            Refresh();
        }
        private void OnSimulationCreated(BattleSimulation next)
        {
            if (editor != null) { editor.gameObject.SetActive(false); Destroy(editor.gameObject); editor = null; }
            bound = next;
            if (healthBars != null) healthBars.Bind(session, next);
            speedSlider.SetValueWithoutNotify(session.PlaybackSpeed - 1);
        }
        private GameObject recoveryPanel;
        private void Update()
        {
            Refresh();
            if (session != null && session.RecoveryRequired && recoveryPanel == null)
                recoveryPanel = LegacyLevelOneRecovery.Open(session);
        }
        private void Refresh()
        {
            if (session == null || levelLabel == null) return;
            levelLabel.text = $"LEVEL {Mathf.Max(1, session.DisplayedLevelNumber)}";
            livesLabel.text = LegacyUiKit.LifeCounterText(session.AttemptsRemaining, session.AttemptsPerLevel);
            if (editButton != null) editButton.interactable = CanEditFormation;
            speedSlider.interactable = bound != null && bound.Outcome == BattleOutcome.Running && !HasOpenEditor && !session.IsUnitInspectionActive && !TutorialOwnsPause;
            pauseButton.interactable = bound != null && bound.Outcome == BattleOutcome.Running && !HasOpenEditor && !TutorialOwnsPause;
            pauseLabel.text = bound != null && bound.IsPaused ? "Resume" : "Pause";
            squadButtons[0].interactable = CanEditFormation || (inspection != null && inspection.IsSquadSelected);
            UnitState captain = null;
            if (bound != null)
                foreach (var unit in bound.Units)
                { if (captain == null) captain = unit; if (unit.Role == UnitRole.Captain) { captain = unit; break; } }
            squadIcon.SetType(captain?.Role ?? UnitRole.Test);
            squadButtons[0].targetGraphic.color = inspection != null && inspection.IsSquadSelected ? LegacyUiKit.NodeBlueColor : LegacyUiKit.PanelLineColor;
        }
        private void EnsureSquadButtons()
        {
            if (squadButtons[0] != null) { foreach (var button in squadButtons) button.gameObject.SetActive(true); return; }
            var scaler = canvas.GetComponent<CanvasScaler>();
            var scale = scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize ? scaler.referenceResolution.x / 1280f : 1f;
            for (var i = 0; i < squadButtons.Length; i++)
            {
                var button = LegacyUiKit.CreateButton(transform, "Squad Selection " + (i + 1), Vector2.zero,
                    Vector2.one * 56 * scale, LegacyUiKit.PanelLineColor, 0);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(24, -124 - i * 68) * scale;
                button.interactable = false;
                squadButtons[i] = button;
                if (i == 0)
                {
                    squadIcon = LegacyUnitIconUI.Create(rect, Vector2.zero, 110 * scale);
                    squadIcon.Rect.anchoredPosition = new Vector2(28, -28) * scale;
                    squadIcon.Rect.anchorMin = squadIcon.Rect.anchorMax = new Vector2(0, 1);
                    button.onClick.AddListener(SelectSquad);
                }
            }
        }
        public void SelectSquad()
        {
            if (inspection == null)
                foreach (var view in FindObjectsByType<BattlePresentation>(FindObjectsSortMode.None))
                    if (view.Session == session) { inspection = view.GetComponent<BattleUnitInspection>(); break; }
            if (inspection == null) return;
            if (inspection.IsSquadSelected) inspection.CloseInspection();
            else inspection.SelectSquad(this);
            Refresh();
        }
        private void EnsurePauseButton()
        {
            if (pauseButton == null)
            {
                pauseButton = LegacyUiKit.CreateButton(speedSlider.transform, "Pause Battle", Vector2.zero,
                    new Vector2(80, 22), LegacyUiKit.PanelLineColor, 3);
                var rect = (RectTransform)pauseButton.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(0, 30);
                pauseLabel = LegacyUiKit.CreateText(rect, "Label", "Pause", Vector2.zero,
                    new Vector2(76, 20), 12, Color.white);
                pauseLabel.font = hudFont;
                pauseButton.onClick.AddListener(TogglePause);
            }
            pauseButton.gameObject.SetActive(true);
        }
        public void TogglePause()
        {
            if (bound == null || bound.Outcome != BattleOutcome.Running || HasOpenEditor || TutorialOwnsPause) return;
            bound.SetPaused(!bound.IsPaused);
            Refresh();
        }
        // Same method names as old persistent UnityEvents; only the receiver changes.
        public void SetSpeedFromSlider(float value)
        {
            var index = Mathf.RoundToInt(value);
            session.SetPlaybackSpeed(Mathf.Clamp(index + 1, 1, 3));
        }
        public void OpenSquadEditor()
        {
            if (!CanEditFormation) return;
            editor = LegacyFormationEditor.Open(this, bound); Refresh();
        }
        public LegacyFormationEditor OpenSquadEditorInPanel(RectTransform panel)
        {
            if (!CanEditFormation) return null;
            editor = LegacyFormationEditor.Open(this, bound, panel); Refresh();
            return editor;
        }
        private void EnsureLevelLabel()
        {
            var existing = transform.Find("Level Label");
            if (existing != null)
            {
                levelLabel = existing.GetComponent<TextMeshProUGUI>();
                livesLabel = transform.Find("Death Count").GetComponent<TextMeshProUGUI>(); return;
            }
            // Unchanged layout/font calculation from OffenseWaveManager.EnsureLevelLabel.
            var scaler = canvas.GetComponent<CanvasScaler>();
            var uiScale = scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize ? scaler.referenceResolution.x / 1280f : 1;
            levelLabel = CreateCornerLabel("Level Label", new Vector2(24, -24) * uiScale, new Vector2(240, 60) * uiScale, 36 * uiScale, FontStyles.Bold);
            livesLabel = CreateCornerLabel("Death Count", new Vector2(56, -76) * uiScale, new Vector2(208, 40) * uiScale, 24 * uiScale, FontStyles.Normal);
            LegacyUiKit.CreateHeartIcon(transform, new Vector2(24, -80) * uiScale, uiScale);
        }
        private TextMeshProUGUI CreateCornerLabel(string name, Vector2 position, Vector2 size, float fontSize, FontStyles style)
        {
            var label = LegacyUiKit.CreateText(transform, name, "", position, size, fontSize, Color.white, style);
            label.font = hudFont;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0, 1);
            label.rectTransform.anchoredPosition = position;
            label.alignment = TextAlignmentOptions.TopLeft;
            return label;
        }
        private void OnDisable()
        {
            if (recoveryPanel != null) { recoveryPanel.SetActive(false); Destroy(recoveryPanel); recoveryPanel = null; }
            if (inspection != null && inspection.IsSquadSelected) inspection.CloseImmediately();
            foreach (var button in squadButtons) if (button != null) button.gameObject.SetActive(false);
            if (pauseButton != null) pauseButton.gameObject.SetActive(false);
            if (editButton != null) editButton.gameObject.SetActive(previousEditButtonActive);
            if (session != null) session.SimulationCreated -= OnSimulationCreated;
            if (canvas != null) canvas.enabled = false;
            if (debugControls != null) debugControls.ShowDebugPanel = previousDebugPanel;
            if (editor != null) { editor.gameObject.SetActive(false); Destroy(editor.gameObject); editor = null; }
            if (ownedEventSystem != null) { ownedEventSystem.SetActive(false); Destroy(ownedEventSystem); ownedEventSystem = null; }
            if (healthBars != null) { healthBars.gameObject.SetActive(false); Destroy(healthBars.gameObject); healthBars = null; }
            bound = null;
        }
    }
}
