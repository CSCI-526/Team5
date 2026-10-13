using TMPro;
using UnityEngine;

/// <summary>Uses the scene's existing font and canvas without modifying scene assets.</summary>
public static class PreviousRunSummary
{
    public static void Create(OffenseData data, TMP_Text style, bool endScreen = false)
    {
        if (data == null || !data.RunStats.HasPreviousRun || style == null) return;
        Canvas canvas = style.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var summary = new GameObject("Previous Run Summary", typeof(RectTransform), typeof(TextMeshProUGUI));
        summary.transform.SetParent(canvas.transform, false);
        var text = summary.GetComponent<TextMeshProUGUI>();
        text.font = style.font;
        text.fontSharedMaterial = style.fontSharedMaterial;
        text.color = style.color;
        text.raycastTarget = false;
        text.fontSize = endScreen ? 14 : 24;
        text.alignment = endScreen ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = endScreen ? new Vector2(0.5f, 0.5f) : new Vector2(0, 1);
        rect.pivot = endScreen ? new Vector2(0.5f, 1) : new Vector2(0, 1);
        // Leave room for How to Play on UpgradeScene; on EndGame place below Play Again.
        rect.anchoredPosition = endScreen ? new Vector2(0, -75) : new Vector2(20, -80);
        rect.sizeDelta = new Vector2(endScreen ? 460 : 620, endScreen ? 55 : 100);
        var stats = data.RunStats;
        text.text = $"Previous run — {(stats.PreviousSucceeded ? "Success" : "Failure")}\n" +
                    $"Damage dealt: {stats.PreviousDamage:0.##}\nGold earned: {stats.PreviousGold:0.##}";
    }
}
