using UnityEngine;

// Refills the burnout bar. Goes on cooldown briefly after each cup.
public class CoffeeMachine : HighlightInteractable
{
    [Tooltip("Seconds before another cup can be poured.")]
    public float cooldownTime = 5f;
    public AudioClip pourSound;

    float lastUseTime = -999f;
    BurnoutSystem burnout;

    protected override void Awake()
    {
        base.Awake();
        burnout = FindAnyObjectByType<BurnoutSystem>();
    }

    bool OnCooldown => Time.time - lastUseTime < cooldownTime;

    public override void Interact(PlayerInteract player)
    {
        if (OnCooldown) return;

        if (burnout == null) burnout = FindAnyObjectByType<BurnoutSystem>();
        if (burnout == null)
        {
            Debug.LogWarning("No BurnoutSystem in the scene for the coffee machine to refill.", this);
            return;
        }

        burnout.DrinkCoffee();
        lastUseTime = Time.time;
        OneShotAudio.PlayAt(pourSound, transform.position);
    }

    public override string GetPrompt()
    {
        if (OnCooldown)
            return $"Brewing... {(int)(cooldownTime - (Time.time - lastUseTime)) + 1}s";

        return "Drink coffee";
    }
}
