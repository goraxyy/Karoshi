using UnityEngine;

// Tools that live in a fixed spot — the mop, the stock crate. They can be carried in the
// inventory like any other item, but dropping one returns it to its snap point rather than
// leaving it wherever the player happened to be standing.
public class ItemHome : MonoBehaviour, IHomeReturnable
{
    [Tooltip("Where this returns to when dropped. Falls back to wherever it started.")]
    public Transform snapPoint;

    [Tooltip("Freeze physics once it's back home so it can't be nudged around.")]
    public bool settleOnReturn = true;

    Vector3 startPosition;
    Quaternion startRotation;
    Vector3 startScale;

    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        startScale = transform.localScale;
    }

    public void ReturnHome()
    {
        transform.SetParent(null, true);

        if (snapPoint != null)
            transform.SetPositionAndRotation(snapPoint.position, snapPoint.rotation);
        else
            transform.SetPositionAndRotation(startPosition, startRotation);

        transform.localScale = startScale;

        // Make sure it can be seen and picked up again.
        gameObject.SetActive(true);
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = true;

        if (settleOnReturn)
        {
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }
    }
}
