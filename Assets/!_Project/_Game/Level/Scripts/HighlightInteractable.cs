using UnityEngine;

// Base for props that glow yellow when looked at and do something on E.
[RequireComponent(typeof(OutlineHighlight))]
public abstract class HighlightInteractable : MonoBehaviour, IInteractable, IHoverable
{
    OutlineHighlight outline;

    protected virtual void Awake()
    {
        outline = GetComponent<OutlineHighlight>();
    }

    public virtual void OnHoverEnter() => SetHighlight(true);
    public virtual void OnHoverExit() => SetHighlight(false);

    protected void SetHighlight(bool on)
    {
        if (outline != null) outline.SetHighlighted(on);
    }

    public abstract void Interact(PlayerInteract player);
    public abstract string GetPrompt();
}
