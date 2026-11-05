using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class CaptainGuide : MonoBehaviour
    {
        private const int CenterSlot = 4;
        private const int TrailDots = 10;
        private static readonly Vector2 EmblemOffset = new Vector2(0f, 45f);
        private static readonly Color Gold = new Color(1f, 0.84f, 0.25f);
        private static readonly Color ChipInk = new Color(0.1f, 0.08f, 0f);
        private static readonly Color PulseLow = new Color(1f, 0.64f, 0.08f);
        private static readonly Color PulseHigh = new Color(1f, 0.94f, 0.48f);

        [SerializeField, Tooltip("Seconds the ghost takes to glide into the center slot.")] private float glideSeconds = 1.4f;
        [SerializeField, Tooltip("Seconds the ghost rests on the center slot before fading.")] private float holdSeconds = 0.5f;
        [SerializeField, Tooltip("Seconds the ghost takes to fade out.")] private float fadeSeconds = 0.3f;
        [SerializeField, Tooltip("Seconds between the fade and the next glide.")] private float idleSeconds = 0.6f;
        [SerializeField, Tooltip("Seconds after the pointer is released before the ghost comes back.")] private float resumeDelay = 0.6f;
        [SerializeField, Tooltip("Opacity of the ghost Captain.")] private float ghostAlpha = 0.78f;

        private readonly List<Image> trail = new List<Image>();
        private readonly Vector2[] cellPositions = new Vector2[9];
        private LegacyUpgradeScene owner;
        private RectTransform root;
        private CanvasGroup group;
        private RectTransform ghost;
        private CanvasGroup ghostGroup;
        private RectTransform pointer;
        private CanvasGroup pointerGroup;
        private Image centerCell;
        private Color centerColor;
        private Image cardFrame;
        private Color cardColor;
        private int cardIndex = -1;
        private float clock;
        private float resumeAt;
        private bool hidden, finished;

        public static CaptainGuide Attach(LegacyUpgradeScene owner, RectTransform root)
        {
            if (owner == null || root == null || owner.Preparation == null || owner.Grid == null) return null;
            if (owner.Preparation.DeployedCount > 0 || FindCaptain(owner.Preparation) == null) return null;
            var rect = LegacyUiKit.CreateRect(root, "Captain Guide", Vector2.zero, Vector2.zero);
            LegacyUiKit.Stretch(rect);
            var guide = rect.gameObject.AddComponent<CaptainGuide>();
            guide.owner = owner;
            guide.root = root;
            guide.Build();
            return guide;
        }

        private void Build()
        {
            group = gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var gridRect = (RectTransform)owner.Grid.transform;
            for (var slot = 0; slot < 9; slot++)
            {
                var cell = gridRect.Find("Cell " + slot) as RectTransform;
                cellPositions[slot] = gridRect.anchoredPosition + (cell != null ? cell.anchoredPosition : Vector2.zero);
                if (slot == CenterSlot && cell != null) centerCell = cell.GetComponent<Image>();
            }
            if (centerCell != null) centerColor = centerCell.color;
            for (var i = 0; i < TrailDots; i++)
                trail.Add(LegacyUiKit.CreateCircle(transform, "Guide Trail", Vector2.zero, 6f, new Color(1f, 1f, 1f, 0f)));
            var icon = LegacyUnitIconUI.Create(transform, Vector2.zero, 58f * 1.4f);
            icon.SetType(UnitRole.Captain);
            ghost = icon.Rect;
            ghostGroup = icon.gameObject.AddComponent<CanvasGroup>();
            ghostGroup.blocksRaycasts = false;
            BuildPointer();
            BuildChip("DRAG HERE", cellPositions[CenterSlot] + new Vector2(0f, 52f));
            ShowGhost(false);
        }

        private void BuildPointer()
        {
            pointer = LegacyUiKit.CreateRect(transform, "Guide Pointer", Vector2.zero, Vector2.zero);
            pointerGroup = pointer.gameObject.AddComponent<CanvasGroup>();
            pointerGroup.blocksRaycasts = false;
            Stroke("Tail Border", new Vector2(6f, -9f), new Vector2(7f, 14f), Color.black);
            Stroke("Tail", new Vector2(6f, -9f), new Vector2(3.5f, 11f), Color.white);
            Head("Head Border", 30f, Color.black);
            Head("Head", 22f, Color.white);
        }

        private void Stroke(string name, Vector2 position, Vector2 size, Color color)
        {
            var image = LegacyUiKit.CreateImage(pointer, name, position, size, color);
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 35f);
        }

        private void Head(string name, float size, Color color)
        {
            var image = LegacyUiKit.CreateImage(pointer, name, Vector2.zero, new Vector2(size, size), color);
            image.sprite = NextMapShapes.Triangle;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 125f);
        }

        private void BuildChip(string text, Vector2 position)
        {
            var chip = LegacyUiKit.CreateRect(transform, "Guide Chip", position, Vector2.zero);
            var label = LegacyUiKit.CreateText(chip, "Label", text, Vector2.zero, new Vector2(160f, 26f), 14, ChipInk, FontStyles.Bold, 3);
            var back = LegacyUiKit.CreateImage(chip, "Back", Vector2.zero, new Vector2(label.GetPreferredValues(text).x + 22f, 28f), Gold, 14);
            back.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
        }

        private void Update()
        {
            if (finished) return;
            var preparation = owner != null ? owner.Preparation : null;
            var captain = preparation != null ? FindCaptain(preparation) : null;
            if (captain == null || owner.Grid == null) { Finish(); return; }
            var slot = SlotOf(preparation, captain.MemberId);
            if (slot == CenterSlot) { Finish(); return; }
            var blocked = owner.IsOverlayOpen || BattleSceneFlow.IsOutOfLives;
            SetHidden(blocked);
            if (blocked) return;
            if (transform.GetSiblingIndex() != root.childCount - 1) transform.SetAsLastSibling();
            var now = Time.unscaledTime;
            PulseCard(slot < 0 ? CardFrame(preparation, captain.MemberId) : null, now);
            var landed = false;
            if (PointerPressed()) resumeAt = now + resumeDelay;
            if (now < resumeAt)
            {
                ShowGhost(false);
                clock = 0f;
            }
            else
            {
                clock += Time.unscaledDeltaTime;
                var from = slot < 0 ? LegacyUpgradeScene.WarehouseCellPosition(cardIndex) + EmblemOffset : cellPositions[slot];
                landed = Animate(from, cellPositions[CenterSlot]);
            }
            if (centerCell != null)
                centerCell.color = Color.Lerp(centerColor, Gold, landed ? 0.65f : 0.15f + 0.35f * (Mathf.Sin(now * 6f) + 1f) / 2f);
        }

        private bool Animate(Vector2 from, Vector2 to)
        {
            var lap = glideSeconds + holdSeconds + fadeSeconds + idleSeconds;
            var t = Mathf.Repeat(clock, lap);
            var lift = Vector2.Distance(from, to) > 200f ? 140f : 50f;
            var control = new Vector2((from.x + to.x) / 2f, Mathf.Max(from.y, to.y) + lift);
            var progress = Ease(Mathf.Clamp01(t / glideSeconds));
            var shown = t < glideSeconds + holdSeconds + fadeSeconds;
            var fade = t < glideSeconds + holdSeconds ? 1f : Mathf.Clamp01(1f - (t - glideSeconds - holdSeconds) / fadeSeconds);
            var at = Curve(from, control, to, progress);
            ghost.anchoredPosition = at;
            ghostGroup.alpha = shown ? ghostAlpha * fade : 0f;
            pointer.anchoredPosition = at + new Vector2(15f, -17f);
            pointerGroup.alpha = t < glideSeconds ? 1f : 0f;
            for (var i = 0; i < trail.Count; i++)
            {
                trail[i].rectTransform.anchoredPosition = Curve(from, control, to, Mathf.Max(0f, progress - (i + 1) * 0.035f));
                trail[i].color = new Color(1f, 1f, 1f, shown ? 0.28f * (1f - (i + 1f) / (trail.Count + 1f)) * fade : 0f);
            }
            return t >= glideSeconds && t < glideSeconds + holdSeconds;
        }

        private void ShowGhost(bool value)
        {
            ghostGroup.alpha = value ? ghostAlpha : 0f;
            pointerGroup.alpha = value ? 1f : 0f;
            foreach (var dot in trail) dot.color = new Color(1f, 1f, 1f, 0f);
        }

        private void SetHidden(bool value)
        {
            if (hidden == value) return;
            hidden = value;
            group.alpha = value ? 0f : 1f;
            if (!value) return;
            RestoreCard();
            if (centerCell != null) centerCell.color = centerColor;
        }

        private Image CardFrame(SquadPreparation preparation, int memberId)
        {
            var index = IndexOf(preparation, memberId);
            if (index == cardIndex && cardFrame != null) return cardFrame;
            cardIndex = index;
            var cell = index >= 0 ? root.Find("Warehouse Cell " + index) : null;
            return cell != null ? cell.GetComponent<Image>() : null;
        }

        private void PulseCard(Image frame, float now)
        {
            if (frame != cardFrame)
            {
                RestoreCard();
                cardFrame = frame;
                if (cardFrame != null) cardColor = cardFrame.color;
            }
            if (cardFrame != null) cardFrame.color = Color.Lerp(PulseLow, PulseHigh, (Mathf.Sin(now * 4f) + 1f) / 2f);
        }

        private void RestoreCard()
        {
            if (cardFrame != null) cardFrame.color = cardColor;
            cardFrame = null;
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            RestoreCard();
            if (centerCell != null) centerCell.color = centerColor;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            RestoreCard();
            if (centerCell != null) centerCell.color = centerColor;
        }

        private static bool PointerPressed()
        {
            var device = Pointer.current;
            return device != null && device.press.isPressed;
        }

        private static MemberSpawnDefinition FindCaptain(SquadPreparation preparation)
        {
            foreach (var member in preparation.Members)
                if (member.Unit.Role == UnitRole.Captain) return member;
            return null;
        }

        private static int IndexOf(SquadPreparation preparation, int memberId)
        {
            for (var i = 0; i < preparation.Members.Count; i++)
                if (preparation.Members[i].MemberId == memberId) return i;
            return -1;
        }

        private static int SlotOf(SquadPreparation preparation, int memberId)
        {
            for (var slot = 0; slot < 9; slot++)
                if (preparation.MemberAtSlot(slot) == memberId) return slot;
            return -1;
        }

        private static Vector2 Curve(Vector2 from, Vector2 control, Vector2 to, float t)
        {
            var u = 1f - t;
            return u * u * from + 2f * u * t * control + t * t * to;
        }

        private static float Ease(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
