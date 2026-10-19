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
using ReverseTD.UpgradeUI;

namespace ReverseTD.Shared
{
    public class HowToPlay : MonoBehaviour
    {
        private const string TitleSceneName = "StartGame";
        private const string UpgradeSceneName = "UpgradeScene";
        private const string GameplaySceneName = "DefenseSandbox";
        private const string SeenPrefsKey = "HowToPlay_Seen";
        private const string TutorialPrefsKey = "InteractiveTutorial_Done";
        private const string IntroTutorialPrefsKey = "InteractiveTutorial_IntroDone";
        private const string SecondTowerTutorialPrefsKey = "InteractiveTutorial_SecondTowerDone";
        private const float SecondTowerTutorialDistance = 5f;
        private const float GoalTutorialDistance = 5f;

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

        // Modal UI (StartGame)
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
        private bool waitingForFormationClick;
        private bool formationTutorialClicked;
        private Button formationTutorialButton;
        private OffenseWaveManager formationTutorialManager;
        private TutorialButtonSpotlight formationSpotlight;

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
            EndFormationTutorial();
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
            if (bannerPanel != null && bannerPanel.activeSelf && !waitingForFormationClick)
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
            StartCoroutine(PlayerPrefs.GetInt(IntroTutorialPrefsKey, 0) == 1
                ? WaitAndShowLateTutorials()
                : RunFreezeFrameSequence());
        }

        private IEnumerator RunFreezeFrameSequence(int firstStep = 1, bool replay = false)
        {
            if (!replay)
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
            }

            // Wait for the first OffenseSoldier to spawn and walk onto the board
            OffenseSoldier soldier = null;
            while (soldier == null)
            {
                soldier = FindAnyObjectByType<OffenseSoldier>();
                yield return null;
            }

            // Let the soldier march slightly onto the track so it's clearly visible
            if (!replay) yield return new WaitForSeconds(0.55f);

            // -----------------------------------------------------------------
            // FREEZE 1: Zoom in on Player (Attacker)
            // -----------------------------------------------------------------
            if (!replay) TakeCameraControl();
            SetTutorialPause(true);

            if (firstStep <= 1)
            {
                if (soldier != null)
                {
                    FocusCamera(soldier.transform.position, 2.2f);
                    ShowWorldRing(soldier.transform.position, new Color(1f, 0.35f, 0.3f, 0.95f));
                }

                ShowBanner(
                    "1/6 — YOU ARE THE ATTACKER!",
                    "This <color=#F2594D><b>RED SQUARE</b></color> is your soldier. Your squad marches down the path automatically.",
                    "Click anywhere to continue...", 1
                );

                yield return WaitForClick();
            }

            // -----------------------------------------------------------------
            // FREEZE 2: Zoom out to include Nearest Tower
            // -----------------------------------------------------------------
            Tower nearestTower = FindNearestTower(soldier != null ? soldier.transform.position : defaultCamPos);
            if (firstStep <= 2)
            {
                if (soldier != null && nearestTower != null)
                {
                    FrameTwoPoints(soldier.transform.position, nearestTower.transform.position, 2.2f);
                    ShowWorldRing(nearestTower.transform.position, new Color(0.35f, 0.7f, 1f, 0.95f));
                }

                ShowBanner(
                    "2/6 — ENEMY TOWERS",
                    "<color=#4D8CF5><b>BLUE SQUARES</b></color> are enemy towers. The narrow strip shows each tower's fixed firing direction and range!",
                    "Click to march into range...", 2
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

            }
            // -----------------------------------------------------------------
            // FREEZE 3: Pause on Range & Damage Taken + Highlight Money (Top Right)
            // -----------------------------------------------------------------
            SetTutorialPause(true);

            if (firstStep <= 3)
            {
                if (soldier != null && nearestTower != null)
                {
                    FrameTwoPoints(soldier.transform.position, nearestTower.transform.position, 1.9f);
                }

                goldCalloutBox.SetActive(true);
                ShowBanner(
                    "3/6 — DAMAGE = GOLD ($)",
                    "Every hit your soldier lands on a tower earns <b><color=#FFD93D>+$1 GOLD</color> (Top Right)</b>! When your wave dies, you'll spend that Gold on upgrades.",
                    "Click to learn about formations...", 3
                );

                yield return WaitForClick();
                goldCalloutBox.SetActive(false);
            }

            // -----------------------------------------------------------------
            // FREEZE 4: Keep step 3's camera framing and spotlight the real button.
            // -----------------------------------------------------------------
            HideWorldRing();

            ShowBanner(
                "4/6 — EDIT YOUR FORMATION",
                "Click <b>EDIT FORMATION</b> to rearrange your <b>3x3 squad</b>. Drag units to swap positions, then choose <b>APPLY & RESUME</b> to finish.",
                "Click the highlighted EDIT FORMATION button.", 4
            );

            yield return GuideFormationButton();

            yield return FinishIntroduction();
        }

