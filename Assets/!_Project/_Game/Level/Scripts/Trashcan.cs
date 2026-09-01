using System.Collections.Generic;
using UnityEngine;

// Fills up as customers use it. Once full the player can carry it to a container and empty it.
public class Trashcan : HighlightInteractable, IHomeReturnable
{
    // Registry so customers can find a bin without scanning the scene.
    static readonly List<Trashcan> all = new List<Trashcan>();
    public static IReadOnlyList<Trashcan> All => all;

    [Header("Capacity")]
    public int capacity = 5;
    [SerializeField] int usageCount;

    [Header("Carrying")]
    public Vector3 holdPosition = new Vector3(0.4f, -0.5f, 0.8f);
    public Vector3 holdRotation = Vector3.zero;

    [Header("Fill Feedback")]
    [Tooltip("How much the bin visually swells as it fills, as a fraction of its size.")]
    public float fullBulge = 0.12f;

    public bool IsFull => usageCount >= capacity;
    public int UsageCount => usageCount;

    Vector3 homePosition;
    Quaternion homeRotation;
    Vector3 baseScale;

    protected override void Awake()
    {
        base.Awake();
        homePosition = transform.position;
        homeRotation = transform.rotation;
        baseScale = transform.localScale;
        ApplyFillVisual();
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    // Called by a customer that has finished using the bin.
    public void RegisterUse()
    {
        usageCount = Mathf.Min(capacity, usageCount + 1);
        ApplyFillVisual();
        TaskManager.NotifyWorldChanged();
    }

    public override void Interact(PlayerInteract player)
    {
        if (player.tools == null) return;

        // Carrying it already? Put it back where it belongs.
        if (player.tools.HeldTool == gameObject)
        {
            player.tools.ReleaseTo(homePosition, homeRotation);
            return;
        }

        if (player.tools.IsHolding) return;
        if (usageCount < 1) return;   // nothing in it yet; it fills to `capacity` at most

        player.tools.Hold(gameObject, holdPosition, holdRotation);
    }

    // Called by the container when the bin is tipped out.
    public void Empty()
    {
        usageCount = 0;
        ApplyFillVisual();
        TaskManager.NotifyWorldChanged();
    }

    public void ReturnHome()
    {
        transform.SetParent(null, true);
        transform.position = homePosition;
        transform.rotation = homeRotation;
    }

    void ApplyFillVisual()
    {
        // Capture lazily and never trust a zero: if this ever runs before Awake (an editor
        // script poking at it, say) a zero baseScale would shrink the bin out of existence.
        if (baseScale.sqrMagnitude < 0.0001f)
        {
            baseScale = transform.localScale;
            if (baseScale.sqrMagnitude < 0.0001f) baseScale = Vector3.one;
        }

        float fill = capacity > 0 ? (float)usageCount / capacity : 0f;
        transform.localScale = baseScale * (1f + fullBulge * fill);
    }

    public override string GetPrompt()
    {
        if (usageCount >= 1) return $"Take out the trash ({usageCount}/{capacity})";
        return "Trash is empty";
    }
}

// Anything the player can put back exactly where it started.
public interface IHomeReturnable
{
    void ReturnHome();
}

// The big skip out back. Empty a carried trashcan into it, and the bin goes back to its spot.
public class TrashContainer : HighlightInteractable
{
    public AudioClip emptySound;

    public override void Interact(PlayerInteract player)
    {
        if (player.tools == null) return;

        Trashcan can = player.tools.GetHeld<Trashcan>();
        if (can == null) return;

        player.tools.Release();
        can.Empty();          // updates the task list
        can.ReturnHome();

        OneShotAudio.PlayAt(emptySound, transform.position);
    }

    public override string GetPrompt() => "Empty trash here";
}
