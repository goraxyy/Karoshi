using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Some shoppers can't find what they came for. After taking something off a shelf a
// customer may stop, light up, and wait to be asked what's wrong. Talk to them and they
// name a product; agree to help and they follow you until you walk them to the right
// shelf, which is marked with a beacon.
//
// The whole thing runs as one coroutine owned by CustomerNPC's routine, so while a request
// is live the customer's normal shopping is simply paused.
[RequireComponent(typeof(CustomerNPC))]
public class CustomerRequest : MonoBehaviour
{
    public enum Stage { None, Asking, Talking, Escorting, Returning }

    [Header("Chance")]
    [Range(0f, 1f)]
    [Tooltip("Odds of asking for help after taking an item off a shelf.")]
    public float askChance = 0.5f;

    [Tooltip("Give up and carry on shopping after this long unattended. 0 waits forever.")]
    public float patienceSeconds = 120f;

    [Header("Talking")]
    [Tooltip("How far the player can stand and still hold the conversation.")]
    public float talkRange = 5f;
    public float bubbleHeight = 2.15f;

    [Tooltip("{0} is the product name.")]
    public string[] questionTemplates =
    {
        "Excuse me - where is the {0}?",
        "Sorry, I can't find the {0} anywhere.",
        "Hi! Which aisle has the {0}?",
        "Do you still have any {0}?"
    };

    public string acceptLabel = "Follow me";
    public string declineLabel = "Sorry, I'm busy";
    public string thanksLine = "Oh, there it is. Thanks!";
    public string declineLine = "...right. Thanks anyway.";
    public string waitingLine = "?";
    public string followingLine = "Right behind you.";
    public string strandedLine = "Hey - where did you go?";

    [Header("Escort")]
    [Tooltip("The player has to stay inside this circle or the customer turns back.")]
    public float followRadius = 7f;

    [Tooltip("How far behind the player the customer trails.")]
    public float followStandoff = 1.8f;

    [Tooltip("Size of the spot in front of the shelf the customer has to walk into.")]
    public float arriveRadius = 1.6f;

    [Header("Colours")]
    public Color radiusColour = new Color(1f, 0.84f, 0.1f, 0.30f);
    public Color targetColour = new Color(0.35f, 1f, 0.45f, 0.35f);
    public Color beaconColour = new Color(1f, 0.84f, 0.1f, 0.95f);

    // How many customers are waiting on an answer right now — the directions task reads this.
    public static int PendingCount { get; private set; }

    // Statics outlive a play-mode restart when domain reloading is off, and a count left
    // over from the last run would block clocking out forever. Start every run at zero.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCounters() => PendingCount = 0;

    public Stage CurrentStage { get; private set; }

    CustomerNPC npc;
    NavMeshAgent agent;
    SpeechBubble bubble;
    GuideMarker radiusRing;
    GuideMarker targetRing;
    GuideMarker beacon;

    Vector3 homePosition;
    Vector3 destination;
    ShelfUnit destinationShelf;
    string question;
    string[] options;
    bool talkRequested;
    bool counted;
    float originalStoppingDistance;

    void Awake()
    {
        npc = GetComponent<CustomerNPC>();
        agent = npc.agent != null ? npc.agent : GetComponent<NavMeshAgent>();
    }

    // --- entry point, driven by CustomerNPC.RunRoutine ----------------------

    public IEnumerator Run()
    {
        if (!ShouldAsk()) yield break;
        if (!PickDestination()) yield break;

        Begin();

        // 1. Stand still and wait to be spoken to.
        yield return WaitToBeAsked();
        if (!talkRequested) { Finish(strandedLine, 0f); yield break; }

        // 2. The conversation: two options, arrows to move, E to pick.
        SetStage(Stage.Talking);
        yield return null;                 // don't let the E that opened this also confirm it

        int selected = 0;
        bool confirmed = false;
        bool abandoned = false;

        while (!confirmed && !abandoned)
        {
            Transform talker = npc.PlayerTransform;
            if (talker == null) { abandoned = true; break; }

            npc.FaceTowards(talker.position);

            if (Pressed(KeyCode.UpArrow) || Pressed(KeyCode.W)) selected = 0;
            if (Pressed(KeyCode.DownArrow) || Pressed(KeyCode.S)) selected = 1;
            if (Pressed(KeyCode.E) || Pressed(KeyCode.Return)) confirmed = true;
            if (!PlayerWithin(talkRange * 1.6f)) abandoned = true;

            bubble.ShowChoices(question, options, selected);
            yield return null;
        }

        if (abandoned || selected != 0)
        {
            Finish(declineLine, 1.2f);
            yield break;
        }

        // 3. The escort. The customer trails the player while they stay inside the ring,
        //    and turns back toward where it asked the moment they leave it. Nothing is
        //    reset by wandering off — walk back into the circle and it picks up again.
        yield return Escort();

        Finish(thanksLine, 1.6f);
    }

