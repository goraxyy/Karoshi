using System.Collections.Generic;
using Kehai.Aiko;
using UnityEngine;

public class ShelfSlot : MonoBehaviour, IInteractable
{
    // Live registry of every enabled slot. With thousands of slots in a level,
    // FindObjectsByType<ShelfSlot>() is far too expensive to call per shift — let alone
    // per frame, which the old enemy AI used to do while sabotaging.
    static readonly List<ShelfSlot> all = new List<ShelfSlot>();
    public static IReadOnlyList<ShelfSlot> All => all;

    [Tooltip("The section this facing belongs to. Only items from the same section fit.")]
    public ItemType requiredType;

    [Tooltip("The planogram: which ProductCatalog id this facing is stocked with. " +
             "Within a section any item still fits — this is what the shelf *should* hold, " +
             "and it's what a customer asks for by name. Set by Kehai/Store/Apply Layout.")]
    public string productId;

    public Transform snapPoint;
    public AudioClip itemDropSound;

    // The SKU stocked here, or null on a facing that has only been given a section.
    public ProductDef Product => ProductCatalog.Get(productId);

    // What to call whatever belongs here: the product if there is one, the section if not.
    public string Label => ProductCatalog.Label(requiredType, productId);

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

            Item taken = storedItem;
            storedItem.SetCarried(false, null);
            player.carrySlot.TryPickup(storedItem);
            storedItem = null;
            isFilled = false;
            if (owner != null) owner.OnSlotEmptied();
            GameEvents.RaisePlayerTookFromShelf(this, taken);
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
            NoiseBus.Emit(transform.position, 0.45f, NoiseKind.Stocking, NoiseAuthor.Player);
            GameEvents.RaisePlayerShelvedItem(this, heldItem);
            if (owner != null && owner.IsFull) GameEvents.RaiseShelfRestocked(owner, 1);
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

        // One placeholder prefab restocks the whole store, so the spawned item takes on
        // this facing's identity. Without this every restocked shelf in the building would
        // fill up with cereal, whatever its sign said.
        item.type = requiredType;
        item.productId = productId;

        item.SetOnShelf(snapPoint, snapRotationOffset);
        storedItem = item;
        isFilled = true;
        if (owner != null) owner.OnSlotFilled();
        return true;
    }

    // Called by Aiko to knock an item off the shelf (her shelf sweep)
    public void Eject()
    {
        if (!isFilled) return;

        if (storedItem != null)
        {
            storedItem.SetCarried(false, null);
            storedItem.lastAuthor = NoiseAuthor.Aiko;

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
        if (isFilled)
            return "Pick up " + (storedItem != null ? storedItem.DisplayName : Label);

        return "Place " + Label;
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
