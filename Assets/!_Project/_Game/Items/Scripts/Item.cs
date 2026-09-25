using UnityEngine;

public enum ItemType
{
    Cereal,
    Soda,
    Bread,
    Milk,
    Chips,
    Mop,      // a tool rather than stock, so it never matches a shelf slot
    Stock,    // the restocking crate — same, it's carried but never shelved
    TrashBag, // carried out to the container, never shelved
    Flashlight // a tool as well — carried and dropped, never shelved
}

[RequireComponent(typeof(Rigidbody))]
public class Item : MonoBehaviour
{
    public ItemType type;

    [Header("Impact Sound")]
    [Tooltip("Played when this lands on the floor, a shelf, or another item.")]
    public AudioClip impactSound;

    [Tooltip("Slower contacts than this are a nudge, not a knock, and stay silent.")]
    public float impactMinSpeed = 1.2f;

    [Tooltip("Contacts at or above this speed play at full volume.")]
    public float impactLoudSpeed = 6f;

    [Tooltip("One clatter per landing: a bouncing item makes several contacts in a row.")]
    public float impactCooldown = 0.12f;

    [Header("Hold Offset")]
    public Vector3 holdPositionOffset = Vector3.zero;
    public Vector3 holdRotationOffset = Vector3.zero;

    [HideInInspector] public bool isCarried;
    [HideInInspector] public bool isOnShelf;
    [HideInInspector] public Vector3 shelfRotationOffset;

    Rigidbody rb;
    Collider[] colliders;
    float nextImpactTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Every collider, not just the one on the root. The mop keeps two more on its
        // children, and leaving those live while carried let them swing across the
        // crosshair as the player turned or strafed, re-targeting the mop in your hands.
        colliders = GetComponentsInChildren<Collider>(true);
    }

    // No Update(): with thousands of items in a level, re-applying a transform every frame
    // for every item costs far more than applying it once when the state actually changes.
    // The Set* methods below do that, and OnValidate keeps the in-editor live tweaking.

    void SetCollidersEnabled(bool on)
    {
        if (colliders == null) return;
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = on;
    }

    // Dropped, thrown, or knocked off a shelf — anything that actually strikes something.
    // Held and shelved items are kinematic with their collider off, so they never get here.
    void OnCollisionEnter(Collision collision)
    {
        if (impactSound == null || isCarried || isOnShelf) return;
        if (Time.time < nextImpactTime) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < impactMinSpeed) return;

        nextImpactTime = Time.time + impactCooldown;

        Vector3 where = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        float loudness = Mathf.InverseLerp(impactMinSpeed, impactLoudSpeed, speed);
        OneShotAudio.PlayAt(impactSound, where, Mathf.Lerp(0.3f, 1f, loudness));
    }

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

        // Colliders disabled while held so it neither pushes the player nor re-targets itself
        SetCollidersEnabled(!carried);

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

        // Keep colliders DISABLED on shelf — prevents pushing player
        SetCollidersEnabled(false);

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

        SetCollidersEnabled(false);

        transform.SetParent(stashParent);
        transform.localPosition = Vector3.zero;
        gameObject.SetActive(false);
    }
}