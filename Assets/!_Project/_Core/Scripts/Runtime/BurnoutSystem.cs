using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Energy runs down over the course of a shift and is topped back up with coffee.
// Empty means no more sprinting, and the edges of the screen close in as it drops.
public class BurnoutSystem : MonoBehaviour
{
    [Header("Energy")]
    [Range(0f, 1f)]
    [Tooltip("1 = fresh, 0 = burnt out.")]
    public float energy = 1f;

    [Tooltip("How much of the bar drains per minute of ordinary work.")]
    public float drainPerMinute = 0.2f;

    [Tooltip("Running burns energy this many times faster.")]
    public float sprintDrainMultiplier = 2f;

    [Tooltip("Extra drain per second while the supervisor is chasing.")]
    public float extraDrainDuringChase = 0.03f;

    [Tooltip("How much one coffee puts back.")]
    public float coffeeRefill = 0.35f;

    [Tooltip("Only drain while a shift is running, so setup time isn't punished.")]
    public bool onlyDrainDuringShift = true;

    [Header("Shift start")]
    [Tooltip("Each shift starts a little more tired than the last.")]
    public float startingEnergyLossPerShift = 0.1f;

    [Header("Vision")]
    public Volume globalVolume;

    [Range(0f, 1f)]
    [Tooltip("Your sight is untouched until energy falls to this. 0.5 = half the bar.")]
    public float fadeStartEnergy = 0.5f;

    [Tooltip("Vignette above the threshold — normally none at all.")]
    public float restedVignette = 0f;

    [Tooltip("Vignette when completely burnt out.")]
    public float burntOutVignette = 0.85f;

    [Range(0.01f, 1f)]
    [Tooltip("Lower is a harder edge to the darkness creeping in.")]
    public float vignetteSmoothness = 0.28f;

    public float Energy01 => Mathf.Clamp01(energy);

    // The whole point of the mechanic: run out and you can't run.
    public bool CanSprint => energy > 0f;

    Vignette vignette;
    ShiftManager shiftManager;
    PlayerMotor playerMotor;
    bool isInChase;

    void Awake()
    {
        if (globalVolume == null) globalVolume = FindAnyObjectByType<Volume>();
        shiftManager = FindAnyObjectByType<ShiftManager>();
        playerMotor = FindAnyObjectByType<PlayerMotor>();

        if (globalVolume != null && globalVolume.profile != null)
        {
            // .profile (not sharedProfile) gives this Volume its own runtime copy,
            // so driving the vignette never writes back into the project asset.
            if (globalVolume.profile.TryGet(out vignette))
            {
                // A parameter only reaches the renderer when its override is switched on.
                vignette.active = true;
                vignette.intensity.overrideState = true;
                vignette.color.overrideState = true;
                vignette.smoothness.overrideState = true;
                vignette.color.value = Color.black;
            }
            else
            {
                Debug.LogWarning("No Vignette override on the Volume profile; vision fade is off.", this);
            }
        }
    }

    void Update()
    {
        if (ShouldDrain())
        {
            float perSecond = drainPerMinute / 60f;

            // Running wears you out faster.
            if (playerMotor != null && playerMotor.IsSprinting())
                perSecond *= sprintDrainMultiplier;

            energy -= perSecond * Time.deltaTime;

            if (isInChase)
                energy -= extraDrainDuringChase * Time.deltaTime;

            energy = Mathf.Clamp01(energy);
        }

        UpdateVisuals();
    }

    bool ShouldDrain()
    {
        if (!onlyDrainDuringShift) return true;
        return shiftManager != null && shiftManager.IsShiftActive;
    }

    void UpdateVisuals()
    {
        if (vignette == null) return;

        // Nothing happens while you are over the threshold; past it the dark closes
        // in fast, so the second half of a shift is where you feel it.
        float fade = Mathf.InverseLerp(fadeStartEnergy, 0f, Energy01);

        vignette.intensity.value = Mathf.Lerp(restedVignette, burntOutVignette, fade);
        vignette.smoothness.value = vignetteSmoothness;
    }

    public void SetChaseState(bool chasing)
    {
        isInChase = chasing;
    }

    public void DrinkCoffee()
    {
        energy = Mathf.Clamp01(energy + coffeeRefill);
    }

    public void ResetForNewShift(int shiftIndex)
    {
        energy = Mathf.Clamp01(1f - startingEnergyLossPerShift * Mathf.Max(0, shiftIndex));
        isInChase = false;
    }
}
