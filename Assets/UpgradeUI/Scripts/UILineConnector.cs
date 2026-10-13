using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UILineConnector : MonoBehaviour
{
    private RectTransform rectTransform;
    private Image image;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        rectTransform.pivot = new Vector2(0f, 0.5f);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
    }

    public void Connect(RectTransform parentRect, RectTransform childRect, float thickness = 4f, Color color = default)
    {
        if (rectTransform == null) Awake();

        image.color = color == default ? new Color(1f, 1f, 1f, 0.4f) : color;

        Vector2 start = parentRect.anchoredPosition;
        Vector2 end = childRect.anchoredPosition;
        Vector2 direction = end - start;

        rectTransform.anchoredPosition = start;
        rectTransform.sizeDelta = new Vector2(direction.magnitude, thickness);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}