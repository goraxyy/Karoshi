using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BurnoutSystem : MonoBehaviour
{
    [Header("Burnout Settings")]
    [Range(0f, 1f)] public float currentBurnout;
    public float burnoutPerMinute = 0.04f;
    public float burnoutDuringChase = 0.03f;
    public float coffeeRecovery = 0.25f;

    [Header("Visual Effects")]
    public Volume globalVolume;
    public float maxVignetteIntensity = 0.6f;

    Vignette vignette;
    bool isInChase;

    void Awake()
    {
        if (globalVolume != null && globalVolume.profile != null)
        {
            if (!globalVolume.profile.TryGet(out vignette))
                Debug.LogError("Vignette override not found in Volume Profile!");
        }
    }

    void Update()
    {
        // Increase burnout over time
        currentBurnout += (burnoutPerMinute / 60f) * Time.deltaTime;

        // Extra burnout during chase
        if (isInChase)
            currentBurnout += burnoutDuringChase * Time.deltaTime;

        currentBurnout = Mathf.Clamp01(currentBurnout);

        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(0.15f, maxVignetteIntensity, currentBurnout);
        }

        // Optional: adjust camera far clip plane for view distance reduction
        Camera mainCam = Camera.main;
        if (mainCam != null)
            mainCam.farClipPlane = Mathf.Lerp(50f, 20f, currentBurnout);
    }

    public void SetChaseState(bool chasing)
    {
        isInChase = chasing;
    }

    public void DrinkCoffee()
    {
        currentBurnout = Mathf.Clamp01(currentBurnout - coffeeRecovery);
        Debug.Log("Coffee consumed! Burnout reduced.");
    }

    public void ResetForNewShift(int shiftIndex)
    {
        // Baseline increases each shift
        currentBurnout = Mathf.Clamp01(0.1f + (0.1f * shiftIndex));
        isInChase = false;
    }
}
