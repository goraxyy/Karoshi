using UnityEngine;

// A carried crate of stock. Looking at any slot of a shelf and pressing E fills that
// whole shelf; dropping the crate simply makes it disappear.
public class StockCrate : MonoBehaviour
{
    public GameObject itemPrefab;

    [Tooltip("Crate is used up once it has restocked a shelf.")]
    public bool consumeOnUse = true;

    // Returns how many slots were filled.
    public int StockShelf(ShelfSlot slot, PlayerInteract player)
    {
        if (slot == null) return 0;

        if (itemPrefab == null)
        {
            Debug.LogWarning("StockCrate has no item prefab to stock with.", this);
            return 0;
        }

        int filled = slot.owner != null
            ? slot.owner.FillAll(itemPrefab)
            : (slot.FillWithNewItem(itemPrefab) ? 1 : 0);

        // ShelfUnit updates the task list as its slots fill.
        if (filled > 0 && consumeOnUse && player != null && player.tools != null)
            player.tools.DestroyHeld();

        return filled;
    }
}
