using UnityEngine;

public class PickupInteractable : MonoBehaviour, IInteractable, IHoverable
{
    Item item;
    OutlineHighlight outline;

    void Awake()
    {
        item = GetComponent<Item>();
        outline = GetComponent<OutlineHighlight>();
    }

    public void Interact(PlayerInteract player)
    {
        if (item != null && player.carrySlot != null)
        {
            // Already in hand or stowed — don't hand out a second copy.
            if (item.isCarried || player.carrySlot.Contains(item)) return;

            if (player.carrySlot.TryPickup(item))
            {
                // Picked up — make sure the highlight doesn't linger in the player's hands.
                if (outline != null) outline.SetHighlighted(false);
            }
        }
    }

    // Only items that carry an OutlineHighlight (like the mop) glow; plain stock does nothing.
    public void OnHoverEnter()
    {
        if (outline != null) outline.SetHighlighted(true);
    }

    public void OnHoverExit()
    {
        if (outline != null) outline.SetHighlighted(false);
    }

    public string GetPrompt()
    {
        return "Pick up " + (item != null ? item.type.ToString() : "item");
    }
}
