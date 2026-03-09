using UnityEngine;
using System.Collections;

public class CustomerNPC : MonoBehaviour
{
    [System.Serializable]
    public class Waypoint
    {
        public Transform point;
        public float stayDuration = 2f;      // How long to stand here (0 = just pass through)
        public bool isTurningPoint = false;  // Should NPC rotate smoothly to face next waypoint?
    }

    [Header("Route")]
    public float verticalOffset = 1f;      // Lifts NPC up so feet align with waypoint (set to half NPC height)
    public Waypoint[] waypoints;
    public bool loopRoute = true;            // Loop back to start or stop at end

    [Header("Movement Timing")]
    public float totalRouteDuration = 60f;   // Total seconds to complete the full route
    [Range(0.5f, 3f)]
    public float speedMultiplier = 1f;       // Quick multiplier on top of auto-calculated speed

    [Header("Rotation")]
    public float rotationSpeed = 5f;         // How fast NPC rotates at turning points

    // Internal state
    int currentWaypointIndex = 0;
    bool isWaiting = false;
    bool routeComplete = false;
    float calculatedSpeed;

    void Start()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        calculatedSpeed = CalculateSpeed();

        // Snap to first waypoint
        transform.position = waypoints[0].point.position + Vector3.up * verticalOffset;

        StartCoroutine(FollowRoute());
    }

    // Automatically calculate speed based on total route distance and desired duration
    float CalculateSpeed()
    {
        float totalDistance = 0f;

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i].point != null && waypoints[i + 1].point != null)
                totalDistance += Vector3.Distance(waypoints[i].point.position, waypoints[i + 1].point.position);
        }

        if (loopRoute && waypoints.Length > 1)
        {
            // Add distance from last waypoint back to first
            totalDistance += Vector3.Distance(
                waypoints[waypoints.Length - 1].point.position,
                waypoints[0].point.position
            );
        }

        // Total stay time across all waypoints
        float totalStayTime = 0f;
        foreach (var wp in waypoints)
            totalStayTime += wp.stayDuration;

        float movingTime = Mathf.Max(1f, totalRouteDuration - totalStayTime);
        float speed = (totalDistance / movingTime) * speedMultiplier;

        return Mathf.Max(0.1f, speed);
    }

    IEnumerator FollowRoute()
    {
        while (!routeComplete)
        {
            Waypoint current = waypoints[currentWaypointIndex];

            // Stay at waypoint if stayDuration > 0
            if (current.stayDuration > 0f)
            {
                isWaiting = true;
                yield return new WaitForSeconds(current.stayDuration);
                isWaiting = false;
            }

            // Figure out next waypoint index
            int nextIndex = currentWaypointIndex + 1;

            if (nextIndex >= waypoints.Length)
            {
                if (loopRoute)
                    nextIndex = 0;
                else
                {
                    routeComplete = true;
                    yield break;
                }
            }

            Waypoint next = waypoints[nextIndex];

            // If this is a turning point, rotate to face next waypoint first
            if (current.isTurningPoint)
                yield return StartCoroutine(RotateTowards(next.point.position));

            // Move to next waypoint
            yield return StartCoroutine(MoveToWaypoint(next.point.position));

            currentWaypointIndex = nextIndex;
        }
    }

    IEnumerator MoveToWaypoint(Vector3 target)
    {
        Vector3 targetWithOffset = target + Vector3.up * verticalOffset;
        Vector3 start = transform.position;
        float distance = Vector3.Distance(start, targetWithOffset);
        float duration = distance / calculatedSpeed;
        float elapsed = 0f;

        // Face direction of movement — flatten Y so NPC doesn't tilt up/down
        Vector3 direction = (targetWithOffset - start);
        direction.y = 0f;
        direction.Normalize();
        if (direction != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                transform.position = Vector3.Lerp(start, targetWithOffset, t);

                // Smoothly rotate to face movement direction while walking
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);

                yield return null;
            }
        }

        transform.position = targetWithOffset;
    }

    IEnumerator RotateTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position);
        direction.y = 0f;
        direction.Normalize();
        if (direction == Vector3.zero) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    // Draw route in Scene view so you can see the path
    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = Color.cyan;

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i].point != null && waypoints[i + 1].point != null)
                Gizmos.DrawLine(waypoints[i].point.position, waypoints[i + 1].point.position);
        }

        if (loopRoute && waypoints[0].point != null && waypoints[waypoints.Length - 1].point != null)
            Gizmos.DrawLine(waypoints[waypoints.Length - 1].point.position, waypoints[0].point.position);

        foreach (var wp in waypoints)
        {
            if (wp.point == null) continue;

            Gizmos.color = wp.isTurningPoint ? Color.yellow : Color.green;
            Gizmos.DrawSphere(wp.point.position, 0.15f);
        }
    }
}