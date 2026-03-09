using UnityEngine;

public class ShelfSlot : MonoBehaviour, IInteractable
{
    public ItemType requiredType;
    public Transform snapPoint;
    public AudioClip itemDropSound;

    [Header("Snap Rotation")]
    public Vector3 snapRotationOffset = Vector3.zero;

    [HideInInspector] public bool isFilled;
    [HideInInspector] public Item storedItem;

    AudioSource audioSource;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    void Update()
    {
        // Push live rotation changes to the stored item while in play mode
        if (isFilled && storedItem != null)
            storedItem.shelfRotationOffset = snapRotationOffset;
    }

    public void Interact(PlayerInteract player)
    {
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

            if (itemDropSound != null && audioSource != null)
                audioSource.PlayOneShot(itemDropSound);

            TaskManager taskManager = FindFirstObjectByType<TaskManager>();
            if (taskManager != null)
                taskManager.NotifySlotFilled(this);
        }
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

            if (itemDropSound != null && audioSource != null)
                audioSource.PlayOneShot(itemDropSound);

            storedItem = null;
        }

        isFilled = false;
    }

    public string GetPrompt()
    {
        return isFilled ? "Pick up " + requiredType : "Place " + requiredType;
    }
}