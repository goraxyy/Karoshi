using UnityEngine;

public class ShelfSlot : MonoBehaviour, IInteractable
{
    public ItemType requiredType;
    public Transform snapPoint;
    public AudioClip itemDropSound;

    [HideInInspector] public bool isFilled;

    AudioSource audioSource;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // Full 3D sound
    }

    public void Interact(PlayerInteract player)
    {
        if (isFilled) return;
        if (!player.carrySlot.IsCarrying) return;

        Item heldItem = player.carrySlot.currentItem;
        if (heldItem.type != requiredType)
        {
            Debug.Log("Wrong item type!");
            return;
        }

        // Place item
        player.carrySlot.Drop();
        heldItem.SetCarried(true, snapPoint);
        isFilled = true;

        // Notify task system
        TaskManager taskManager = FindFirstObjectByType<TaskManager>();
        if (taskManager != null)
            taskManager.NotifySlotFilled(this);
    }

    public void Eject()
    {
        if (!isFilled) return;

        Item item = snapPoint.GetComponentInChildren<Item>();
        if (item != null)
        {
            item.SetCarried(false, null);

            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(transform.forward * 2f + Vector3.up * 0.5f, ForceMode.Impulse);

            // Play 3D sound
            if (itemDropSound != null && audioSource != null)
                audioSource.PlayOneShot(itemDropSound);
        }

        isFilled = false;
    }

    public string GetPrompt()
    {
        return isFilled ? "Already filled" : "Place " + requiredType;
    }
}