    IEnumerator WaitToBeAsked()
    {
        float waited = 0f;

        while (!talkRequested)
        {
            if (PlayerWithin(talkRange))
                npc.FaceTowards(npc.PlayerTransform.position);

            waited += Time.deltaTime;
            if (patienceSeconds > 0f && waited >= patienceSeconds) yield break;

            yield return null;
        }
    }

    IEnumerator Escort()
    {
        SetStage(Stage.Escorting);

        originalStoppingDistance = agent.stoppingDistance;
        agent.stoppingDistance = followStandoff;
        agent.isStopped = false;

        ShowEscortMarkers();

        while (!AtDestination())
        {
            Transform player = npc.PlayerTransform;
            if (player == null) break;

            bool inRange = Vector3.Distance(transform.position, player.position) <= followRadius;

            if (inRange)
            {
                if (CurrentStage != Stage.Escorting)
                {
                    SetStage(Stage.Escorting);
                    bubble.Show(followingLine);
                }
                agent.SetDestination(player.position);
            }
            else
            {
                // Out of range: head back to where the question was asked. Progress toward
                // the shelf isn't lost — the destination and its marker stay put.
                if (CurrentStage != Stage.Returning)
                {
                    SetStage(Stage.Returning);
                    bubble.Show(strandedLine);
                }
                agent.SetDestination(homePosition);
            }

            yield return null;
        }

        agent.stoppingDistance = originalStoppingDistance;
    }

    // The task is only done once the customer itself is standing in the spot in front
    // of the shelf — walking there alone doesn't count.
    bool AtDestination()
    {
        Vector3 here = transform.position;
        Vector3 there = destination;
        here.y = there.y = 0f;
        return Vector3.Distance(here, there) <= arriveRadius;
    }

    // --- setup / teardown ---------------------------------------------------

    bool ShouldAsk()
    {
        if (askChance <= 0f) return false;
        if (CurrentStage != Stage.None) return false;
        if (npc.PlayerTransform == null) return false;
        if (agent == null || !agent.isOnNavMesh) return false;
        return Random.value <= askChance;
    }

