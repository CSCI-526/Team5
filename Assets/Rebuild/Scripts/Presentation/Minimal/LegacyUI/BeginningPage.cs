using Team5.Rebuild.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class BeginningPage : MonoBehaviour
    {
        private RectTransform artwork;
        private RectTransform panel;
        private bool muted;
        private float volume = 1f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartFullscreen()
        {
            if (Application.isEditor || Application.isBatchMode) return;
            var display = Screen.currentResolution;
            Screen.SetResolution(display.width, display.height, FullScreenMode.MaximizedWindow);
        }
        private void Start()
        {
            foreach (var oldCanvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (oldCanvas.gameObject.scene == gameObject.scene) oldCanvas.gameObject.SetActive(false);
            var canvasRoot = new GameObject("Reverse TD Beginning", typeof(RectTransform));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = LegacyUiKit.CreateCanvas(canvasRoot, 100, new Vector2(1110, 630), .5f);
            var black = LegacyUiKit.CreateImage(canvas.transform, "Letterbox", Vector2.zero, Vector2.zero, Color.black);
            LegacyUiKit.Stretch(black.rectTransform);
            artwork = LegacyUiKit.CreateRect(canvas.transform, "Artwork and controls", Vector2.zero, new Vector2(1110, 630));
            var image = LegacyUiKit.CreateImage(artwork, "Beginning artwork", Vector2.zero, new Vector2(1110, 630), Color.white);
            var texture = Resources.Load<Texture2D>("Menu/Beginning");
            image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
            image.raycastTarget = false;
            Hotspot("Play", new Vector2(-3, -164), new Vector2(350, 92), BattleSceneFlow.BeginRun);
            Hotspot("Levels", new Vector2(-121, -255), new Vector2(204, 57), ShowLevels);
            Hotspot("Settings", new Vector2(116, -255), new Vector2(204, 57), ShowSettings);
            Hotspot("Sound", new Vector2(502, 275), new Vector2(62, 53), ToggleSound);
        }
        private void LateUpdate()
        {
            if (artwork == null) return;
            var parent = (RectTransform)artwork.parent;
            artwork.localScale = Vector3.one * Mathf.Min(parent.rect.width / 1110f, parent.rect.height / 630f);
        }
        private void Hotspot(string name, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var button = LegacyUiKit.CreateButton(artwork, name, position, size, new Color(1, 1, 1, .001f), 0);
            var colors = button.colors;
            colors.highlightedColor = new Color(1, 1, 1, .1f);
            button.colors = colors;
            button.onClick.AddListener(action);
        }
        private void OpenPanel(string title)
        {
            if (panel != null) Destroy(panel.gameObject);
            panel = LegacyUiKit.CreateImage(artwork, title, Vector2.zero, new Vector2(780, 540), new Color(.08f, .1f, .18f, .99f), 18).rectTransform;
            LegacyUiKit.CreateText(panel, "Title", title, new Vector2(0, 220), new Vector2(680, 50), 30, Color.white, FontStyles.Bold);
            Button("Back", new Vector2(0, -220), "BACK", () => Destroy(panel.gameObject));
        }
        private void Button(string name, Vector2 position, string label, UnityEngine.Events.UnityAction action)
        {
            var button = LegacyUiKit.CreateButton(panel, name, position, new Vector2(270, 45), LegacyUiKit.PanelLineColor, 8);
            LegacyUiKit.CreateText(button.transform, "Label", label, Vector2.zero, new Vector2(260, 40), 19, Color.white, FontStyles.Bold);
            button.onClick.AddListener(action);
        }
        private void ShowLevels()
        {
            OpenPanel("CAMPAIGN · 7 LEVELS");
            var text = "1   NORMAL  <color=#408CFF>Directional single target</color>\n\n" +
                "2   + LASER  <color=#FF3048>Warning line → piercing beam</color>\n\n" +
                "3   + SENTRY  <color=#AD4DFF>All directions, single target</color>\n\n" +
                "4   + FROST  <color=#2EC7FF>25% slow for 2 seconds</color>\n\n" +
                "5   + BLAST  <color=#FF6E14>Explosion in a 2x2 area</color>\n\n" +
                "6–7   Mixed defenses · use formation and legendary skills";
            var label = LegacyUiKit.CreateText(panel, "Campaign guide", text, new Vector2(0, 0), new Vector2(700, 365), 20, Color.white);
            label.alignment = TextAlignmentOptions.Left;
        }
        private void ShowSettings()
        {
            OpenPanel("SETTINGS");
            Button("Audio", new Vector2(0, 100), AudioListener.volume == 0 ? "SOUND: OFF" : "SOUND: ON", () => { ToggleSound(); ShowSettings(); });
            Button("Display", new Vector2(0, 30), "TOGGLE FULLSCREEN", () =>
            {
                if (Screen.fullScreen) Screen.fullScreenMode = FullScreenMode.Windowed;
                else
                {
                    var display = Screen.currentResolution;
                    Screen.SetResolution(display.width, display.height, FullScreenMode.MaximizedWindow);
                }
            });
            LegacyUiKit.CreateText(panel, "Hint", "Battle speed can be adjusted with the in-game slider.", new Vector2(0, -80), new Vector2(660, 70), 18, LegacyUiKit.MutedColor);
        }
        private void ToggleSound()
        {
            muted = AudioListener.volume > 0;
            if (muted) { volume = AudioListener.volume; AudioListener.volume = 0; }
            else AudioListener.volume = volume;
            var existing = artwork.Find("Muted badge");
            if (existing != null) Destroy(existing.gameObject);
            if (muted) LegacyUiKit.CreateText(artwork, "Muted badge", "OFF", new Vector2(502, 240), new Vector2(60, 20), 13, Color.white, FontStyles.Bold);
        }
    }
}
