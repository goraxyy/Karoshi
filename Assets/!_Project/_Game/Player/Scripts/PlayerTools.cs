using UnityEngine;

// Holds one bulky "tool" at a time — the mop, a stock crate, or a full trashcan.
// Separate from CarrySlot, which handles shelf items and has four slots.
public class PlayerTools : MonoBehaviour
{
    [Tooltip("Where held tools are parented. Falls back to the camera if unset.")]
    public Transform toolHoldPoint;

    public GameObject HeldTool { get; private set; }
    public bool IsHolding => HeldTool != null;

    Vector3 heldOriginalScale;

    public T GetHeld<T>() where T : Component
    {
        return HeldTool != null ? HeldTool.GetComponent<T>() : null;
    }

    public void Hold(GameObject tool, Vector3 localPosition, Vector3 localEuler, float scaleMultiplier = 1f)
    {
        if (tool == null || IsHolding) return;

        HeldTool = tool;
        heldOriginalScale = tool.transform.localScale;

        // Colliders off while held so the item can't be re-targeted or shove the player around.
        foreach (Collider collider in tool.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        Rigidbody body = tool.GetComponent<Rigidbody>();
        if (body != null) { body.isKinematic = true; body.useGravity = false; }

        Transform parent = toolHoldPoint != null ? toolHoldPoint : transform;
        tool.transform.SetParent(parent, false);
        tool.transform.localPosition = localPosition;
        tool.transform.localRotation = Quaternion.Euler(localEuler);
        tool.transform.localScale = heldOriginalScale * scaleMultiplier;
    }

    // Puts the tool back into the world where the player is standing.
    public GameObject Release()
    {
        if (!IsHolding) return null;

        GameObject tool = HeldTool;
        HeldTool = null;

        tool.transform.SetParent(null, true);
        tool.transform.localScale = heldOriginalScale;

        foreach (Collider collider in tool.GetComponentsInChildren<Collider>(true))
            collider.enabled = true;

        return tool;
    }

    // Puts the tool back at a specific place (used when returning the trashcan home).
    public GameObject ReleaseTo(Vector3 position, Quaternion rotation)
    {
        GameObject tool = Release();
        if (tool != null)
        {
            tool.transform.position = position;
            tool.transform.rotation = rotation;
        }
        return tool;
    }

    public void DestroyHeld()
    {
        if (!IsHolding) return;

        GameObject tool = HeldTool;
        HeldTool = null;
        Destroy(tool);
    }
}
