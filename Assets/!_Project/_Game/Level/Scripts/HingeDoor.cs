using System.Collections;
using UnityEngine;

public class HingeDoor : MonoBehaviour
{
    [Header("Settings")]
    public float openAngle = 90f;  // How far the door swings open
    public float openSpeed = 3f;   // Swing speed
    public float interactRange = 3f; // Max distance to interact

    private Quaternion closedRot;
    private Quaternion openRot;
    private bool isOpen = false;
    private bool isMoving = false;
    private Transform player;

    void Start()
    {
        closedRot = transform.rotation;
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        // Check distance & E key press
        float dist = Vector3.Distance(transform.position, player.position);
        if (Input.GetKeyDown(KeyCode.E) && dist <= interactRange && !isMoving)
        {
            if (!isOpen)
            {
                // Detect which side the player is on using dot product
                Vector3 doorForward = transform.forward;
                Vector3 toPlayer = (player.position - transform.position).normalized;
                float side = Vector3.Dot(doorForward, toPlayer);

                // Open away from the player
                float angle = (side >= 0) ? -openAngle : openAngle;
                openRot = Quaternion.Euler(transform.eulerAngles + new Vector3(0, angle, 0));
            }

            StartCoroutine(RotateDoor(isOpen ? closedRot : openRot));
            isOpen = !isOpen;
        }

        // Optional: show a prompt when in range
        if (dist <= interactRange)
            Debug.Log(isOpen ? "Press E to close" : "Press E to open");
    }

    IEnumerator RotateDoor(Quaternion target)
    {
        isMoving = true;
        while (Quaternion.Angle(transform.rotation, target) > 0.1f)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, target, openSpeed * Time.deltaTime);
            yield return null;
        }
        transform.rotation = target;
        isMoving = false;
    }
}
