using UnityEngine;

public class PickupInteractable : MonoBehaviour, IInteractable
{
    Item item;

    void Awake()
    {
        item = GetComponent<Item>();
    }

    public void Interact(PlayerInteract player)
    {
        if (item != null && player.carrySlot != null)
        {
            if (player.carrySlot.TryPickup(item))
            {
                // Optional: play pickup sound
            }
        }
    }

    public string GetPrompt()
    {
        return "Pick up " + (item != null ? item.type.ToString() : "item");
    }
}
