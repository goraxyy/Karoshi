using UnityEngine;

public class CarrySlot : MonoBehaviour
{
    public Transform holdPoint;

    [HideInInspector] public Item currentItem;

    public bool IsCarrying => currentItem != null;

    public bool TryPickup(Item item)
    {
        if (IsCarrying) return false;

        currentItem = item;
        currentItem.SetCarried(true, holdPoint);
        return true;
    }

    public Item Drop()
    {
        if (!IsCarrying) return null;

        Item droppedItem = currentItem;
        currentItem = null;
        droppedItem.SetCarried(false, null);
        return droppedItem;
    }
}
