using UnityEngine;

// The skip out back. Its trigger volume swallows any trash bag dropped into it —
// that's what actually clears the trash task.
[RequireComponent(typeof(Collider))]
public class TrashContainer : MonoBehaviour
{
    public AudioClip disposeSound;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        TryDispose(other);
    }

    // A bag dropped from standing height can pass through in a single physics step,
    // so also accept anything resting inside the volume.
    void OnTriggerStay(Collider other)
    {
        TryDispose(other);
    }

    void TryDispose(Collider other)
    {
        TrashBag bag = other.GetComponentInParent<TrashBag>();
        if (bag == null) return;

        // Ignore a bag still in the player's hands hovering over the skip.
        Item item = bag.GetComponent<Item>();
        if (item != null && item.isCarried) return;

        OneShotAudio.PlayAt(disposeSound, transform.position);
        Destroy(bag.gameObject);   // OnDisable refreshes the task list
    }
}
