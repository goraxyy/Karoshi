using UnityEngine;

public interface IInteractable
{
    void Interact(PlayerInteract player);
    string GetPrompt();
}

// Optional add-on for interactables that want to react to being looked at.
public interface IHoverable
{
    void OnHoverEnter();
    void OnHoverExit();
}

// Optional add-on for interactables completed by holding E rather than tapping it.
public interface IHoldInteractable
{
    bool CanHold(PlayerInteract player);
    float HoldDuration { get; }
    void OnHoldProgress(float normalised);
    void OnHoldComplete(PlayerInteract player);
    void OnHoldCancelled();
}

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction")]
    public Transform rayOrigin;
    public float interactRange = 2.5f;

    [Tooltip("Customers are talked to across a counter, so they reach further than shelves do.")]
    public float customerInteractRange = 5f;

    public LayerMask interactLayer;

    [Header("Input")]
    public KeyCode dropKey = KeyCode.Q;

    [HideInInspector] public CarrySlot carrySlot;
    [HideInInspector] public PlayerTools tools;

    IInteractable currentTarget;
    IHoldInteractable holdTarget;
    float holdTimer;

    public IInteractable CurrentTarget => currentTarget;

    void Awake()
    {
        carrySlot = GetComponent<CarrySlot>();
        tools = GetComponent<PlayerTools>();
        if (rayOrigin == null)
            rayOrigin = Camera.main.transform;
    }

    void Update()
    {
        HandleSlotSwitching();

        // A target can be destroyed while we're looking at it (e.g. a customer despawning).
        if (!IsAlive(currentTarget))
            currentTarget = null;

        CheckForInteractable();

        if (HandleHoldInteraction())
            return;   // a hold is in progress; don't also fire tap interactions

        // E is purely "use what I'm looking at".
        if (Input.GetKeyDown(KeyCode.E) && currentTarget != null)
            currentTarget.Interact(this);

        // Q always drops, whether or not something is under the crosshair.
        if (Input.GetKeyDown(dropKey))
            DropHeld();
    }

    void HandleSlotSwitching()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) carrySlot.SetActiveSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) carrySlot.SetActiveSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) carrySlot.SetActiveSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) carrySlot.SetActiveSlot(3);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) carrySlot.SetActiveSlot((carrySlot.activeSlot + 3) % 4);
        if (scroll < 0f) carrySlot.SetActiveSlot((carrySlot.activeSlot + 1) % 4);
    }

    void CheckForInteractable()
    {
        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

        // Normal reach first.
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayer))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                SetTarget(interactable);
                return;
            }
        }

        // Then a longer probe that only customers answer, so they can be served from
        // the far side of a till without shelves becoming reachable from a distance too.
        if (customerInteractRange > interactRange &&
            Physics.Raycast(ray, out RaycastHit farHit, customerInteractRange, interactLayer))
        {
            CustomerNPC customer = farHit.collider.GetComponentInParent<CustomerNPC>();
            if (customer != null)
            {
                SetTarget(customer);
                return;
            }
        }

        SetTarget(null);
    }

    // Q: put down whatever is in hand — a bulky tool first, otherwise the active inventory item.
    void DropHeld()
    {
        if (tools != null && tools.IsHolding)
        {
            // The bin belongs somewhere specific.
            var toolHome = tools.HeldTool.GetComponent<IHomeReturnable>();
            tools.Release();
            toolHome?.ReturnHome();
            return;
        }

        // Everything else just goes on the floor. Tools that belong somewhere are
        // recalled by looking at their snap point and pressing E, not by dropping.
        if (carrySlot != null && carrySlot.IsCarrying)
            carrySlot.Drop();
    }

    // Returns true while a hold-to-use action (mopping) is running.
    bool HandleHoldInteraction()
    {
        var candidate = currentTarget as IHoldInteractable;
        bool eligible = candidate != null && candidate.CanHold(this) && Input.GetKey(KeyCode.E);

        if (!eligible)
        {
            if (holdTarget != null)
            {
                holdTarget.OnHoldCancelled();
                holdTarget = null;
                holdTimer = 0f;
            }
            return false;
        }

        if (!ReferenceEquals(holdTarget, candidate))
        {
            holdTarget?.OnHoldCancelled();
            holdTarget = candidate;
            holdTimer = 0f;
        }

        holdTimer += Time.deltaTime;
        float duration = Mathf.Max(0.01f, holdTarget.HoldDuration);
        holdTarget.OnHoldProgress(Mathf.Clamp01(holdTimer / duration));

        if (holdTimer >= duration)
        {
            IHoldInteractable finished = holdTarget;
            holdTarget = null;
            holdTimer = 0f;
            finished.OnHoldComplete(this);
        }

        return true;
    }

    void SetTarget(IInteractable target)
    {
        if (ReferenceEquals(currentTarget, target)) return;

        if (IsAlive(currentTarget))
            (currentTarget as IHoverable)?.OnHoverExit();

        currentTarget = target;

        (currentTarget as IHoverable)?.OnHoverEnter();
    }

    // Guards against calling into a MonoBehaviour that Unity has already destroyed.
    static bool IsAlive(IInteractable interactable)
    {
        if (interactable == null) return false;
        if (interactable is Object unityObject) return unityObject != null;
        return true;
    }
}