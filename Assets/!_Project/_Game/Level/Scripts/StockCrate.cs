using UnityEngine;

// The stock crate. Carried in the inventory like any other item; while it's the item in
// hand, looking at any slot of a shelf and pressing E refills that entire shelf.
// It is never used up — one crate restocks the whole store.
public class StockCrate : MonoBehaviour
{
    [Tooltip("Item spawned into empty shelf slots.")]
    public GameObject itemPrefab;

    // Returns how many slots were filled.
    public int StockShelf(ShelfSlot slot, PlayerInteract player)
    {
        if (slot == null) return 0;

        if (itemPrefab == null)
        {
            Debug.LogWarning("StockCrate has no item prefab to stock with.", this);
            return 0;
        }

        // ShelfUnit refreshes its highlight and the task list as the slots fill.
        return slot.owner != null
            ? slot.owner.FillAll(itemPrefab)
            : (slot.FillWithNewItem(itemPrefab) ? 1 : 0);
    }
}
