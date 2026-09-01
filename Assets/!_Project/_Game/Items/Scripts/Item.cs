using UnityEngine;

public enum ItemType
{
    Cereal,
    Soda,
    Bread,
    Milk,
    Chips,
    Mop      // a tool rather than stock, so it never matches a shelf slot
}

[RequireComponent(typeof(Rigidbody))]
public class Item : MonoBehaviour
{
    public ItemType type;

    [Header("Hold Offset")]
    public Vector3 holdPositionOffset = Vector3.zero;
    public Vector3 holdRotationOffset = Vector3.zero;

    [HideInInspector] public bool isCarried;
    [HideInInspector] public bool isOnShelf;
    [HideInInspector] public Vector3 shelfRotationOffset;

    Rigidbody rb;
    Collider col;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    // No Update(): with thousands of items in a level, re-applying a transform every frame
    // for every item costs far more than applying it once when the state actually changes.
    // The Set* methods below do that, and OnValidate keeps the in-editor live tweaking.

    public void ApplyCarriedTransform()
    {
        transform.localPosition = holdPositionOffset;
        transform.localRotation = Quaternion.Euler(holdRotationOffset);
    }

    public void ApplyShelfTransform()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(shelfRotationOffset);
    }

#if UNITY_EDITOR
    // Tweaking the offsets in the Inspector during play still updates immediately,
    // but costs nothing at runtime.
    void OnValidate()
    {
        if (!Application.isPlaying) return;

        if (isCarried) ApplyCarriedTransform();
        else if (isOnShelf) ApplyShelfTransform();
    }
#endif

    // Called when player actively holds item or when ejected
    public void SetCarried(bool carried, Transform parent)
    {
        gameObject.SetActive(true);
        isCarried = carried;
        isOnShelf = false;

        if (rb != null)
        {
            rb.isKinematic = carried;
            rb.useGravity = !carried;
        }

        // Collider disabled while held so it doesn't push the player
        if (col != null)
            col.enabled = !carried;

        if (carried && parent != null)
        {
            transform.SetParent(parent);
            ApplyCarriedTransform();
        }
        else
        {
            transform.SetParent(null);
        }
    }

    // Called when placed on a shelf snap point
    public void SetOnShelf(Transform snapPoint, Vector3 rotationOffset)
    {
        gameObject.SetActive(true);
        isCarried = false;
        isOnShelf = true;
        shelfRotationOffset = rotationOffset;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Keep collider DISABLED on shelf — prevents pushing player
        if (col != null)
            col.enabled = false;

        transform.SetParent(snapPoint);
        ApplyShelfTransform();
    }

    // Called when item goes into a non-active inventory slot
    public void SetStowed(Transform stashParent)
    {
        isCarried = false;
        isOnShelf = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (col != null)
            col.enabled = false;

        transform.SetParent(stashParent);
        transform.localPosition = Vector3.zero;
        gameObject.SetActive(false);
    }
}