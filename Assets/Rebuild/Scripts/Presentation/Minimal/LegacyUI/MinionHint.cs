using System.Collections.Generic;
using System.Globalization;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class MinionHint
    {
        private const int SortingOrder = 910;
        private const float FadeSeconds = 0.35f;
        private static readonly HashSet<int> shown = new HashSet<int>();

        private readonly RectTransform canvasRoot;
        private readonly CanvasGroup group;
        private readonly TMP_Text title;
        private readonly TMP_Text line;
        private readonly Image shieldIcon;
        private readonly Image targetIcon;
        private readonly Image fireIcon;
        private readonly Image iceIcon;
        private readonly float seconds;
        private float left;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            shown.Clear();
            BattleSceneFlow.RunStarted -= OnRunStarted;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            BattleSceneFlow.RunStarted -= OnRunStarted;
            BattleSceneFlow.RunStarted += OnRunStarted;
        }

        private static void OnRunStarted() => shown.Clear();

        public MinionHint(Transform parent, float seconds)
        {
            this.seconds = seconds;
            var root = new GameObject("Minion Hint", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            LegacyUiKit.CreateCanvas(root, SortingOrder, new Vector2(1280f, 720f), 0.5f);
            canvasRoot = (RectTransform)root.transform;
            group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var banner = LegacyUiKit.CreateImage(canvasRoot, "Hint Banner", new Vector2(0f, -53f), new Vector2(660f, 90f), new Color(0.07f, 0.12f, 0.19f, 0.92f), 14);
            banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            banner.raycastTarget = false;
            shieldIcon = Icon(banner.transform, "shield");
            targetIcon = Icon(banner.transform, "target");
            fireIcon = ElementIcon(banner.transform, MinionElement.Fire);
            iceIcon = ElementIcon(banner.transform, MinionElement.Ice);
            title = LegacyUiKit.CreateText(banner.transform, "Heading", string.Empty, new Vector2(24f, 18f), new Vector2(560f, 38f), 24, LegacyUiKit.Gold, FontStyles.Bold);
            title.raycastTarget = false;
            line = LegacyUiKit.CreateText(banner.transform, "Help", string.Empty, new Vector2(24f, -19f), new Vector2(570f, 34f), 16, Color.white);
            line.raycastTarget = false;
            root.SetActive(false);
        }

        public bool Show(MinionKind kind, MinionElement element = MinionElement.None)
        {
            var bearer = kind == MinionKind.Bearer;
            if (bearer) element = MinionElement.None;
            var key = (int)kind * 8 + (int)element;
            if (canvasRoot == null || left > 0f || shown.Contains(key)) return false;
            shown.Add(key);
            title.text = bearer ? "SHIELD BEARER" : element == MinionElement.Fire ? "FIRE LOBBER" : element == MinionElement.Ice ? "ICE LOBBER" : "LOBBER";
            line.text = bearer ? "Its tower takes 70% less damage while it stands. Break it first." : LobberLine(element);
            if (shieldIcon != null) shieldIcon.gameObject.SetActive(bearer);
            if (targetIcon != null) targetIcon.gameObject.SetActive(!bearer && element == MinionElement.None);
            if (fireIcon != null) fireIcon.gameObject.SetActive(element == MinionElement.Fire);
            if (iceIcon != null) iceIcon.gameObject.SetActive(element == MinionElement.Ice);
            left = seconds;
            group.alpha = 0f;
            canvasRoot.gameObject.SetActive(true);
            return true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (canvasRoot == null || left <= 0f) return;
            left = Mathf.Max(0f, left - unscaledDeltaTime);
            var shownFor = seconds - left;
            group.alpha = Mathf.Min(Mathf.Clamp01(shownFor / FadeSeconds), Mathf.Clamp01(left / FadeSeconds));
            if (left <= 0f) canvasRoot.gameObject.SetActive(false);
        }

        private static string LobberLine(MinionElement element)
        {
            if (element == MinionElement.Fire)
                return "Its fire bomb lands in the red ring and burns units for " + Seconds(MinionRules.BurnTicks * BattleSimulation.BurnTickSeconds) + ". Move out!";
            if (element == MinionElement.Ice)
                return "Its ice bomb lands in the red ring and freezes units for " + Seconds(MinionRules.FreezeSeconds) + ". Move out!";
            return "A red ring shows where its paint bomb will land. Move out!";
        }

        private static string Seconds(float value) => value.ToString("0.#", CultureInfo.InvariantCulture) + " s";

        private static Image ElementIcon(Transform banner, MinionElement element)
        {
            var sprite = MinionIcons.Of(element);
            if (sprite == null) return null;
            var icon = LegacyUiKit.CreateImage(banner, element + " Icon", new Vector2(-282f, 0f), new Vector2(46f, 46f), LegacyUiKit.Gold);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.gameObject.SetActive(false);
            return icon;
        }

        private static Image Icon(Transform banner, string name)
        {
            var icon = LegacyUiKit.CreateIcon(banner, name + " Icon", name, new Vector2(-282f, 0f), 46f);
            if (icon == null) return null;
            icon.color = LegacyUiKit.Gold;
            icon.raycastTarget = false;
            icon.gameObject.SetActive(false);
            return icon;
        }
    }
}
