using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // One shared mapping for visible keycaps, tooltip text, and keyboard input.
    public static class BattleHotkeys
    {
        public const string PauseLabel = "Space";
        public const int SkillSlotCount = 4;
        public static string SkillLabel(int zeroBasedSlot) =>
            zeroBasedSlot >= 0 && zeroBasedSlot < SkillSlotCount ? (zeroBasedSlot + 1).ToString() : "";

        internal static bool CanReadKeyboard => Application.isFocused && Keyboard.current != null &&
            Time.timeScale > 0f && !EditingText();

        private static bool EditingText()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var tmp = selected.GetComponentInParent<TMP_InputField>();
            var legacy = selected.GetComponentInParent<InputField>();
            return (tmp != null && tmp.isFocused) || (legacy != null && legacy.isFocused);
        }

        internal static bool SkillPressed(int slot)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (slot)
            {
                case 0: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
                case 1: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
                case 2: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
                case 3: return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
                default: return false;
            }
        }

        internal static Image CreateKeycap(RectTransform parent, string key, Vector2 anchor,
            Vector2 pivot, Vector2 offset, Vector2 size, float fontSize)
        {
            var frame = LegacyUiKit.CreateImage(parent, "Hotkey " + key, Vector2.zero, size,
                new Color(.83f, .80f, .68f, .98f), 3f);
            var rect = frame.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            var face = LegacyUiKit.CreateImage(rect, "Key face", Vector2.zero,
                size - Vector2.one * 2f, new Color(.13f, .19f, .25f, .98f), 2f);
            var label = LegacyUiKit.CreateText(face.transform, "Key", key, Vector2.zero,
                size - Vector2.one * 2f, fontSize, Color.white, FontStyles.Bold);
            frame.raycastTarget = face.raycastTarget = label.raycastTarget = false;
            rect.SetAsLastSibling();
            return frame;
        }

        internal static bool PointerOn(RectTransform rect, Vector2 screen)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;
            var canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled) return false;
            var root = canvas.rootCanvas;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screen,
                root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera);
        }
    }
}
