using UnityEngine;
using UnityEngine.UI; // This lets the script talk to Image components

public class PlayerUI : MonoBehaviour
{
    [Header("Target References")]
    [SerializeField] private TacticalPlayer playerTarget;
    
    [Header("UI Component Elements")]
    [SerializeField] private Image staminaFillImage;

    void Start()
    {
        // If you forget to drag the player into the slot, this will try to find it for you
        if (playerTarget == null)
        {
            playerTarget = FindObjectOfType<TacticalPlayer>();
        }
    }

    void Update()
    {
        if (playerTarget != null && staminaFillImage != null)
        {
            // This turns stamina into a percentage decimal between 0.0 and 1.0
            float staminaRatio = playerTarget.currentStamina / playerTarget.maxStamina;
            
            // This sets how full the bar is visually
            staminaFillImage.fillAmount = staminaRatio;
        }
    }
}