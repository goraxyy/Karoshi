using UnityEngine;

// A patch of mess a customer left behind. Cleaned by holding E while carrying the mop;
// the patch visibly shrinks and fades as the mopping progresses.
public class Dirt : HighlightInteractable, IHoldInteractable
{
    [Header("Cleaning")]
    public float secondsToClean = 3f;
    [Range(0f, 1f)] public float minScaleWhenAlmostClean = 0.15f;

    // How many spills are on the floor right now — the mopping task reads this directly.
    public static int ActiveCount { get; private set; }

    Vector3 fullScale;
    MaterialPropertyBlock propertyBlock;
    Renderer patchRenderer;
    Color baseColour;

    void OnEnable()
    {
        ActiveCount++;
        TaskManager.NotifyWorldChanged();
    }

    void OnDisable()
    {
        ActiveCount = Mathf.Max(0, ActiveCount - 1);
        TaskManager.NotifyWorldChanged();
    }

    protected override void Awake()
    {
        base.Awake();
        fullScale = transform.localScale;

        patchRenderer = GetComponentInChildren<Renderer>();
        if (patchRenderer != null)
        {
            propertyBlock = new MaterialPropertyBlock();
            baseColour = patchRenderer.sharedMaterial != null && patchRenderer.sharedMaterial.HasProperty("_BaseColor")
                ? patchRenderer.sharedMaterial.GetColor("_BaseColor")
                : Color.white;
        }
    }

    public float HoldDuration => secondsToClean;

    // Only moppable while the mop is the item currently in hand.
    public bool CanHold(PlayerInteract player)
    {
        if (player == null || player.carrySlot == null) return false;
        return player.carrySlot.IsCarrying && player.carrySlot.currentItem.type == ItemType.Mop;
    }

    public void OnHoldProgress(float normalised)
    {
        ApplyProgress(normalised);
    }

    public void OnHoldCancelled()
    {
        ApplyProgress(0f);
    }

    public void OnHoldComplete(PlayerInteract player)
    {
        // Counts toward the shift's mopping quota; OnDisable refreshes the rest.
        TaskManager tasks = TaskManager.Instance;
        if (tasks != null) tasks.ReportMopped();

        Destroy(gameObject);
    }

    void ApplyProgress(float normalised)
    {
        float remaining = Mathf.Lerp(1f, minScaleWhenAlmostClean, normalised);
        transform.localScale = fullScale * remaining;

        if (patchRenderer == null || propertyBlock == null) return;

        // Fade out alongside the shrink so progress reads clearly on dark floors too.
        Color faded = baseColour;
        faded.a = Mathf.Lerp(baseColour.a, 0f, normalised);
        patchRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", faded);
        patchRenderer.SetPropertyBlock(propertyBlock);
    }

    public override void Interact(PlayerInteract player)
    {
        // Cleaning happens through the hold interface; a tap does nothing.
    }

    public override string GetPrompt() => "Hold E with the mop to clean";
}
