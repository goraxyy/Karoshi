using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public enum ItemType
{
    Cereal,
    Soda,
    Bread,
    Milk,
    Chips
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

    public void SetCarried(bool carried, Transform parent)
    {
        if (rb != null)
        {
            rb.isKinematic = carried;
            rb.useGravity = !carried;
        }

        GetComponent<Collider>().enabled = !carried;

        if (carried)
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
}
