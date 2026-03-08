using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum ItemType
{
    Cereal,
    Soda,
    Bread,
    Milk,
    Chips,
    Bar
}

[RequireComponent(typeof(Rigidbody))]
public class Item : MonoBehaviour
{
    public ItemType type;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Called when actively held at the holdPoint, or placed on a ShelfSlot snapPoint
    public void SetCarried(bool carried, Transform parent)
    {
        gameObject.SetActive(true); // Re-enable in case it was stowed

        if (rb != null)
        {
            rb.isKinematic = carried;
            rb.useGravity = !carried;
        }

        GetComponent<Collider>().enabled = !carried;

        if (carried && parent != null)
        {
            transform.SetParent(parent);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            transform.SetParent(null);
        }
    }

    // Called when stored in a non-active inventory slot — hides the item completely
    public void SetStowed(Transform stashParent)
    {
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        GetComponent<Collider>().enabled = false;
        transform.SetParent(stashParent);
        transform.localPosition = Vector3.zero;
        gameObject.SetActive(false); // Invisible until the slot is selected
    }
}