using System.Collections.Generic;
using System.Linq;
using Team5.BasicTowerTrial.Presentation.LegacyUI;
using Team5.BasicTowerTrial.Session;
using Team5.BasicTowerTrial.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Team5.BasicTowerTrial.Presentation
{
    public sealed class FormationReserveBattle : MonoBehaviour
    {
        public const string ScenePath = "Assets/RandomMuster/Scenes/RandomMuster.unity";
        internal const string StartScenePath = "Assets/RandomMuster/Scenes/RandomMusterStart.unity";
        private const string EndScenePath = "Assets/RandomMuster/Scenes/RandomMusterEnd.unity";
        private static TavernRecruitment pendingTavern;
        private static readonly Color Gold = new Color(1, .79f, .36f);
        private static readonly Color Surface = new Color(.08f, .12f, .18f, .97f);
        [SerializeField] private BattleSession session;
        [SerializeField] private string nextMusterScenePath;
        [SerializeField] private Slider levelOneSpeedSlider;

        private readonly List<UnitDefinition> reserves = new List<UnitDefinition>();
        private UnitDefinition[] summonPool;
        private int regularZombieHandPassives;
        private readonly UnitDefinition[] readySquad = new UnitDefinition[9];
        private readonly int[] deployExtraZombies = new int[9];
        private readonly TMP_Text[] slotAmounts = new TMP_Text[9];
        private readonly Button[] cells = new Button[9];
        private readonly LegacyUnitIconUI[] icons = new LegacyUnitIconUI[9];
        private readonly List<Button> reserveButtons = new List<Button>();
        private BattleSimulation simulation;
        private TavernRecruitment tavern;
        private int musterTowerCount;
        private RectTransform root;
        private TMP_Text status, pauseText;
        private TMP_Text departureCount, departureCountdown;
        private Button launch, roll, pause;
        private Slider speedSlider;
        private Button draggedReserve;
        private RectTransform draggedIcon;
        private int selected = -1, lockedSlot = -1;
        private const int Refresh = -2;
        private const int LockItem = -3;
        [SerializeField] private bool showLockButton;
        private const int DepartureCountdownStart = 8;
        private static readonly Color RefreshAvailableColor = new Color(.18f, .55f, .29f);
        private double departureReadyTime;
        private const int UnitsPerSquad = 3;
        private bool choosing, leaving, departed;
        private bool departureCoolingDown;
        private float showZeroUntil = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => pendingTavern = null;

        public static void EnterFromTavern(TavernRecruitment recruitment)
        {
            pendingTavern = recruitment;
            Open(ScenePath);
        }

        private void Start()
        {
            tavern = pendingTavern;
            pendingTavern = null;
            if (tavern == null)
            {
                BattlegroundsUpgradeScene.NewGame();
                tavern = BattlegroundsUpgradeScene.State;
            }
            reserves.AddRange(tavern.Followers.Where(follower => follower.Slot < 0 && MusterZombieRules.IsZombie(follower.Role))
                .Select(tavern.Stats));
            regularZombieHandPassives = reserves.Count(unit => unit.Role == UnitRole.Zombie && unit.Level > 1);
            summonPool = BuildSummonPool(tavern.Tier, reserves);

            RefreshReadySquad(false);
            var initialRoster = readySquad.Select((unit, slot) => new { unit, slot })
                .Where(entry => entry.unit != null)
                .Select(entry => new MemberSpawnDefinition(3101 + entry.slot, entry.slot, entry.unit)).ToArray();
            session.SetPreparedMembers(initialRoster);
            session.StartBattle();
            simulation = session.Simulation;
            if (simulation == null) return;
            simulation.SetPaused(true);
            musterTowerCount = simulation.Towers.Count(tower =>
                tower.IsAlive && !tower.Definition.IsWall && !tower.Definition.IsMinion);
            simulation.EnableFixedWaveDeployment(tavern.Tier * 2, Mathf.Max(1, musterTowerCount));
            var inspection = session.transform.root.GetComponentInChildren<BattleUnitInspection>();
            if (inspection != null) inspection.IsInputSuppressed = true;
            if (EventSystem.current == null)
            {
                var input = new GameObject("Battle Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                input.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            BuildUi();
            Update();
        }

        private static UnitDefinition[] BuildSummonPool(int tier, IReadOnlyList<UnitDefinition> hand)
        {
            var brute = hand.Count(unit => unit.Role == UnitRole.BruteZombie) * (tier < 2 ? 15 : 18);
            var shaman = tier < 2 ? 0 : hand.Count(unit => unit.Role == UnitRole.ShamanZombie) * 13;
            var tank = tier < 2 ? 0 : hand.Count(unit => unit.Role == UnitRole.TankZombie) * 4;
            // If special weights exceed 100, draw among them in proportion to their weights.
            var regular = Mathf.Max(0, 100 - brute - shaman - tank);
            return Enumerable.Repeat(MusterZombieRules.Create(UnitRole.Zombie), regular)
                .Concat(Enumerable.Repeat(MusterZombieRules.Create(UnitRole.BruteZombie), brute))
                .Concat(Enumerable.Repeat(MusterZombieRules.Create(UnitRole.ShamanZombie), shaman))
                .Concat(Enumerable.Repeat(MusterZombieRules.Create(UnitRole.TankZombie), tank))
                .ToArray();
        }

        private static void Shuffle(int[] values)
        {
            for (var i = values.Length - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private void BuildUi()
        {
            var host = new GameObject("Formation and Reserves", typeof(RectTransform));
            host.transform.SetParent(transform, false);
            root = (RectTransform)host.transform;
            LegacyUiKit.CreateCanvas(host, 35, new Vector2(1280, 720), 0);
            BuildPlaybackControls();

            var bottom = LegacyUiKit.CreateImage(root, "Reserve bar", Vector2.zero, Vector2.zero, Surface).rectTransform;
            bottom.anchorMin = Vector2.zero;
            bottom.anchorMax = Vector2.right;
            bottom.pivot = new Vector2(.5f, 0);
            bottom.sizeDelta = new Vector2(0, 110);
            bottom.anchoredPosition = Vector2.zero;

            var panel = LegacyUiKit.CreateImage(bottom, "Squad panel", Vector2.zero, new Vector2(240, 252), Surface, 10).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(10, 8);
            launch = MakeButton(panel, "Launch", "DEPART", 120, 229, 210, 32, Depart);
            departureCount = LegacyUiKit.CreateText(launch.transform, "Departures remaining", "",
                new Vector2(-78, 0), new Vector2(44, 28), 18, Gold, FontStyles.Bold);
            departureCountdown = LegacyUiKit.CreateText(launch.transform, "Departure countdown", "",
                new Vector2(78, 0), new Vector2(40, 28), 18, Gold, FontStyles.Bold);
            roll = MakeButton(panel, "Refresh", "REFRESH", 64, 192, 98, 30, () => Select(Refresh));
            roll.targetGraphic.color = RefreshAvailableColor;
            var camera = Camera.main;
            var cameraRight = camera != null ? camera.transform.right : Vector3.right;
            // Keep the 3×3 preview aligned with the battle camera and the fixed wave's world-space slots.
            var gridRight = Mathf.Abs(cameraRight.x) >= Mathf.Abs(cameraRight.z)
                ? Vector3.right * Mathf.Sign(cameraRight.x)
                : Vector3.forward * Mathf.Sign(cameraRight.z);
            var gridUp = Vector3.Cross(gridRight, Vector3.up);
            for (var slot = 0; slot < 9; slot++)
            {
                var index = slot;
                var offset = SquadCombatRules.Offset(slot);
                var x = 120 + 64 * Vector3.Dot(offset, gridRight);
                var y = 91 + 56 * Vector3.Dot(offset, gridUp);
                var button = MakeButton(panel, "Slot " + slot, "", x, y, 57, 51, () => PickTarget(index));
                cells[slot] = button;
                icons[slot] = LegacyUnitIconUI.Create(button.transform, Vector2.zero, 84);
                var badge = LegacyUiKit.CreateImage(button.transform, "Count badge", new Vector2(17, -15),
                    new Vector2(23, 16), Surface, 3);
                slotAmounts[slot] = LegacyUiKit.CreateText(badge.transform, "Count", "", Vector2.zero,
                    new Vector2(22, 15), 11, Gold, FontStyles.Bold);
                AddPointerEvent(button.gameObject, EventTriggerType.Drop, pointer => DropReserve(index, pointer));
            }

            var tray = LegacyUiKit.CreateRect(bottom, "Reserve tray", Vector2.zero, new Vector2(1000, 110));
            tray.anchorMin = tray.anchorMax = tray.pivot = Vector2.zero;
            tray.anchoredPosition = new Vector2(258, 0);
            Center(tray, "Reserve label", "RESERVES", 56, 95, 112, 19, 14, Gold);
            for (var i = 0; i < reserves.Count; i++)
            {
                var reserve = reserves[i];
                var card = MakeButton(tray, "Reserve " + i, "", 36 + i * 69, 43, 65, 66, null);
                card.onClick.AddListener(() =>
                {
                    var index = reserveButtons.IndexOf(card);
                    if (index >= 0 && index < reserves.Count) Select(index);
                });
                LegacyUnitIconUI.Create(card.transform, new Vector2(0, 10), 78).SetType(reserve.Role, reserve.Level > 1);
                LegacyUiKit.CreateText(card.transform, "Name", LegacyUnitLooks.Name(reserve.Role), new Vector2(0, -20),
                    new Vector2(64, 18), 9, Color.white);
                reserveButtons.Add(card);
                AddPointerEvent(card.gameObject, EventTriggerType.BeginDrag, pointer => BeginReserveDrag(card, pointer));
                AddPointerEvent(card.gameObject, EventTriggerType.Drag, MoveReserveDrag);
                AddPointerEvent(card.gameObject, EventTriggerType.EndDrag, _ => EndReserveDrag());
            }
            var lockCard = MakeButton(panel, "Lock item", "LOCK", 176, 192, 98, 30, () => Select(LockItem));
            reserveButtons.Add(lockCard);
            lockCard.gameObject.SetActive(showLockButton);

            var actions = LegacyUiKit.CreateRect(root, "Battle controls", Vector2.zero, new Vector2(520, 116));
            actions.anchorMin = actions.anchorMax = actions.pivot = Vector2.right;
            actions.anchoredPosition = new Vector2(-18, 114);
            status = Center(actions, "Battle status", "", 260, 89, 510, 45, 16, Color.white);
            status.alignment = TextAlignmentOptions.Right;
        }

        private void BuildPlaybackControls()
        {
            speedSlider = Instantiate(levelOneSpeedSlider, root, false);
            speedSlider.name = "Speed Slider";
            var rect = (RectTransform)speedSlider.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.anchoredPosition = new Vector2(-100, -100);
            rect.localScale = Vector3.one * 1.92f;
            speedSlider.onValueChanged = new Slider.SliderEvent();
            speedSlider.SetValueWithoutNotify(session.PlaybackSpeed - 1);
            speedSlider.onValueChanged.AddListener(value =>
                session.SetPlaybackSpeed(Mathf.Clamp(Mathf.RoundToInt(value) + 1, 1, 3)));
            pause = LegacyUiKit.CreatePlaybackPause(speedSlider, TMP_Settings.defaultFontAsset,
                () =>
                {
                    if (departed) simulation.SetPaused(!simulation.IsPaused);
                }, out pauseText);
        }

        private void Update()
        {
            if (simulation == null || root == null) return;
            if (simulation.Outcome != BattleOutcome.Running)
            {
                EndBattle();
                return;
            }
            UpdateDepartureButton();
            for (var i = 0; i < reserveButtons.Count; i++)
            {
                var card = i == reserves.Count ? LockItem : i;
                reserveButtons[i].targetGraphic.color = choosing && selected == card
                    ? LegacyUiKit.NodeBlueColor : LegacyUiKit.PanelColor;
            }

            for (var slot = 0; slot < 9; slot++)
            {
                var unit = readySquad[slot];
                icons[slot].SetType(unit?.Role ?? UnitRole.Test, unit?.Level > 1);
                icons[slot].gameObject.SetActive(unit != null);
                var extras = GetExtraZombies(slot);
                slotAmounts[slot].transform.parent.gameObject.SetActive(extras > 0);
                if (extras > 0) slotAmounts[slot].text = $"×{extras + 1}";
                cells[slot].interactable = choosing && (selected >= 0 || selected == LockItem) &&
                    (selected == LockItem ? unit != null : CanApplyReserve(slot));
                cells[slot].targetGraphic.color = slot == lockedSlot ? new Color(.32f, .32f, .18f) :
                    cells[slot].interactable ? LegacyUiKit.NodeBlueColor : LegacyUiKit.PanelColor;
            }
            pause.interactable = departed;
            pauseText.text = departed && simulation.IsPaused ? "Resume" : "Pause";
            var progress = $"Round {tavern.Turn}/{TavernRecruitment.MaxRounds}   |   Reached: {simulation.ReachedGoalCount}/{Mathf.Max(1, musterTowerCount)}";
            status.text = $"{progress}\nCaptain HP: {tavern.CaptainHealth}";
        }

        private void UpdateDepartureButton()
        {
            var remaining = simulation.DeparturesRemaining;
            departureCount.text = remaining.HasValue ? $"x {remaining.Value}" : "";
            if (departureCoolingDown)
            {
                var secondsLeft = departureReadyTime - simulation.ElapsedTime;
                if (secondsLeft > 0)
                    departureCountdown.text = ((int)System.Math.Ceiling(secondsLeft)).ToString();
                else if (simulation.QueuedFixedWaves > 0 || simulation.FixedWaveClearanceDistance > 0)
                    departureCountdown.text = "0";
                else
                {
                    departureCoolingDown = false;
                    showZeroUntil = Time.unscaledTime + .25f;
                }
            }

            var showingZero = Time.unscaledTime < showZeroUntil;
            if (showingZero) departureCountdown.text = "0";
            departureCountdown.gameObject.SetActive(remaining != 0 && (departureCoolingDown || showingZero));
            launch.interactable = remaining != 0 && !departureCoolingDown && !showingZero;
            launch.targetGraphic.color = launch.interactable ? new Color(.69f, .49f, .13f) : LegacyUiKit.PanelColor;
        }

        private void Select(int adjustment)
        {
            if (simulation.Outcome != BattleOutcome.Running) return;
            if (choosing && selected == adjustment)
            {
                FinishAdjustment();
                return;
            }
            selected = adjustment;
            choosing = true;
            if (adjustment == Refresh) Commit();
        }

        private void PickTarget(int slot)
        {
            if (!choosing || !cells[slot].interactable) return;
            Commit(slot);
        }

        private bool CanApplyReserve(int slot) =>
            simulation.Outcome == BattleOutcome.Running &&
            slot != lockedSlot &&
            (readySquad[slot] != null || readySquad.Count(unit => unit != null) < SquadPreparation.MaxDeployed);

        private static void AddPointerEvent(GameObject target, EventTriggerType type,
            System.Action<PointerEventData> callback)
        {
            var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => callback((PointerEventData)data));
            trigger.triggers.Add(entry);
        }

        private void BeginReserveDrag(Button card, PointerEventData pointer)
        {
            var index = reserveButtons.IndexOf(card);
            if (index < 0 || index >= reserves.Count || !card.interactable ||
                pointer.button != PointerEventData.InputButton.Left) return;
            EndReserveDrag();
            draggedReserve = card;
            selected = index;
            choosing = true;
            var icon = LegacyUnitIconUI.Create(root, Vector2.zero, 108);
            var reserve = reserves[index];
            icon.SetType(reserve.Role, reserve.Level > 1);
            draggedIcon = icon.Rect;
            MoveReserveDrag(pointer);
        }

        private void MoveReserveDrag(PointerEventData pointer)
        {
            if (draggedIcon != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root, pointer.position, pointer.pressEventCamera, out var position))
                draggedIcon.anchoredPosition = position;
        }

        private void DropReserve(int slot, PointerEventData pointer)
        {
            if (draggedReserve == null || pointer.pointerDrag != draggedReserve.gameObject || !CanApplyReserve(slot))
                return;
            Commit(slot);
            EndReserveDrag();
        }

        private void EndReserveDrag()
        {
            if (draggedReserve == null) return;
            draggedReserve = null;
            if (draggedIcon != null) Destroy(draggedIcon.gameObject);
            draggedIcon = null;
            FinishAdjustment();
        }

        private void Depart()
        {
            if (departureCoolingDown || Time.unscaledTime < showZeroUntil) return;
            var extraZombies = Enumerable.Range(0, readySquad.Length).Select(GetExtraZombies).ToArray();
            if (!simulation.TryQueueFixedWave(readySquad, extraZombies)) return;
            departureReadyTime = simulation.ElapsedTime + DepartureCountdownStart;
            departureCoolingDown = simulation.DeparturesRemaining != 0;
            UpdateDepartureButton();
            RefreshReadySquad(false);
            FinishAdjustment();
            if (!departed)
            {
                departed = true;
                simulation.SetPaused(false);
            }
        }

        private int GetExtraZombies(int slot)
        {
            var unit = readySquad[slot];
            if (unit?.Role != UnitRole.Zombie) return 0;
            // An upgraded zombie in the formation was moved from this assault's starting hand.
            var otherHandPassives = regularZombieHandPassives - (unit.Level > 1 ? 1 : 0);
            return deployExtraZombies[slot] + otherHandPassives;
        }

        private void RefreshReadySquad(bool keepLock)
        {
            var preservedSlot = keepLock ? lockedSlot : -1;
            var preserved = preservedSlot < 0 ? null : readySquad[preservedSlot];
            var preservedExtras = preservedSlot < 0 ? 0 : deployExtraZombies[preservedSlot];
            var slots = Enumerable.Range(0, readySquad.Length).Where(slot => slot != preservedSlot).ToArray();
            Shuffle(slots);
            System.Array.Clear(readySquad, 0, readySquad.Length);
            System.Array.Clear(deployExtraZombies, 0, deployExtraZombies.Length);
            if (preserved != null)
            {
                readySquad[preservedSlot] = preserved;
                deployExtraZombies[preservedSlot] = preservedExtras;
            }
            var drawCount = UnitsPerSquad - (preserved == null ? 0 : 1);
            for (var index = 0; index < drawCount; index++)
                readySquad[slots[index]] = summonPool[Random.Range(0, summonPool.Length)];
            lockedSlot = -1;
        }

        private void Commit(int target = -1)
        {
            if (!choosing) return;
            if (selected == Refresh) RefreshReadySquad(true);
            else if (selected == LockItem && target >= 0)
            {
                lockedSlot = lockedSlot == target ? -1 : target;
            }
            else if (selected >= 0 && selected < reserves.Count && target >= 0 && CanApplyReserve(target))
            {
                var deployed = reserves[selected];
                readySquad[target] = deployed;
                deployExtraZombies[target] = 0;
                if (deployed.Role == UnitRole.Zombie && deployed.Level > 1)
                    for (var slot = 0; slot < readySquad.Length; slot++)
                        if (slot != target && readySquad[slot]?.Role == UnitRole.Zombie)
                            deployExtraZombies[slot]++;
                var used = reserveButtons[selected];
                reserves.RemoveAt(selected);
                used.interactable = false;
                used.gameObject.SetActive(false);
                reserveButtons.RemoveAt(selected);
            }
            else return;
            FinishAdjustment();
        }

        private void FinishAdjustment()
        {
            choosing = false;
            selected = -1;
        }

        private void EndBattle()
        {
            if (leaving || simulation.Outcome == BattleOutcome.Running) return;
            leaving = true;
            var furthestProgress = simulation.Units.Select(unit => unit.RouteProgress).DefaultIfEmpty().Max();
            // RouteProgress is in small cells. Allow for float error at a whole-cell boundary.
            tavern.RecordMusterProgress(Mathf.FloorToInt(furthestProgress + .001f));
            if (simulation.Outcome == BattleOutcome.Victory)
            {
                if (!string.IsNullOrWhiteSpace(nextMusterScenePath))
                {
                    pendingTavern = tavern;
                    Open(nextMusterScenePath);
                    return;
                }
                tavern.CompleteExpedition();
                Open(EndScenePath);
                return;
            }
            tavern.ResolveMuster(musterTowerCount, simulation.ReachedGoalCount);
            Open(tavern.Finished ? EndScenePath : BattlegroundsUpgradeScene.ScenePath);
        }

        private static TMP_Text Center(RectTransform parent, string name, string text, float x, float y,
            float width, float height, int fontSize, Color color)
        {
            var label = LegacyUiKit.CreateText(parent, name, text, Vector2.zero, new Vector2(width, height), fontSize, color);
            Place(label.rectTransform, x, y);
            return label;
        }

        private static Button MakeButton(RectTransform parent, string name, string text, float x, float y,
            float width, float height, UnityEngine.Events.UnityAction onClick)
        {
            var button = LegacyUiKit.CreateButton(parent, name, Vector2.zero, new Vector2(width, height), LegacyUiKit.PanelColor, 5);
            Place((RectTransform)button.transform, x, y);
            if (text.Length != 0)
            {
                var label = LegacyUiKit.CreateText(button.transform, "Label", text, Vector2.zero,
                    new Vector2(width - 4, height), 13, Color.white);
                label.textWrappingMode = TextWrappingModes.Normal;
            }
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        private static void Place(RectTransform rect, float x, float y)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
        }

        internal static void Open(string path)
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(path);
#endif
        }
    }
}