    // Picks a shelf somewhere else in the store that actually stocks something, and
    // takes the product name from what that shelf's slots hold.
    bool PickDestination()
    {
        Transform[] points = npc.shelfPoints;
        if (points == null || points.Length == 0) return false;

        NavMeshPath path = new NavMeshPath();

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Transform point = points[Random.Range(0, points.Length)];
            if (point == null) continue;

            // Don't send them to the shelf they are already standing at.
            if (Vector3.Distance(point.position, transform.position) < arriveRadius * 3f) continue;

            ShelfSlot slot = NearestSlotTo(point.position, 4f);
            if (slot == null) continue;

            if (!NavMesh.SamplePosition(point.position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;

            destination = hit.position;
            destinationShelf = slot.owner;
            question = string.Format(
                questionTemplates.Length > 0 ? questionTemplates[Random.Range(0, questionTemplates.Length)] : "Where is the {0}?",
                ProductName(slot.requiredType));
            return true;
        }

        return false;
    }

    static ShelfSlot NearestSlotTo(Vector3 position, float radius)
    {
        var slots = ShelfSlot.All;
        ShelfSlot nearest = null;
        float nearestSqr = radius * radius;

        for (int i = 0; i < slots.Count; i++)
        {
            float sqr = (position - slots[i].transform.position).sqrMagnitude;
            if (sqr >= nearestSqr) continue;
            nearest = slots[i];
            nearestSqr = sqr;
        }

        return nearest;
    }

    static string ProductName(ItemType type)
    {
        switch (type)
        {
            case ItemType.Cereal: return "cereal";
            case ItemType.Soda: return "soda";
            case ItemType.Bread: return "bread";
            case ItemType.Milk: return "milk";
            case ItemType.Chips: return "crisps";
            default: return "that thing";
        }
    }

    void Begin()
    {
        homePosition = transform.position;
        options = new[] { acceptLabel, declineLabel };
        talkRequested = false;

        StopWalking();
        npc.SetForcedHighlight(true);

        bubble = SpeechBubble.Create(transform, bubbleHeight);
        bubble.Show(waitingLine);

        SetStage(Stage.Asking);
    }

    void ShowEscortMarkers()
    {
        // The circle the player has to stay inside, drawn on the floor under the customer.
        radiusRing = GuideMarker.CreateRing("EscortRadius", radiusColour, followRadius, 0.94f);
        radiusRing.follow = transform;

        // The spot in front of the target shelf the customer has to reach.
        targetRing = GuideMarker.CreateRing("EscortTarget", targetColour, arriveRadius, 0f);
        targetRing.transform.position = destination + Vector3.up * 0.03f;
        targetRing.SetAnchor(destination + Vector3.up * 0.03f);

        // And a beacon over the shelf itself, high enough to clear the shelving.
        Vector3 above = destination + Vector3.up * 2.4f;
        if (destinationShelf != null)
        {
            Bounds bounds = ShelfBounds(destinationShelf);
            above = new Vector3(bounds.center.x, bounds.max.y + 0.85f, bounds.center.z);
        }

        beacon = GuideMarker.CreateBeacon("EscortBeacon", beaconColour, 0.35f);
        beacon.transform.position = above;
        beacon.SetAnchor(above);
    }

    static Bounds ShelfBounds(ShelfUnit shelf)
    {
        var renderers = shelf.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(shelf.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    void Finish(string parting, float lingerSeconds)
    {
        if (bubble != null && !string.IsNullOrEmpty(parting) && lingerSeconds > 0f)
        {
            bubble.Show(parting);
            StartCoroutine(DestroyBubbleAfter(bubble, lingerSeconds));
            bubble = null;
        }

        Cleanup();
    }

    static IEnumerator DestroyBubbleAfter(SpeechBubble target, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (target != null) Destroy(target.gameObject);
    }

    void Cleanup()
    {
        SetStage(Stage.None);

        npc.SetForcedHighlight(false);

        if (bubble != null) { Destroy(bubble.gameObject); bubble = null; }
        if (radiusRing != null) { Destroy(radiusRing.gameObject); radiusRing = null; }
        if (targetRing != null) { Destroy(targetRing.gameObject); targetRing = null; }
        if (beacon != null) { Destroy(beacon.gameObject); beacon = null; }

        if (agent != null && agent.isOnNavMesh)
        {
            if (originalStoppingDistance > 0f) agent.stoppingDistance = originalStoppingDistance;
            agent.isStopped = false;
        }
    }

    void StopWalking()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        agent.ResetPath();
        agent.isStopped = true;
    }

    void SetStage(Stage stage)
    {
        if (CurrentStage == stage) return;
        CurrentStage = stage;

        // Anything other than None means the player still owes this customer an answer.
        bool pending = stage != Stage.None;
        if (pending != counted)
        {
            counted = pending;
            PendingCount = Mathf.Max(0, PendingCount + (pending ? 1 : -1));
            TaskManager.NotifyWorldChanged();
        }
    }

    // --- player-facing ------------------------------------------------------

    // Returns true when this swallowed the interaction, so CustomerNPC doesn't also
    // treat the same key press as serving them at the till.
    public bool TryTalk()
    {
        if (CurrentStage == Stage.Asking && PlayerWithin(talkRange))
        {
            talkRequested = true;
            return true;
        }

        // Mid-conversation the coroutine reads the key itself; just don't fall through.
        return CurrentStage == Stage.Talking;
    }

    public string GetPrompt()
    {
        if (CurrentStage == Stage.Asking) return "Talk";
        return string.Empty;
    }

    bool PlayerWithin(float range)
    {
        Transform player = npc.PlayerTransform;
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= range;
    }

    static bool Pressed(KeyCode key) => Input.GetKeyDown(key);

    void OnDisable()
    {
        // Don't leave a despawned customer counted as still asking.
        if (counted)
        {
            counted = false;
            PendingCount = Mathf.Max(0, PendingCount - 1);
            TaskManager.NotifyWorldChanged();
        }
    }
}