        private IEnumerator FinishIntroduction()
        {
            // Resume normal play; the final hint waits until the squad approaches the goal.
            PlayerPrefs.SetInt(IntroTutorialPrefsKey, 1);
            PlayerPrefs.Save();
            HideBanner();
            HideWorldRing();
            ReleaseCameraControl();
            SetTutorialPause(false);
            yield return WaitAndShowLateTutorials();
        }

        private IEnumerator WaitAndShowLateTutorials()
        {
            // On a later wave, skip the completed introduction and initialize the normal view.
            if (mainCam == null)
            {
                yield return null;
                yield return null;
                mainCam = Camera.main;
                if (mainCam == null) yield break;
                boardCamera = mainCam.GetComponent<BoardCamera>();
                defaultCamPos = mainCam.transform.position;
                defaultCamSize = mainCam.orthographicSize;
            }

            BoardView boardView = FindAnyObjectByType<BoardView>();
            if (boardView == null) yield break;

            if (PlayerPrefs.GetInt(SecondTowerTutorialPrefsKey, 0) == 0)
            {
                yield return WaitAndShowSecondTowerTutorial(boardView);
            }

            while (!IsAnySoldierNearGoal())
            {
                yield return null;
            }

            SetTutorialPause(true);
            TakeCameraControl();

            // -----------------------------------------------------------------
            // FREEZE 6: Pan to Goal, then Zoom Out to Full Board
            // -----------------------------------------------------------------
            Vector3 goalPos = boardView.Path.End;
            FocusCamera(goalPos, 3.0f);
            ShowWorldRing(goalPos, new Color(0.3f, 0.95f, 0.4f, 0.95f));

            ShowBanner(
                "6/6 — REACH THE GOAL TO WIN!",
                "Keep upgrading and adding units from cards between waves until <b>just 1 soldier</b> reaches this end point!",
                "Click to zoom out & play!", 6
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

        private IEnumerator WaitAndShowSecondTowerTutorial(BoardView board)
        {
            if (board.Layout == null || board.Layout.TowerSlots.Count < 2) yield break;

            // Use the configured second slot, not the shrinking list of surviving towers.
            Vector2 position = board.Layout.TowerSlots[1].Position;
            Tower secondTower = null;
            foreach (Tower tower in board.Towers)
            {
                if (tower != null && ((Vector2)tower.transform.position - position).sqrMagnitude < 0.0001f)
                {
                    secondTower = tower;
                    break;
                }
            }
            if (secondTower == null) yield break;
            TowerHealth health = secondTower.GetComponent<TowerHealth>();
            OffenseSoldier nearbyUnit = null;
            while (CombatRegistry.IsAlive(health))
            {
                nearbyUnit = FindSoldierNearPoint(position, SecondTowerTutorialDistance);
                if (nearbyUnit != null || IsAnySoldierNearGoal()) break;
                yield return null;
            }
            if (!CombatRegistry.IsAlive(health) || nearbyUnit == null) yield break;

            SetTutorialPause(true);
            TakeCameraControl();
            FrameTwoPoints(nearbyUnit.Position, position, 1.9f);
            ShowWorldRing(position, new Color(0.35f, 0.7f, 1f, 0.95f));
            ShowBanner(
                "5/6 — AVOID THE NEXT TOWER",
                "You're approaching another tower! When play resumes, click <b>EDIT FORMATION</b> and move your unit <b>out of its firing line</b> to avoid damage.",
                "Click to continue, then adjust your formation.", 5
            );
            yield return WaitForClick();

            PlayerPrefs.SetInt(SecondTowerTutorialPrefsKey, 1);
            PlayerPrefs.Save();
            HideBanner();
            HideWorldRing();
            ReleaseCameraControl();
            SetTutorialPause(false);
        }

        private OffenseSoldier FindSoldierNearPoint(Vector3 point, float distance)
        {
            OffenseWaveManager manager = OffenseWaveManager.Instance;
            if (manager == null || manager.IsSquadEditing) return null;

            float nearestSqr = distance * distance;
            OffenseSoldier nearest = null;
            for (int cell = 0; cell < SquadRules.CellCount; cell++)
            {
                OffenseSoldier unit = manager.GetSoldierAt(cell);
                if (unit == null) continue;
                float distanceSqr = (unit.Position - point).sqrMagnitude;
                if (distanceSqr <= nearestSqr)
                {
                    nearest = unit;
                    nearestSqr = distanceSqr;
                }
            }
            return nearest;
        }

        private bool IsAnySoldierNearGoal()
        {
            OffenseWaveManager manager = OffenseWaveManager.Instance;
            if (manager == null || manager.IsSquadEditing) return false;

            for (int cell = 0; cell < SquadRules.CellCount; cell++)
            {
                OffenseSoldier unit = manager.GetSoldierAt(cell);
                if (unit == null || unit.CurrentLane == null) continue;
                float remaining = unit.CurrentLane.TotalLength - unit.DistanceTraveled;
                if (remaining >= 0f && remaining <= GoalTutorialDistance) return true;
            }
            return false;
        }

        private IEnumerator GuideFormationButton()
        {
            formationTutorialManager = OffenseWaveManager.Instance;
            formationTutorialButton = FindFormationButton();
            if (formationTutorialManager == null || formationTutorialButton == null)
            {
                // Keep the guide usable if a scene has no formation button configured.
                bannerPrompt.text = "Click to see the Goal...";
                yield return WaitForClick();
                EndFormationTutorial();
                yield break;
            }

            var unitsBeforeEdit = new OffenseSoldier[SquadRules.CellCount];
            var positionsBeforeEdit = new Vector3[SquadRules.CellCount];
            for (int cell = 0; cell < SquadRules.CellCount; cell++)
            {
                unitsBeforeEdit[cell] = formationTutorialManager.GetSoldierAt(cell);
                if (unitsBeforeEdit[cell] != null) positionsBeforeEdit[cell] = unitsBeforeEdit[cell].Position;
            }

            waitingForFormationClick = true;
            formationTutorialClicked = false;
            formationTutorialManager.AllowTutorialSquadEditing = true;
            formationTutorialButton.onClick.AddListener(OnFormationTutorialClicked);
            formationSpotlight = TutorialButtonSpotlight.Create(tutorialCanvasObj.transform, (RectTransform)formationTutorialButton.transform, PressFormationButton);

            while (!formationTutorialClicked && formationTutorialManager != null && formationTutorialButton != null)
            {
                yield return null;
            }

            if (formationTutorialManager != null)
            {
                formationTutorialManager.AllowTutorialSquadEditing = false;
                while (formationTutorialManager != null && formationTutorialManager.IsSquadEditing)
                {
                    yield return null;
                }
            }
            EndFormationTutorial();

            // Apply moves the existing objects immediately; explain the result before releasing
            // the tutorial pause or the camera framing inherited from step 3.
            OffenseSoldier movedUnit = null;
            for (int cell = 0; cell < unitsBeforeEdit.Length; cell++)
            {
                OffenseSoldier unit = unitsBeforeEdit[cell];
                if (unit != null && unit.IsAlive && (unit.Position - positionsBeforeEdit[cell]).sqrMagnitude > 0.0001f)
                {
                    movedUnit = unit;
                    break;
                }
            }

            if (movedUnit != null)
            {
                ShowWorldRing(movedUnit.Position, new Color(0.3f, 0.95f, 0.4f, 0.95f));
                ShowBanner(
                    "POSITIONING AVOIDS DAMAGE",
                    "Your formation change moved this unit on the battlefield! Moving a unit <b>out of a tower's firing line</b> lets it <b>avoid incoming damage</b>.",
                    "Click to continue...", 7
                );
                yield return WaitForClick();
                HideWorldRing();
            }
        }

        private Button FindFormationButton()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (Button button in buttons)
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentTarget(i) == formationTutorialManager &&
                        button.onClick.GetPersistentMethodName(i) == nameof(OffenseWaveManager.OpenSquadEditor))
                    {
                        return button;
                    }
                }
            }
            return null;
        }

        private void PressFormationButton()
        {
            if (formationTutorialButton != null) formationTutorialButton.onClick.Invoke();
        }

        private void OnFormationTutorialClicked()
        {
            if (formationTutorialManager == null) return;
            if (!formationTutorialManager.IsSquadEditing) formationTutorialManager.OpenSquadEditor();
            if (!formationTutorialManager.IsSquadEditing) return;

            formationTutorialClicked = true;
            HideFormationSpotlight();
            HideBanner();
        }

        private void HideFormationSpotlight()
        {
            if (formationSpotlight == null) return;
            formationSpotlight.gameObject.SetActive(false);
            Destroy(formationSpotlight.gameObject);
            formationSpotlight = null;
        }

        private void EndFormationTutorial()
        {
            if (formationTutorialButton != null) formationTutorialButton.onClick.RemoveListener(OnFormationTutorialClicked);
            if (formationTutorialManager != null) formationTutorialManager.AllowTutorialSquadEditing = false;
            HideFormationSpotlight();
            formationTutorialButton = null;
            formationTutorialManager = null;
            waitingForFormationClick = false;
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

        private void ShowBanner(string title, string body, string prompt, int step)
        {
            if (bannerPanel == null) return;
            bannerPanel.SetActive(true);
            bannerTitle.text = title;
            bannerBody.text = body;
            bannerPrompt.text = prompt;
            CaptureTutorialCheckpoint(title, body, prompt, step);
        }

        private void HideBanner()
        {
            if (bannerPanel != null)
                bannerPanel.SetActive(false);
        }

        private void CaptureTutorialCheckpoint(string title, string body, string prompt, int step)
        {
            LevelOneTutorialRewind rewind = OffenseWaveManager.Instance != null
                ? OffenseWaveManager.Instance.TutorialRewind : null;
            if (rewind == null || mainCam == null) return;

            // Keep checkpoint identity independent of editable tutorial text.
            int introDone = PlayerPrefs.GetInt(IntroTutorialPrefsKey, 0);
            int secondDone = PlayerPrefs.GetInt(SecondTowerTutorialPrefsKey, 0);
            int tutorialDone = PlayerPrefs.GetInt(TutorialPrefsKey, 0);
            Vector3 cameraPosition = mainCam.transform.position, savedTarget = targetCamPos;
            Vector3 fullPosition = defaultCamPos;
            float cameraSize = mainCam.orthographicSize, savedSize = targetCamSize, fullSize = defaultCamSize;
            bool ringVisible = worldMarkerRing != null && worldMarkerRing.activeSelf;
            Vector3 ringPosition = ringVisible ? worldMarkerRing.transform.position : Vector3.zero;
            Color ringColor = ringVisible ? worldMarkerRing.GetComponent<SpriteRenderer>().color : Color.white;
            bool goldVisible = goldCalloutBox.activeSelf;

            rewind.Capture(() =>
            {
                StopAllCoroutines();
                EndFormationTutorial();
                PlayerPrefs.SetInt(IntroTutorialPrefsKey, introDone);
                PlayerPrefs.SetInt(SecondTowerTutorialPrefsKey, secondDone);
                PlayerPrefs.SetInt(TutorialPrefsKey, tutorialDone);
                PlayerPrefs.Save();
                tutorialCanvasObj.SetActive(true);
                defaultCamPos = fullPosition;
                defaultCamSize = fullSize;
                mainCam.transform.position = cameraPosition;
                mainCam.orthographicSize = cameraSize;
                if (boardCamera != null) boardCamera.enabled = false;
                isControllingCamera = true;
                targetCamPos = savedTarget;
                targetCamSize = savedSize;
                userClickedNext = false;
                goldCalloutBox.SetActive(goldVisible);
                if (ringVisible) ShowWorldRing(ringPosition, ringColor);
                else HideWorldRing();
                SetTutorialPause(true);
                if (step <= 4) StartCoroutine(RunFreezeFrameSequence(step, true));
                else if (step == 7) StartCoroutine(ReplayFormationResult(title, body, prompt));
                else StartCoroutine(WaitAndShowLateTutorials());
            });
        }

        private IEnumerator ReplayFormationResult(string title, string body, string prompt)
        {
            ShowBanner(title, body, prompt, 7);
            yield return WaitForClick();
            yield return FinishIntroduction();
        }

        public static void ResetLevelOneTutorial()
        {
            PlayerPrefs.DeleteKey(IntroTutorialPrefsKey);
            PlayerPrefs.DeleteKey(SecondTowerTutorialPrefsKey);
            PlayerPrefs.DeleteKey(TutorialPrefsKey);
            PlayerPrefs.Save();
        }

        public static void SuspendBattleTutorial()
        {
            if (current == null) return;
            current.StopAllCoroutines();
            current.EndFormationTutorial();
            current.HideBanner();
            current.HideWorldRing();
            if (current.goldCalloutBox != null) current.goldCalloutBox.SetActive(false);
            current.isControllingCamera = false;
            OffenseWaveManager.IsTutorialPaused = true;
            Time.timeScale = 0f;
        }

        public static void ShowLevelOneDefeat(System.Action restart, System.Action rewind, bool canRewind)
        {
            if (current == null) return;
            SuspendBattleTutorial();

            Canvas canvas = current.CreateCanvas("LevelOneDefeat", ModalReferenceResolution, ModalMatchWidthOrHeight, ModalSortingOrder + 200);
            Image backdrop = CreateImage(canvas.transform, "Backdrop", BackdropColor);
            backdrop.raycastTarget = true;
            Stretch(backdrop.rectTransform);
            Image card = CreateImage(canvas.transform, "DefeatCard", CardColor);
            SetRect(card.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(660f, 300f));
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.85f, 0.4f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            var heading = CreateText(card.transform, "Title", "SQUAD DEFEATED", 34f, HeadingColor, TextAlignmentOptions.Center);
            heading.fontStyle = FontStyles.Bold;
            SetRect(heading.rectTransform, new Vector2(0f, 0.7f), new Vector2(1f, 0.95f), Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            var description = CreateText(card.transform, "Description", canRewind
                ? "Try again, or return to the last tutorial hint.\nYour squad and battle state will be restored."
                : "No tutorial checkpoint is available yet.\nRestart the level to try again.", 23f, BodyColor, TextAlignmentOptions.Center);
            SetRect(description.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 0.7f), Vector2.one * 0.5f, Vector2.zero, new Vector2(-40f, 0f));
            Button restartButton = CreateButton(card.transform, "Restart", "RESTART LEVEL", AccentColor, Color.white, 23f);
            SetRect((RectTransform)restartButton.transform, new Vector2(0.26f, 0.2f), new Vector2(0.26f, 0.2f), Vector2.one * 0.5f, Vector2.zero, new Vector2(270f, 60f));
            Button rewindButton = CreateButton(card.transform, "Rewind", "BACK TO LAST HINT", new Color(0.2f, 0.45f, 0.8f), Color.white, 23f);
            SetRect((RectTransform)rewindButton.transform, new Vector2(0.74f, 0.2f), new Vector2(0.74f, 0.2f), Vector2.one * 0.5f, Vector2.zero, new Vector2(300f, 60f));
            rewindButton.interactable = canRewind;
            restartButton.onClick.AddListener(() => { canvas.gameObject.SetActive(false); Destroy(canvas.gameObject); restart(); });
            rewindButton.onClick.AddListener(() => { canvas.gameObject.SetActive(false); Destroy(canvas.gameObject); rewind(); });
        }

        // =====================================================================
        // TITLE GUIDE AND UPGRADE LEVEL LABEL
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
            if (!isTitle)
            {
                BuildUpgradeLevelLabel();
                return;
            }

            BuildTitleButton();
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

        private void BuildUpgradeLevelLabel()
        {
            Canvas canvas = CreateCanvas("UpgradeLevelCanvas", UpgradeReferenceResolution, SceneMatchWidthOrHeight, SceneButtonSortingOrder);
            UpgradeSceneUI upgrade = FindAnyObjectByType<UpgradeSceneUI>();
            int level = upgrade != null ? upgrade.NextBattleLevelNumber : 1;

            TextMeshProUGUI label = CreateText(canvas.transform, "Level Label", $"LEVEL {level}", 36f, Color.white, TextAlignmentOptions.TopLeft);
            label.fontStyle = FontStyles.Bold;
            SetRect(label.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -24f), new Vector2(240f, 60f));

            int deaths = upgrade != null ? upgrade.NextBattleDeathCount : 0;
            UiKit.CreateHeartIcon(canvas.transform, new Vector2(24f, -80f));
            TextMeshProUGUI deathLabel = CreateText(canvas.transform, "Death Count", UiKit.LifeCounterText(deaths), 24f, Color.white, TextAlignmentOptions.TopLeft);
            SetRect(deathLabel.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(56f, -76f), new Vector2(208f, 40f));
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
