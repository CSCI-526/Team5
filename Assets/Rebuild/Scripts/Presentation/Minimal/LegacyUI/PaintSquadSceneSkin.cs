using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Restyles the UI that is authored in the scene files (title, end, settlement and upgrade scenes, and the
    // battle HUD's speed bar) into the Paint Squad look at runtime. Only sprites, colors and fonts change:
    // RectTransforms, button listeners and persistent UnityEvents are left as authored. Every method is safe
    // to call more than once.
    public static class PaintSquadSceneSkin
    {
        public const string BackdropName = "Paint Squad Backdrop";
        private const string RibbonSuffix = " Ribbon";

        /// <summary>Title or end scene: hex paper backdrop, outlined logo title, chunky buttons.</summary>
        public static void StyleMenuScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas) continue;
                EnsureBackdrop(canvas);
                foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                {
                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    var text = label != null ? label.text : button.name;
                    var face = text.Contains("Again") ? LegacyUiKit.Green : text.Contains("Test") ? LegacyUiKit.Blue : LegacyUiKit.Red;
                    LegacyUiKit.SkinButton(button, face, 16f);
                    if (label != null) label.text = label.text.Trim().ToUpperInvariant();
                }
                foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.GetComponentInParent<Button>(true) != null) continue;
                    StyleLogo(label);
                }
                AddMascot(canvas);
            }
        }

        // The selected squad's Captain standing in the bottom right corner, like the Paint Squad menu mascot.
        private static void AddMascot(Canvas canvas)
        {
            if (canvas.transform.Find("Paint Squad Mascot") != null) return;
            if (!CharacterSetCatalogs.TryGetSelected(out var entry) || entry.captainPreview == null) return;
            var height = LegacyUiKit.CanvasUnitsHeight(canvas.transform);
            var size = height * 0.42f;
            var mascot = LegacyUiKit.CreateImage(canvas.transform, "Paint Squad Mascot", Vector2.zero, new Vector2(size, size), Color.white);
            mascot.sprite = entry.captainPreview;
            mascot.preserveAspect = true;
            mascot.rectTransform.anchorMin = mascot.rectTransform.anchorMax = mascot.rectTransform.pivot = new Vector2(1f, 0f);
            mascot.rectTransform.anchoredPosition = new Vector2(-size * 0.12f, size * 0.02f);
            // Behind the buttons and titles, in front of the backdrop.
            mascot.transform.SetSiblingIndex(1);
        }

        /// <summary>The settlement scene: cream reward card, title ribbon (red for a win, slate for a loss), green Continue.</summary>
        public static void StyleSettlement(Scene scene, TMP_Text missionHeading, TMP_Text rewardHeading, TMP_Text rewardAmount,
            TMP_Text rewardDescription, bool victory)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.isRootCanvas) EnsureBackdrop(canvas);
            if (missionHeading != null)
            {
                missionHeading.color = Color.white;
                LegacyUiKit.StyleText(missionHeading, true, 2f);
                missionHeading.ForceMeshUpdate();
                var ribbon = EnsureRibbon(missionHeading, victory ? LegacyUiKit.Red : LegacyUiKit.Slate);
                // The ribbon goes right behind the heading.
                if (ribbon.transform.GetSiblingIndex() > missionHeading.transform.GetSiblingIndex())
                    ribbon.transform.SetSiblingIndex(missionHeading.transform.GetSiblingIndex());
            }
            var panel = rewardHeading != null ? rewardHeading.transform.parent as RectTransform : null;
            if (panel != null && panel.GetComponent<Image>() is Image card)
            {
                card.color = LegacyUiKit.CardColor;
                LegacyUiKit.Round(card, 22f);
                if (LegacyUiKit.Skin != null && LegacyUiKit.Skin.panel != null) { card.sprite = LegacyUiKit.Skin.panel; card.pixelsPerUnitMultiplier = LegacyUiKit.SkinCornerRadius / 22f; }
                EnsureShadow(panel);
            }
            Style(rewardHeading, LegacyUiKit.InkMuted, true);
            Style(rewardAmount, victory ? LegacyUiKit.GoldDark : LegacyUiKit.Red, true);
            Style(rewardDescription, LegacyUiKit.Ink, false);
            var scope = missionHeading != null ? missionHeading.canvas : null;
            if (scope != null)
                foreach (var button in scope.GetComponentsInChildren<Button>(true))
                {
                    LegacyUiKit.SkinButton(button, LegacyUiKit.Green, 16f);
                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    if (label != null) label.text = label.text.Trim().ToUpperInvariant();
                }
        }

        /// <summary>The upgrade scene's authored Start Wave button and its canvas backdrop.</summary>
        public static void StyleUpgradeScene(Button startWave)
        {
            if (startWave == null) return;
            var canvas = startWave.GetComponentInParent<Canvas>(true);
            if (canvas != null) EnsureBackdrop(canvas.rootCanvas);
            LegacyUiKit.SkinButton(startWave, LegacyUiKit.Green, 18f);
            foreach (var label in startWave.GetComponentsInChildren<TMP_Text>(true))
                if (label.text.Trim() == "Start Wave") label.text = "START WAVE";
        }

        /// <summary>The battle HUD's authored speed bar: pill track, gold fill, white knob and outlined labels.</summary>
        public static void StyleSpeedBar(Slider speed)
        {
            if (speed == null) return;
            LegacyUiKit.SkinSlider(speed);
            foreach (var label in speed.GetComponentsInChildren<TMP_Text>(true))
            {
                label.color = Color.white;
                LegacyUiKit.StyleText(label, true);
                // Keep the authored glyph spacing of "1x     2x     3x" so each label stays over its notch.
                label.characterSpacing = 0f;
                label.wordSpacing = 0f;
            }
        }

        /// <summary>A beige hex paper image behind everything else on the canvas.</summary>
        public static Image EnsureBackdrop(Canvas canvas)
        {
            if (canvas == null) return null;
            var existing = canvas.transform.Find(BackdropName);
            if (existing != null) return existing.GetComponent<Image>();
            var backdrop = LegacyUiKit.CreateBackdrop(canvas.transform, BackdropName);
            backdrop.transform.SetAsFirstSibling();
            return backdrop;
        }

        private static void StyleLogo(TMP_Text label)
        {
            label.color = Color.white;
            LegacyUiKit.StyleText(label, true, 3f);
            // Fit the chunky display font to the authored title box.
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSizeMax = label.fontSize * 1.3f;
            label.fontSizeMin = label.fontSize * 0.5f;
            label.enableAutoSizing = true;
        }

        private static void Style(TMP_Text label, Color color, bool heading)
        {
            if (label == null) return;
            label.color = color;
            LegacyUiKit.StyleText(label, heading);
        }

        private static Image EnsureRibbon(TMP_Text heading, Color color)
        {
            var parent = heading.transform.parent;
            var name = heading.name + RibbonSuffix;
            var existing = parent.Find(name);
            Image ribbon = existing != null ? existing.GetComponent<Image>() : null;
            if (ribbon == null)
            {
                var rect = heading.rectTransform;
                var height = heading.fontSize * 1.9f;
                var width = heading.preferredWidth + height * 2.2f;
                ribbon = LegacyUiKit.CreateRibbon(parent, name, string.Empty, Vector2.zero, new Vector2(width, height), color);
                var ribbonRect = ribbon.rectTransform;
                ribbonRect.anchorMin = rect.anchorMin;
                ribbonRect.anchorMax = rect.anchorMax;
                ribbonRect.pivot = new Vector2(0.5f, 0.5f);
                var center = rect.anchoredPosition + Vector2.Scale(new Vector2(0.5f, 0.5f) - rect.pivot, rect.rect.size);
                ribbonRect.anchoredPosition = center + new Vector2(0f, -height * 0.06f);
            }
            ribbon.color = color;
            return ribbon;
        }

        private static void EnsureShadow(RectTransform target)
        {
            if (target.parent == null || target.parent.Find(target.name + " Shadow") != null) return;
            LegacyUiKit.CreateShadow(target);
        }
    }
}
