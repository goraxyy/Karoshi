using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public enum AIState { Patrol, Sabotage, Search, Chase }

    [Header("References")]
    public Transform playerTransform;
    public Transform[] patrolPoints;
    public NavMeshAgent agent;

    [Header("Perception")]
    public float viewDistance = 15f;
    public float viewAngle = 90f;
    public LayerMask playerLayer;

    [Header("Behavior")]
    public float sabotageChance = 0.001f;
    public float chaseDuration = 5f;

    [HideInInspector] public AIState currentState = AIState.Patrol;

    int currentPatrolIndex;
    float chaseTimer;
    Vector3 lastPlayerPosition;
    BurnoutSystem burnoutSystem;

    void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        burnoutSystem = FindFirstObjectByType<BurnoutSystem>();
    }

    void Update()
    {
        bool canSeePlayer = CanSeePlayer();

        if (canSeePlayer)
        {
            currentState = AIState.Chase;
            chaseTimer = chaseDuration;
            lastPlayerPosition = playerTransform.position;

            if (burnoutSystem != null)
                burnoutSystem.SetChaseState(true);
        }
        else if (currentState == AIState.Chase)
        {
            chaseTimer -= Time.deltaTime;
            if (chaseTimer <= 0f)
            {
                currentState = AIState.Search;
                if (burnoutSystem != null)
                    burnoutSystem.SetChaseState(false);
            }
        }

        switch (currentState)
        {
            case AIState.Patrol: UpdatePatrol(); break;
            case AIState.Sabotage: UpdateSabotage(); break;
            case AIState.Search: UpdateSearch(); break;
            case AIState.Chase: UpdateChase(); break;
        }
    }

    void UpdatePatrol()
    {
        if (patrolPoints.Length == 0) return;

        // Unity 6.2 fix: only call SetDestination if actually needed
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            Vector3 nextPoint = patrolPoints[currentPatrolIndex].position;

            if (Vector3.Distance(agent.destination, nextPoint) > 0.5f)
                agent.SetDestination(nextPoint);
        }

        // Random sabotage chance
        if (Random.value < sabotageChance)
            currentState = AIState.Sabotage;
    }

    void UpdateSabotage()
    {
        ShelfSlot targetSlot = FindNearestFilledSlot();

        if (targetSlot != null)
        {
            if (Vector3.Distance(agent.destination, targetSlot.transform.position) > 0.5f)
                agent.SetDestination(targetSlot.transform.position);

            if (!agent.pathPending && agent.remainingDistance <= 2f)
            {
                targetSlot.Eject();
                currentState = AIState.Patrol;
            }
        }
        else
        {
            currentState = AIState.Patrol;
        }
    }

    void UpdateSearch()
    {
        if (Vector3.Distance(agent.destination, lastPlayerPosition) > 0.5f)
            agent.SetDestination(lastPlayerPosition);

        if (!agent.pathPending && agent.remainingDistance <= 1f)
            currentState = AIState.Patrol;
    }

    void UpdateChase()
    {
        // Unity 6.2 optimization: only update destination if player moved significantly
        if (Vector3.Distance(agent.destination, playerTransform.position) > 1.5f)
            agent.SetDestination(playerTransform.position);

        // Check if caught player
        if (Vector3.Distance(transform.position, playerTransform.position) < 1.5f)
        {
            Debug.Log("Player caught!");
            // TODO: Trigger penalty
        }
    }

    bool CanSeePlayer()
    {
        Vector3 directionToPlayer = playerTransform.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer > viewDistance)
            return false;

        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
        if (angleToPlayer > viewAngle * 0.5f)
            return false;

        // Raycast for occlusion
        if (Physics.Raycast(transform.position + Vector3.up * 1f, directionToPlayer.normalized, out RaycastHit hit, viewDistance))
        {
            if (hit.transform == playerTransform)
                return true;
        }

        return false;
    }

    ShelfSlot FindNearestFilledSlot()
    {
        ShelfSlot[] slots = FindObjectsByType<ShelfSlot>(FindObjectsSortMode.None);
        ShelfSlot nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var slot in slots)
        {
            if (!slot.isFilled) continue;

            float distance = Vector3.Distance(transform.position, slot.transform.position);
            if (distance < nearestDistance)
            {
                nearest = slot;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    public void ResetForNewShift()
    {
        currentState = AIState.Patrol;
        chaseTimer = 0f;

        if (patrolPoints.Length > 0)
            agent.Warp(patrolPoints[0].position);
    }
}
