using UnityEngine;

// The breaker panel out the back. When the mains are down this is the only way to
// bring the lights — and the radio — back.
public class ElectricBox : HighlightInteractable
{
    public AudioClip switchSound;

    [Tooltip("What the prompt says when there is nothing to fix.")]
    public string idlePrompt = "Breakers are on";
    public string resetPrompt = "Flip the breakers";

    public override void Interact(PlayerInteract player)
    {
        PowerSystem power = PowerSystem.Instance;
        if (power == null || power.HasPower) return;

        power.RestorePower();
        OneShotAudio.PlayAt(switchSound, transform.position);
    }

    public override string GetPrompt()
    {
        PowerSystem power = PowerSystem.Instance;
        if (power == null) return string.Empty;

        return power.HasPower ? idlePrompt : resetPrompt;
    }
}
