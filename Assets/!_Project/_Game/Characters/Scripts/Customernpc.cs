using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CustomerNPC : MonoBehaviour, IInteractable, IHoverable
{
    [Header("Route (leave empty if CustomerSpawner will assign these)")]
    public Transform[] shelfPoints;
    public Transform cashierPoint;
    public Transform exitPoint;

    [Header("Shopping Behaviour")]
    public Vector2Int shelvesToVisitRange = new Vector2Int(2, 4);
    public Vector2 shelfStayDurationRange = new Vector2(3f, 8f);

    [Header("Mess & Trash")]
    public GameObject dirtPrefab;
    [Range(0f, 1f)] public float dirtChance = 0.5f;
    [Range(0f, 1f)] public float trashcanVisitChance = 0.5f;
    public float trashcanUseSeconds = 3f;
    public float trashcanStandOffset = 1f;

    [Header("Taking Items")]
    public Transform carryPoint;                 // items hover here, in front of the customer
    public float shelfReachRadius = 2.5f;        // how far they can reach for a stocked slot
    public float carryStackSpacing = 0.42f;      // vertical gap between carried items
    public int maxCarriedItems = 3;

    [Header("Cashier")]
    public bool waitForPlayerToServe = true;              // wait at the desk until the player presses E
    public Vector2 cashierWaitDurationRange = new Vector2(4f, 10f); // used only when waitForPlayerToServe is false
    public float faceTurnSpeed = 6f;

    [Header("Movement")]
    public NavMeshAgent agent;
    public float arriveDistance = 0.3f;
    public float stuckTimeout = 20f; // safety net so a blocked NPC doesn't wait forever at one destination

    // True while standing at the desk waiting to be served by the player.
    public bool IsWaitingToBeServed
    {
        get => isWaitingToBeServed;
        private set
        {
            if (isWaitingToBeServed == value) return;
            isWaitingToBeServed = value;
            WaitingCount = Mathf.Max(0, WaitingCount + (value ? 1 : -1));
            TaskManager.NotifyWorldChanged();
        }
    }

    // How many customers are queued at the till right now — the shift can't be
    // closed while anyone is still waiting to be served.
    public static int WaitingCount { get; private set; }

    bool isWaitingToBeServed;

    Action onDespawn;
    OutlineHighlight outline;
    Transform player;
    bool served;
    readonly List<Item> basket = new List<Item>();

    void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        outline = GetComponent<OutlineHighlight>();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    void Start()
    {
        StartCoroutine(RunRoutine());
    }

    // Called by CustomerSpawner right after Instantiate to hand over this run's route.
    public void Init(Transform[] shelves, Transform cashier, Transform exit, Action despawnCallback)
    {
        shelfPoints = shelves;
        cashierPoint = cashier;
        exitPoint = exit;
        onDespawn = despawnCallback;
    }

    IEnumerator RunRoutine()
    {
        if (cashierPoint == null || exitPoint == null || shelfPoints == null || shelfPoints.Length == 0)
        {
            Debug.LogWarning("CustomerNPC is missing route points, despawning.");
            Despawn();
            yield break;
        }

        // Some shoppers get the urge to bin something partway round the store.
        List<Transform> route = PickRandomShelves();
        bool willUseBin = UnityEngine.Random.value <= trashcanVisitChance;
        int binAfterShelf = willUseBin && route.Count > 0
            ? UnityEngine.Random.Range(0, route.Count)
            : -1;

        for (int i = 0; i < route.Count; i++)
        {
            yield return MoveTo(route[i].position);

            // Browse for a moment, then take something off the shelf.
            float browseTime = UnityEngine.Random.Range(shelfStayDurationRange.x, shelfStayDurationRange.y);
            yield return new WaitForSeconds(browseTime * 0.5f);

            TakeItemFromNearbyShelf();

            yield return new WaitForSeconds(browseTime * 0.5f);

            if (i == binAfterShelf)
                yield return VisitTrashcan();
        }

        yield return MoveTo(cashierPoint.position);
        yield return WaitAtCashier();

        yield return MoveTo(exitPoint.position);

        Despawn();
    }

    IEnumerator WaitAtCashier()
    {
        if (!waitForPlayerToServe)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(cashierWaitDurationRange.x, cashierWaitDurationRange.y));
            yield break;
        }

        served = false;
        IsWaitingToBeServed = true;

        // Stand still and look toward the player while queueing to be served.
        while (!served)
        {
            FacePlayer();
            yield return null;
        }

        IsWaitingToBeServed = false;
    }

    void FacePlayer()
    {
        if (player == null) return;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion target = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * faceTurnSpeed);
    }

    // Mid-shop, some customers wander to the nearest bin and use it.
    // The caller decides whether this happens; this just walks there and does it.
    IEnumerator VisitTrashcan()
    {
        Trashcan can = FindNearestTrashcan();
        if (can == null) yield break;

        // Stand in front of it rather than inside it.
        Vector3 approach = can.transform.position
                         + (transform.position - can.transform.position).normalized * trashcanStandOffset;
        if (!NavMesh.SamplePosition(approach, out NavMeshHit spot, 2f, NavMesh.AllAreas)) yield break;

        // The bin sits out the back — only go if there is a real walkable route to it,
        // otherwise the customer would stand against a wall and use it through the wall.
        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(spot.position, path) || path.status != NavMeshPathStatus.PathComplete)
            yield break;

        yield return MoveTo(spot.position);

        // Confirm we actually got there before counting it as a use. Distance alone isn't
        // enough — the bin sits against a wall, and a customer on the far side can easily be
        // within a couple of metres of it, so require clear line of sight as well.
        if (Vector3.Distance(transform.position, can.transform.position) > trashcanStandOffset + 1.5f)
            yield break;
        if (!HasLineOfSight(can.transform))
            yield break;

        yield return new WaitForSeconds(trashcanUseSeconds);

        if (can != null) can.RegisterUse();
    }

    // True when nothing solid sits between this customer and the target.
    bool HasLineOfSight(Transform target)
    {
        Vector3 from = transform.position + Vector3.up * 0.6f;
        Vector3 to = target.position + Vector3.up * 0.4f;
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance < 0.01f) return true;

        foreach (RaycastHit hit in Physics.RaycastAll(from, direction / distance, distance))
        {
            if (hit.collider.isTrigger) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;   // ourselves
            if (hit.collider.transform.IsChildOf(target)) continue;      // the bin itself
            if (target.IsChildOf(hit.collider.transform)) continue;      // a parent of the bin
            return false;                                               // something is in the way
        }

        return true;
    }

    Trashcan FindNearestTrashcan()
    {
        var cans = Trashcan.All;
        Trashcan nearest = null;
        float nearestSqr = float.MaxValue;

        for (int i = 0; i < cans.Count; i++)
        {
            float sqr = (transform.position - cans[i].transform.position).sqrMagnitude;
            if (sqr < nearestSqr) { nearest = cans[i]; nearestSqr = sqr; }
        }

        return nearest;
    }

    // Customers are messy: half the time taking something leaves a patch behind.
    void DropDirt()
    {
        if (dirtPrefab == null) return;
        if (UnityEngine.Random.value > dirtChance) return;

        // The shift allows only so much mess at once.
        TaskManager tasks = TaskManager.Instance;
        if (tasks != null && tasks.MaxDirt > 0 && Dirt.ActiveCount >= tasks.MaxDirt) return;

        Vector3 position = transform.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            position = hit.position;

        Instantiate(dirtPrefab, position + Vector3.up * 0.02f, Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f));
    }

    // Takes one item from the nearest stocked slot within reach. The item leaves the shelf
    // (so the slot needs restocking) and hovers in front of the customer from then on.
    void TakeItemFromNearbyShelf()
    {
        if (basket.Count >= maxCarriedItems) return;

        ShelfSlot nearest = FindNearestStockedSlot();
        if (nearest == null) return;

        Item taken = nearest.TakeItem();
        if (taken == null) return;

        Transform holder = carryPoint != null ? carryPoint : transform;
        taken.SetCarried(true, holder);

        // Nothing re-applies this every frame any more, so the stacking offset sticks.
        taken.transform.localPosition = new Vector3(0f, basket.Count * carryStackSpacing, 0f);
        taken.transform.localRotation = Quaternion.identity;

        basket.Add(taken);

        DropDirt();
    }

    ShelfSlot FindNearestStockedSlot()
    {
        var slots = ShelfSlot.All;
        ShelfSlot nearest = null;
        float nearestSqr = shelfReachRadius * shelfReachRadius;
        Vector3 position = transform.position;

        for (int i = 0; i < slots.Count; i++)
        {
            ShelfSlot slot = slots[i];
            if (!slot.isFilled) continue;

            float sqr = (position - slot.transform.position).sqrMagnitude;
            if (sqr >= nearestSqr) continue;

            // Being close isn't enough — a shelf on the far side of a wall is metres away
            // but not actually reachable.
            if (!CanReach(slot)) continue;

            nearest = slot;
            nearestSqr = sqr;
        }

        return nearest;
    }

    // True when nothing but this slot's own shelf sits between the customer and the slot.
    bool CanReach(ShelfSlot slot)
    {
        Transform shelf = slot.owner != null ? slot.owner.transform : slot.transform.root;

        Vector3 from = transform.position + Vector3.up * 0.8f;
        Vector3 to = slot.transform.position;
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance < 0.05f) return true;

        foreach (RaycastHit hit in Physics.RaycastAll(from, direction / distance, distance))
        {
            if (hit.collider.isTrigger) continue;                          // slots themselves
            if (hit.collider.transform.IsChildOf(transform)) continue;     // ourselves
            if (hit.collider.transform.IsChildOf(shelf)) continue;         // the shelf we're reaching into
            return false;                                                  // a wall or another fixture
        }

        return true;
    }

    List<Transform> PickRandomShelves()
    {
        List<Transform> pool = new List<Transform>(shelfPoints);
        int count = Mathf.Min(UnityEngine.Random.Range(shelvesToVisitRange.x, shelvesToVisitRange.y + 1), pool.Count);

        List<Transform> picked = new List<Transform>(count);
        for (int i = 0; i < count; i++)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return picked;
    }

    IEnumerator MoveTo(Vector3 destination)
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"{name} is not on the NavMesh, despawning.", this);
            Despawn();
            yield break;
        }

        // Route points are authored by hand (ShelfPoint sits in front of each shelf), so snap
        // to the nearest reachable spot rather than failing on a slightly off-mesh target.
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            destination = hit.position;

        agent.SetDestination(destination);

        float elapsed = 0f;
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + arriveDistance)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= stuckTimeout)
                break;

            yield return null;
        }
    }

    // --- Player interaction -------------------------------------------------

    public void Interact(PlayerInteract player)
    {
        if (!IsWaitingToBeServed) return;

        served = true;
    }

    public string GetPrompt()
    {
        return IsWaitingToBeServed ? "Serve customer" : string.Empty;
    }

    public void OnHoverEnter()
    {
        if (outline != null)
            outline.SetHighlighted(true);
    }

    public void OnHoverExit()
    {
        if (outline != null)
            outline.SetHighlighted(false);
    }

    void Despawn()
    {
        onDespawn?.Invoke();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Don't leave a destroyed customer counted as still waiting.
        IsWaitingToBeServed = false;
    }
}
