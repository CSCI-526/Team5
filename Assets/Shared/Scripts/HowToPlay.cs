using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ReverseTD.Defense;
using ReverseTD.Offense;

namespace ReverseTD.Shared
{
    public class HowToPlay : MonoBehaviour
    {
        private const string TitleSceneName = "StartGame";
        private const string UpgradeSceneName = "UpgradeScene";
        private const string GameplaySceneName = "DefenseSandbox";
        private const string SeenPrefsKey = "HowToPlay_Seen";
        private const string TutorialPrefsKey = "InteractiveTutorial_Done";

        private const int SceneButtonSortingOrder = 500;
        private const int ModalSortingOrder = 1000;

        private static readonly Vector2 TitleReferenceResolution = new Vector2(500f, 500f);
        private static readonly Vector2 UpgradeReferenceResolution = new Vector2(1280f, 720f);
        private const float SceneMatchWidthOrHeight = 0f;

        private static readonly Vector2 ModalReferenceResolution = new Vector2(1280f, 720f);
        private const float ModalMatchWidthOrHeight = 0.5f;
        private static readonly Vector2 CardSize = new Vector2(760f, 440f);

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color CardColor = new Color(0.11f, 0.14f, 0.21f, 0.98f);
        private static readonly Color HeadingColor = Color.white;
        private static readonly Color BodyColor = new Color(0.9f, 0.92f, 0.95f, 1f);
        private static readonly Color AccentColor = new Color(0.2f, 0.6f, 0.2f, 1f);
        private static readonly Color LightButtonColor = Color.white;
        private static readonly Color DarkTextColor = new Color(0.196f, 0.196f, 0.196f, 1f);

        // Short, punchy rules text (solves "Too long how to play, too much text")
        private const string RulesText =
            "• <b>YOU ARE THE ATTACKER: </b> March past towers trying to stop you.\n" +
            "• <b>EARN GOLD ($):</b> Every 1 damage your soldiers deal to towers = <b>+$1 Gold</b> (Top Right).\n" +
            "• <b>UPGRADE:</b> When your squad dies, spend Gold on <color=#4CC24C>Green</color> upgrades and pick new units from cards.\n" +
            "• <b>WIN:</b> Get <b>1 soldier</b> to the end of the path!";

        private static HowToPlay current;

        // Modal UI (StartGame / UpgradeScene)
        private GameObject modalRoot;
        private Button closeButton;

        // Interactive Freeze-Frame Tutorial (DefenseSandbox)
        private Camera mainCam;
        private BoardCamera boardCamera;
        private Vector3 defaultCamPos;
        private float defaultCamSize;
        private Vector3 targetCamPos;
        private float targetCamSize;
        private bool isControllingCamera;

