using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReverseTD.Shared
{
    // Self-bootstrapping "How to Play" overlay. Nothing needs to be placed in a scene:
    // on every scene load it builds its own canvases in code for the scenes listed below.
    //  - StartGame: a "How to Play" button under Start Game, plus the modal auto-opens on first visit.
    //  - UpgradeScene: a "? How to Play" button in the top-left corner, plus the modal auto-opens on first visit.
    // "First visit" is tracked with a PlayerPrefs key. OffenseData.ResetGame wipes PlayerPrefs on
    // Play Again, so a fresh run shows the rules again.
    public class HowToPlay : MonoBehaviour
    {
        private const string TitleSceneName = "StartGame";
        private const string UpgradeSceneName = "UpgradeScene";
        private const string SeenPrefsKey = "HowToPlay_Seen";

        // Above the scene canvases (which all use sortingOrder 0).
        private const int SceneButtonSortingOrder = 500;
        private const int ModalSortingOrder = 1000;

        // Scene canvases use ScaleWithScreenSize, match width (0). Mirror them so our
        // buttons line up with the existing layout.
        private static readonly Vector2 TitleReferenceResolution = new Vector2(500f, 500f);
        private static readonly Vector2 UpgradeReferenceResolution = new Vector2(1280f, 720f);
        private const float SceneMatchWidthOrHeight = 0f;

        // The modal has its own fixed reference so it looks the same in every scene.
        // Matching halfway between width and height keeps the card on screen for wide and narrow landscape windows.
        private static readonly Vector2 ModalReferenceResolution = new Vector2(1280f, 720f);
        private const float ModalMatchWidthOrHeight = 0.5f;
        private static readonly Vector2 CardSize = new Vector2(880f, 580f);

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color CardColor = new Color(0.11f, 0.14f, 0.21f, 0.98f);
        private static readonly Color HeadingColor = Color.white;
        private static readonly Color BodyColor = new Color(0.9f, 0.92f, 0.95f, 1f);
        private static readonly Color AccentColor = new Color(0.2f, 0.6f, 0.2f, 1f);
        private static readonly Color LightButtonColor = Color.white;
        private static readonly Color DarkTextColor = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);

        private const string RulesText =
            "You are the <b>ATTACKER</b>. The towers defend the path; your soldiers try to break through.\n" +
            "<b>1. Upgrade:</b> spend $ in the upgrade tree (<color=#4CC24C>green</color> = affordable, <color=#E0503A>red</color> = not enough $). Each box is an upgrade you can buy several times (Lv shows how many). Your first purchase opens three new boxes: a bigger damage boost, more soldier HP, and an extra soldier per wave.\n" +
            "<b>2. Start Wave:</b> your soldiers march down the path and automatically shoot towers in range.\n" +
            "<b>3. Earn:</b> every point of damage to a tower gives you $1. When your whole wave falls, you return here to upgrade.\n" +
            "<b>4. Win:</b> get a single soldier to the end of the path.\n" +
            "<i>Tip: use the speed slider during a wave to play at 2x or 4x.</i>";

        private static HowToPlay current;

        private GameObject modalRoot;
        private Button closeButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Safety net in case the first scene's sceneLoaded fired before Bootstrap subscribed.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapFirstScene()
        {
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool isTitle = scene.name == TitleSceneName;
            bool isUpgrade = scene.name == UpgradeSceneName;
            if (!isTitle && !isUpgrade)
                return;

            if (current != null && current.gameObject.scene == scene)
                return;

            EnsureEventSystem(scene);

            GameObject root = new GameObject("HowToPlay");
            SceneManager.MoveGameObjectToScene(root, scene);
            current = root.AddComponent<HowToPlay>();
            current.Build(isTitle);

            if (PlayerPrefs.GetInt(SeenPrefsKey, 0) == 0)
                current.Show();
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            // Project uses the new Input System only, so the legacy StandaloneInputModule would not work.
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        private void OnDestroy()
        {
            if (current == this)
                current = null;
        }

        public void Show()
        {
            if (modalRoot == null)
                return;

            modalRoot.SetActive(true);

            if (EventSystem.current != null && closeButton != null)
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        public void Hide()
        {
            if (modalRoot == null)
                return;

            if (EventSystem.current != null && closeButton != null
                && EventSystem.current.currentSelectedGameObject == closeButton.gameObject)
                EventSystem.current.SetSelectedGameObject(null);

            modalRoot.SetActive(false);

            PlayerPrefs.SetInt(SeenPrefsKey, 1);
            PlayerPrefs.Save();
        }

        private void Build(bool isTitle)
        {
            if (isTitle)
                BuildTitleButton();
            else
                BuildUpgradeButton();

            BuildModal();
            modalRoot.SetActive(false);
        }

        private void BuildTitleButton()
        {
            Canvas canvas = CreateCanvas("HowToPlayButtonCanvas", TitleReferenceResolution, SceneMatchWidthOrHeight, SceneButtonSortingOrder);

            // Start Game sits at (-2, -32.9), 200x70, center anchored. Put this just below it, same x.
            Button button = CreateButton(canvas.transform, "HowToPlayButton", "How to Play", LightButtonColor, DarkTextColor, 20f);
            SetRect((RectTransform)button.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-2f, -100f), new Vector2(150f, 36f));
            button.onClick.AddListener(Show);
        }

        private void BuildUpgradeButton()
        {
            Canvas canvas = CreateCanvas("HowToPlayButtonCanvas", UpgradeReferenceResolution, SceneMatchWidthOrHeight, SceneButtonSortingOrder);

            // Top-left corner is empty: gold is top-right, Start Wave is bottom-right, the tree is centered left.
            Button button = CreateButton(canvas.transform, "HowToPlayButton", "? How to Play", LightButtonColor, DarkTextColor, 22f);
            SetRect((RectTransform)button.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -20f), new Vector2(180f, 44f));
            button.onClick.AddListener(Show);
        }

        private void BuildModal()
        {
            Canvas canvas = CreateCanvas("HowToPlayModalCanvas", ModalReferenceResolution, ModalMatchWidthOrHeight, ModalSortingOrder);
            modalRoot = canvas.gameObject;

            // Full-screen backdrop: dims the scene and swallows clicks so nothing behind is clickable.
            Image backdrop = CreateImage(canvas.transform, "Backdrop", BackdropColor);
            backdrop.raycastTarget = true;
            Stretch((RectTransform)backdrop.transform);

            Image card = CreateImage(backdrop.transform, "Card", CardColor);
            card.raycastTarget = true;
            SetRect((RectTransform)card.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, CardSize);

            TextMeshProUGUI heading = CreateText(card.transform, "Heading", "How to Play", 44f, HeadingColor, TextAlignmentOptions.Center);
            heading.fontStyle = FontStyles.Bold;
            SetRect(heading.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(-80f, 64f));

            TextMeshProUGUI body = CreateText(card.transform, "Rules", RulesText, 26f, BodyColor, TextAlignmentOptions.TopLeft);
            body.enableAutoSizing = true;
            body.fontSizeMin = 14f;
            body.fontSizeMax = 26f;
            body.paragraphSpacing = 10f;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.offsetMin = new Vector2(48f, 108f);
            bodyRect.offsetMax = new Vector2(-48f, -100f);

            closeButton = CreateButton(card.transform, "GotItButton", "Got it", AccentColor, Color.white, 28f);
            SetRect((RectTransform)closeButton.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(220f, 58f));
            closeButton.onClick.AddListener(Hide);
        }

        private Canvas CreateCanvas(string name, Vector2 referenceResolution, float matchWidthOrHeight, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(transform, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = matchWidthOrHeight;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(parent, name);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();

            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;

            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.richText = true;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(Transform parent, string name, string labelText, Color background, Color textColor, float fontSize)
        {
            Image image = CreateImage(parent, name, background);
            image.raycastTarget = true;

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI label = CreateText(image.transform, "Label", labelText, fontSize, textColor, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = fontSize;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(label.rectTransform);
            label.margin = new Vector4(6f, 2f, 6f, 2f);

            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
