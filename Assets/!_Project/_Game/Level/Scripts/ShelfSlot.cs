using System.Collections.Generic;
using UnityEngine;

public class ShelfSlot : MonoBehaviour, IInteractable
{
    // Live registry of every enabled slot. With thousands of slots in a level,
    // FindObjectsByType<ShelfSlot>() is far too expensive to call per shift — let alone
    // per frame, which EnemyAI used to do while sabotaging.
    static readonly List<ShelfSlot> all = new List<ShelfSlot>();
    public static IReadOnlyList<ShelfSlot> All => all;

    public ItemType requiredType;
    public Transform snapPoint;
    public AudioClip itemDropSound;

    [Header("Snap Rotation")]
    public Vector3 snapRotationOffset = Vector3.zero;

    [HideInInspector] public bool isFilled;
    [HideInInspector] public Item storedItem;

    // Set by ShelfUnit.Awake so the shelf can track how many of its slots are empty.
    [HideInInspector] public ShelfUnit owner;

    int registryIndex = -1;

    void OnEnable()
    {
        registryIndex = all.Count;
        all.Add(this);
    }

    void OnDisable()
    {
        if (registryIndex < 0) return;

        // Swap-remove keeps deregistration O(1); order in the registry doesn't matter.
        int last = all.Count - 1;
        if (registryIndex != last)
        {
            all[registryIndex] = all[last];
            all[registryIndex].registryIndex = registryIndex;
        }
        all.RemoveAt(last);
        registryIndex = -1;
    }

    public void Interact(PlayerInteract player)
    {
        // Holding the stock crate restocks the whole shelf in one go.
        if (player.carrySlot != null && player.carrySlot.IsCarrying)
        {
            StockCrate crate = player.carrySlot.currentItem.GetComponent<StockCrate>();
            if (crate != null)
            {
                crate.StockShelf(this, player);
                return;
            }
        }

        if (isFilled)
        {
            // Pick item up from shelf
            if (player.carrySlot.IsFull())
            {
                Debug.Log("Inventory full!");
                return;
            }

            storedItem.SetCarried(false, null);
            player.carrySlot.TryPickup(storedItem);
            storedItem = null;
            isFilled = false;
            if (owner != null) owner.OnSlotEmptied();
        }
        else
        {
            // Place item on shelf
            if (!player.carrySlot.IsCarrying) return;

            Item heldItem = player.carrySlot.currentItem;
            if (heldItem.type != requiredType)
            {
                Debug.Log("Wrong item type!");
                return;
            }

            player.carrySlot.Drop();
            heldItem.SetOnShelf(snapPoint, snapRotationOffset);
            storedItem = heldItem;
            isFilled = true;
            if (owner != null) owner.OnSlotFilled();

            OneShotAudio.PlayAt(itemDropSound, transform.position);
        }
    }

    // Called by customers taking an item off the shelf. Hands over the stored item and
    // leaves the slot empty, so restocking it becomes work for the player.
    public Item TakeItem()
    {
        if (!isFilled || storedItem == null) return null;

        Item taken = storedItem;
        storedItem = null;
        isFilled = false;
        if (owner != null) owner.OnSlotEmptied();
        return taken;
    }

    // Spawns a brand new item straight into this slot — used when restocking from a crate.
    public bool FillWithNewItem(GameObject itemPrefab)
    {
        if (isFilled || itemPrefab == null || snapPoint == null) return false;

        GameObject spawned = Object.Instantiate(itemPrefab);
        Item item = spawned.GetComponent<Item>();
        if (item == null) { Object.Destroy(spawned); return false; }

        item.SetOnShelf(snapPoint, snapRotationOffset);
        storedItem = item;
        isFilled = true;
        if (owner != null) owner.OnSlotFilled();
        return true;
    }

    // Called by EnemyAI to knock item off shelf
    public void Eject()
    {
        if (!isFilled) return;

        if (storedItem != null)
        {
            storedItem.SetCarried(false, null);

            Rigidbody rb = storedItem.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(transform.forward * 2f + Vector3.up * 0.5f, ForceMode.Impulse);

            OneShotAudio.PlayAt(itemDropSound, transform.position);

            storedItem = null;
        }

        isFilled = false;
        if (owner != null) owner.OnSlotEmptied();
    }

    public string GetPrompt()
    {
        return isFilled ? "Pick up " + requiredType : "Place " + requiredType;
    }

#if UNITY_EDITOR
    // Replaces the old per-frame Update() that pushed snapRotationOffset into the stored
    // item every frame for every slot. This fires only when the value is edited.
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (isFilled && storedItem != null)
        {
            storedItem.shelfRotationOffset = snapRotationOffset;
            storedItem.ApplyShelfTransform();
        }
    }
#endif
}