        private GameObject tutorialCanvasObj;
        private GameObject bannerPanel;
        private TextMeshProUGUI bannerTitle;
        private TextMeshProUGUI bannerBody;
        private TextMeshProUGUI bannerPrompt;
        private GameObject goldCalloutBox;
        private GameObject worldMarkerRing;
        private bool userClickedNext;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapFirstScene()
        {
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool isTitle = scene.name == TitleSceneName;
            bool isUpgrade = scene.name == UpgradeSceneName;
            bool isGameplay = scene.name == GameplaySceneName;

            if (!isTitle && !isUpgrade && !isGameplay)
                return;

            if (current != null && current.gameObject.scene == scene)
                return;

            EnsureEventSystem(scene);

            GameObject root = new GameObject("HowToPlay");
            SceneManager.MoveGameObjectToScene(root, scene);
            current = root.AddComponent<HowToPlay>();

            if (isGameplay)
            {
                if (PlayerPrefs.GetInt(TutorialPrefsKey, 0) == 0)
                {
                    current.StartInteractiveTutorial();
                }
            }
            else
            {
                current.Build(isTitle);
            }
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        private void OnDestroy()
        {
            if (isControllingCamera)
            {
                ReleaseCameraControl();
            }
            OffenseWaveManager.IsTutorialPaused = false;

            if (current == this)
                current = null;
        }

        private void LateUpdate()
        {
            // Check for click or Spacebar during freeze-frame tutorial
            if (bannerPanel != null && bannerPanel.activeSelf)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    userClickedNext = true;
                else if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                    userClickedNext = true;
            }

            // Pulse the top-right gold callout box
            if (goldCalloutBox != null && goldCalloutBox.activeSelf)
            {
                float s = 1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.06f;
                goldCalloutBox.transform.localScale = new Vector3(s, s, 1f);
            }

            // Pulse the world-space highlight ring if active
            if (worldMarkerRing != null && worldMarkerRing.activeSelf)
            {
                float r = 1.15f + Mathf.Sin(Time.unscaledTime * 7f) * 0.12f;
                worldMarkerRing.transform.localScale = new Vector3(r, r, 1f);
            }

            // Smoothly pan & zoom camera using unscaledDeltaTime while frozen
            if (isControllingCamera && mainCam != null)
            {
                float t = 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime);
                mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, targetCamPos, t);
                mainCam.orthographicSize = Mathf.Lerp(mainCam.orthographicSize, targetCamSize, t);
            }
        }

        // =====================================================================
        // INTERACTIVE FREEZE-FRAME TUTORIAL (DefenseSandbox - Wave 1)
        // =====================================================================

        private void StartInteractiveTutorial()
        {
            BuildFreezeFrameUI();
            StartCoroutine(RunFreezeFrameSequence());
        }

        private IEnumerator RunFreezeFrameSequence()
        {
            // Wait 2 frames so BoardCamera.Start() finishes fitting the board
            yield return null;
            yield return null;

            mainCam = Camera.main;
            if (mainCam == null) yield break;

            boardCamera = mainCam.GetComponent<BoardCamera>();
            defaultCamPos = mainCam.transform.position;
            defaultCamSize = mainCam.orthographicSize;
            targetCamPos = defaultCamPos;
            targetCamSize = defaultCamSize;

            BoardView boardView = FindAnyObjectByType<BoardView>();

            // Wait for the first OffenseSoldier to spawn and walk onto the board
            OffenseSoldier soldier = null;
            while (soldier == null)
            {
                soldier = FindAnyObjectByType<OffenseSoldier>();
                yield return null;
            }

            // Let the soldier march slightly onto the track so it's clearly visible
            yield return new WaitForSeconds(0.55f);

            // -----------------------------------------------------------------
            // FREEZE 1: Zoom in on Player (Attacker)
            // -----------------------------------------------------------------
            TakeCameraControl();
            SetTutorialPause(true);

            if (soldier != null)
            {
                FocusCamera(soldier.transform.position, 2.2f);
                ShowWorldRing(soldier.transform.position, new Color(1f, 0.35f, 0.3f, 0.95f));
            }

            ShowBanner(
                "1/4 — YOU ARE THE ATTACKER!",
                "This <color=#F2594D><b>RED SQUARE</b></color> is your soldier. Your squad marches down the path automatically.",
                "Click anywhere to continue..."
            );

            yield return WaitForClick();

            // -----------------------------------------------------------------
            // FREEZE 2: Zoom out to include Nearest Tower
            // -----------------------------------------------------------------
            Tower nearestTower = FindNearestTower(soldier != null ? soldier.transform.position : defaultCamPos);
            if (soldier != null && nearestTower != null)
            {
                FrameTwoPoints(soldier.transform.position, nearestTower.transform.position, 2.2f);
                ShowWorldRing(nearestTower.transform.position, new Color(0.35f, 0.7f, 1f, 0.95f));
            }

            ShowBanner(
                "2/4 — ENEMY TOWERS",
                "<color=#4D8CF5><b>BLUE SQUARES</b></color> are enemy towers. Watch the circle around them — that is their firing range!",
                "Click to march into range..."
            );

            yield return WaitForClick();

            // Unpause and let the soldier enter range, shoot, and take damage
            HideBanner();
            HideWorldRing();
            SetTutorialPause(false);

            // Wait until both combat starts AND a hit registers (soldier takes damage or gold increases)
            OffenseData offenseData = FindOffenseData();
            double startGold = offenseData != null ? offenseData.gold : -1;
            float waitTimer = 0f;

            while (waitTimer < 4.5f)
            {
                waitTimer += Time.deltaTime;
                if (soldier == null)
                {
                    soldier = FindAnyObjectByType<OffenseSoldier>();
                    if (soldier == null) break;
                }

                if (nearestTower != null)
                {
                    FrameTwoPoints(soldier.transform.position, nearestTower.transform.position, 2.2f);
                }

                bool goldEarned = offenseData != null && offenseData.gold > startGold;
                bool projActive = FindAnyObjectByType<Projectile>() != null;

                if (goldEarned || (projActive && waitTimer > 1.1f))
                {
                    // Let the impact ring & HP bar show for a beat before freezing
                    yield return new WaitForSeconds(0.25f);
                    break;
                }

                yield return null;
            }

            // -----------------------------------------------------------------
            // FREEZE 3: Pause on Range & Damage Taken + Highlight Money (Top Right)
            // -----------------------------------------------------------------
            SetTutorialPause(true);

            if (soldier != null && nearestTower != null)
            {
                FrameTwoPoints(soldier.transform.position, nearestTower.transform.position, 1.9f);
            }

            goldCalloutBox.SetActive(true);
            ShowBanner(
                "3/4 — DAMAGE = GOLD ($)",
                "Every hit your soldier lands on a tower earns <b><color=#FFD93D>+$1 GOLD</color> (Top Right)</b>! When your wave dies, you'll spend that Gold on upgrades.",
                "Click to see the Goal..."
            );

            yield return WaitForClick();
            goldCalloutBox.SetActive(false);

            // -----------------------------------------------------------------
            // FREEZE 4: Pan to Goal, then Zoom Out to Full Board
            // -----------------------------------------------------------------
            Vector3 goalPos = boardView != null ? (Vector3)boardView.Path.End : new Vector3(8f, -3f, 0f);
            FocusCamera(goalPos, 3.0f);
            ShowWorldRing(goalPos, new Color(0.3f, 0.95f, 0.4f, 0.95f));

            ShowBanner(
                "4/4 — REACH THE GOAL TO WIN!",
                "Keep upgrading and adding units from cards between waves until <b>just 1 soldier</b> reaches this end point!",
                "Click to zoom out & play!"
            );

            yield return WaitForClick();

            // Smoothly zoom back out to the full board before unpausing
            HideWorldRing();
            FocusCamera(defaultCamPos, defaultCamSize);
            HideBanner();

            float zoomOutTimer = 0f;
            while (zoomOutTimer < 0.45f)
            {
                zoomOutTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            ReleaseCameraControl();
            SetTutorialPause(false);

            PlayerPrefs.SetInt(TutorialPrefsKey, 1);
            PlayerPrefs.Save();

            if (tutorialCanvasObj != null)
                tutorialCanvasObj.SetActive(false);
        }

        private void SetTutorialPause(bool paused)
        {
            OffenseWaveManager.IsTutorialPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        private IEnumerator WaitForClick()
        {
            // Small unscaled delay to prevent accidental double-clicks
            yield return new WaitForSecondsRealtime(0.18f);
            userClickedNext = false;
            while (!userClickedNext)
            {
                yield return null;
            }
        }

        private void TakeCameraControl()
        {
            if (mainCam == null) return;
            if (boardCamera != null) boardCamera.enabled = false;
            targetCamPos = mainCam.transform.position;
            targetCamSize = mainCam.orthographicSize;
            isControllingCamera = true;
        }

        private void ReleaseCameraControl()
        {
            isControllingCamera = false;
            if (mainCam != null)
            {
                mainCam.transform.position = defaultCamPos;
                mainCam.orthographicSize = defaultCamSize;
            }
            if (boardCamera != null) boardCamera.enabled = true;
        }

        private void FocusCamera(Vector3 worldPoint, float orthoSize)
        {
            targetCamPos = new Vector3(worldPoint.x, worldPoint.y, defaultCamPos.z);
            targetCamSize = orthoSize;
        }

        private void FrameTwoPoints(Vector3 a, Vector3 b, float pad)
        {
            if (mainCam == null) return;
            Vector3 mid = (a + b) * 0.5f;
            // Offset slightly downward so the bottom banner doesn't cover the action
            targetCamPos = new Vector3(mid.x, mid.y - 0.35f, defaultCamPos.z);

            float aspect = Mathf.Max(0.1f, mainCam.aspect);
            float halfW = Mathf.Abs(a.x - b.x) * 0.5f + pad;
            float halfH = Mathf.Abs(a.y - b.y) * 0.5f + pad;
            targetCamSize = Mathf.Clamp(Mathf.Max(halfH, halfW / aspect), 2.2f, defaultCamSize);
        }

        private Tower FindNearestTower(Vector3 fromPos)
        {
            Tower[] towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
            Tower best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < towers.Length; i++)
            {
                if (towers[i] == null) continue;
                float d = Vector2.Distance(fromPos, towers[i].transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = towers[i];
                }
            }
            return best;
        }

        private OffenseData FindOffenseData()
        {
            // Grab OffenseData from GoldDisplay or Resources if available
            GoldDisplay gd = FindAnyObjectByType<GoldDisplay>();
            if (gd != null)
            {
                var field = typeof(GoldDisplay).GetField("offenseData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null) return field.GetValue(gd) as OffenseData;
            }
            return null;
        }

        private void ShowWorldRing(Vector3 pos, Color color)
        {
            if (worldMarkerRing == null)
            {
                worldMarkerRing = new GameObject("TutorialWorldRing");
                var sr = worldMarkerRing.AddComponent<SpriteRenderer>();
                sr.sprite = ShapeSprites.ThickRing;
                sr.sortingOrder = SortingOrders.HealthBarFill + 5;
            }
            worldMarkerRing.SetActive(true);
            worldMarkerRing.transform.position = new Vector3(pos.x, pos.y, 0f);
            worldMarkerRing.GetComponent<SpriteRenderer>().color = color;
        }

        private void HideWorldRing()
        {
            if (worldMarkerRing != null)
                worldMarkerRing.SetActive(false);
        }

        private void BuildFreezeFrameUI()
        {
            Canvas canvas = CreateCanvas("TutorialFreezeCanvas", ModalReferenceResolution, ModalMatchWidthOrHeight, ModalSortingOrder);
            tutorialCanvasObj = canvas.gameObject;

            // Bottom Banner Card
            Image bannerBg = CreateImage(canvas.transform, "FreezeBanner", new Color(0.08f, 0.11f, 0.17f, 0.96f));
            bannerBg.raycastTarget = true;
            bannerPanel = bannerBg.gameObject;
            SetRect(bannerBg.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(880f, 175f));

            Outline bannerOutline = bannerPanel.AddComponent<Outline>();
            bannerOutline.effectColor = new Color(0.3f, 0.85f, 0.4f, 1f);
            bannerOutline.effectDistance = new Vector2(3f, -3f);

            bannerTitle = CreateText(bannerPanel.transform, "Title", "", 30f, new Color(0.35f, 0.95f, 0.45f), TextAlignmentOptions.Center);
            bannerTitle.fontStyle = FontStyles.Bold;
            SetRect(bannerTitle.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-40f, 0f));

            bannerBody = CreateText(bannerPanel.transform, "Body", "", 23f, Color.white, TextAlignmentOptions.Center);
            SetRect(bannerBody.rectTransform, new Vector2(0f, 0.24f), new Vector2(1f, 0.68f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-50f, 0f));

            bannerPrompt = CreateText(bannerPanel.transform, "Prompt", "", 18f, new Color(0.75f, 0.82f, 0.9f), TextAlignmentOptions.Center);
            bannerPrompt.fontStyle = FontStyles.Italic;
            SetRect(bannerPrompt.rectTransform, new Vector2(0f, 0.03f), new Vector2(1f, 0.24f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-40f, 0f));

            // Top-Right Money Callout Box (Points directly at upper-right Gold counter)
            Image goldBg = CreateImage(canvas.transform, "GoldCalloutBox", new Color(0.15f, 0.55f, 0.2f, 0.96f));
            goldCalloutBox = goldBg.gameObject;
            SetRect(goldBg.rectTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -145f), new Vector2(320f, 86f));

            Outline goldOutline = goldCalloutBox.AddComponent<Outline>();
            goldOutline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
            goldOutline.effectDistance = new Vector2(3f, -3f);

            TextMeshProUGUI goldLabel = CreateText(
                goldCalloutBox.transform,
                "GoldPointerText",
                "▲ YOUR GOLD ($) ▲\nSpend this on Upgrades!",
                22f,
                new Color(1f, 0.92f, 0.3f, 1f),
                TextAlignmentOptions.Center
            );
            goldLabel.fontStyle = FontStyles.Bold;
            Stretch(goldLabel.rectTransform);

            bannerPanel.SetActive(false);
            goldCalloutBox.SetActive(false);
        }

        private void ShowBanner(string title, string body, string prompt)
        {
            if (bannerPanel == null) return;
            bannerPanel.SetActive(true);
            bannerTitle.text = title;
            bannerBody.text = body;
            bannerPrompt.text = prompt;
        }

        private void HideBanner()
        {
            if (bannerPanel != null)
                bannerPanel.SetActive(false);
        }

        // =====================================================================
        // MODAL POPUP (StartGame & UpgradeScene "? How to Play" button)
        // =====================================================================

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

            Button button = CreateButton(canvas.transform, "HowToPlayButton", "How to Play", LightButtonColor, DarkTextColor, 20f);
            SetRect((RectTransform)button.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-2f, -100f), new Vector2(150f, 36f));
            button.onClick.AddListener(Show);
        }

        private void BuildUpgradeButton()
        {
            Canvas canvas = CreateCanvas("HowToPlayButtonCanvas", UpgradeReferenceResolution, SceneMatchWidthOrHeight, SceneButtonSortingOrder);

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

            Image backdrop = CreateImage(canvas.transform, "Backdrop", BackdropColor);
            backdrop.raycastTarget = true;
            Stretch((RectTransform)backdrop.transform);

            Image card = CreateImage(backdrop.transform, "Card", CardColor);
            card.raycastTarget = true;
            SetRect((RectTransform)card.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, CardSize);

            TextMeshProUGUI heading = CreateText(card.transform, "Heading", "Quick Guide", 40f, HeadingColor, TextAlignmentOptions.Center);
            heading.fontStyle = FontStyles.Bold;
            SetRect(heading.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(-80f, 56f));

            TextMeshProUGUI body = CreateText(card.transform, "Rules", RulesText, 25f, BodyColor, TextAlignmentOptions.TopLeft);
            body.enableAutoSizing = true;
            body.fontSizeMin = 16f;
            body.fontSizeMax = 25f;
            body.paragraphSpacing = 14f;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.offsetMin = new Vector2(44f, 100f);
            bodyRect.offsetMax = new Vector2(-44f, -90f);

            closeButton = CreateButton(card.transform, "GotItButton", "Got it", AccentColor, Color.white, 26f);
            SetRect((RectTransform)closeButton.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(200f, 54f));
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