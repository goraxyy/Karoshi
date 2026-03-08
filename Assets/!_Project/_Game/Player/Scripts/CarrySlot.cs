using UnityEngine;

public class CarrySlot : MonoBehaviour
{
    public Transform holdPoint;
    public Transform stashPoint;   // Hidden child Transform on the Player — items go here when in a non-active slot

    public int activeSlot = 0;
    public Item[] items = new Item[4];

    // These keep ShelfSlot and PickupInteractable working without changes
    public bool IsCarrying => items[activeSlot] != null;
    public Item currentItem => items[activeSlot];

    public bool TryPickup(Item item)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
            {
                items[i] = item;

                if (i == activeSlot)
                    item.SetCarried(true, holdPoint);   // Show at holdPoint
                else
                    item.SetStowed(stashPoint);         // Hide in stash

                return true;
            }
        }

        Debug.Log("Inventory full!");
        return false;
    }

    public Item Drop()
    {
        if (!IsCarrying) return null;

        Item dropped = items[activeSlot];
        items[activeSlot] = null;
        dropped.SetCarried(false, null);
        return dropped;
    }

    public void SetActiveSlot(int index)
    {
        if (index == activeSlot) return;

        // Stow the currently held item
        if (items[activeSlot] != null)
            items[activeSlot].SetStowed(stashPoint);

        activeSlot = index;

        // Pull out the newly active item
        if (items[activeSlot] != null)
            items[activeSlot].SetCarried(true, holdPoint);
    }

    public bool IsFull()
    {
        foreach (var item in items)
            if (item == null) return false;
        return true;
    }
}