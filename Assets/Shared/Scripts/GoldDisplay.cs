using UnityEngine;
using TMPro;

public class GoldDisplay : MonoBehaviour
{
    [SerializeField] private OffenseData offenseData;
    [SerializeField] private TMP_Text goldText;

    private double lastGold = -1;
    private float pulseTimer = 0f;

    private void Awake()
    {
        if (goldText == null)
            goldText = GetComponent<TMP_Text>();

        if (goldText != null)
        {
            goldText.color = new Color(1f, 0.88f, 0.25f, 1f); // Bright Gold color
            goldText.fontStyle = FontStyles.Bold;
        }
    }

    private void Update()
    {
        if (offenseData == null || goldText == null) return;

        if (lastGold >= 0 && offenseData.gold != lastGold)
        {
            pulseTimer = 0.22f;
        }
        lastGold = offenseData.gold;

        goldText.text = $"${offenseData.gold:N0}";

        if (pulseTimer > 0f)
        {
            pulseTimer -= Time.unscaledDeltaTime;
            float scale = 1f + Mathf.Sin((pulseTimer / 0.22f) * Mathf.PI) * 0.02f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
        else
        {
            transform.localScale = Vector3.one;
        }
    }
}