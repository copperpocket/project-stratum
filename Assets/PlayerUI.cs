using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your TacticalPlayer object here.")]
    public TacticalPlayer player;

    [Tooltip("Drag the Bar_Fill Image component here.")]
    public RectTransform staminaFillRect;

    [Header("Visual Tuning")]
    [Tooltip("The color of the bar when stamina is high.")]
    public Color fullColor = Color.green;
    [Tooltip("The color of the bar when stamina is dangerously low.")]
    public Color lowColor = Color.red;

    private Image fillImage;

    void Start()
    {
        if (staminaFillRect != null)
        {
            fillImage = staminaFillRect.GetComponent<Image>();
        }
    }

    void Update()
    {
        if (player == null || staminaFillRect == null) return;

        // Calculate current stamina percentage safely using standard words for comparisons
        float staminaPct = Mathf.Clamp01(player.currentStamina / player.maxStamina);

        // Map percentage directly to the X local scale of our pivoted bar
        staminaFillRect.localScale = new Vector3(staminaPct, 1f, 1f);

        // Dynamic flair: Blend the color from green to red based on how empty it is
        if (fillImage != null)
        {
            fillImage.color = Color.Lerp(lowColor, fullColor, staminaPct);
        }
    }
}
