using System.Collections;
using UnityEngine;

public class AutoDoubleDoor : MonoBehaviour
{
    public float slideDistance = 2f;
    public float slideSpeed = 3f;
    public float closeDelay = 2f;

    private Transform leftDoor, rightDoor;
    private Vector3 leftClosed, rightClosed;
    private Vector3 leftOpen, rightOpen;
    private bool isOpen = false;
    private int playersInside = 0;

    void Start()
    {
        // Auto-find doors by name — no Inspector dragging needed
        leftDoor = transform.Find("LeftDoor");
        rightDoor = transform.Find("RightDoor");

        if (leftDoor == null || rightDoor == null)
        {
            Debug.LogError("LeftDoor or RightDoor not found! Check names in Hierarchy.");
            return;
        }

        // Use WORLD position, not local
        leftClosed = leftDoor.position;
        rightClosed = rightDoor.position;
        leftOpen = leftClosed + transform.right * slideDistance;
        rightOpen = rightClosed + transform.right * -slideDistance;

        Debug.Log("Door script initialized successfully.");
    }

    void Update()
    {
        if (leftDoor == null || rightDoor == null) return;

        Vector3 leftTarget = isOpen ? leftOpen : leftClosed;
        Vector3 rightTarget = isOpen ? rightOpen : rightClosed;

        leftDoor.position = Vector3.MoveTowards(leftDoor.position, leftTarget, slideSpeed * Time.deltaTime);
        rightDoor.position = Vector3.MoveTowards(rightDoor.position, rightTarget, slideSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger entered by: " + other.name);
        playersInside++;
        isOpen = true;
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log("Trigger exited by: " + other.name);
        playersInside = Mathf.Max(0, playersInside - 1);
        if (playersInside == 0)
            StartCoroutine(CloseAfterDelay());
    }

    IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);
        if (playersInside == 0) isOpen = false;
    }
}
