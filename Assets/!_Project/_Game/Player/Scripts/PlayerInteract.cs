using UnityEngine;

public interface IInteractable
{
    void Interact(PlayerInteract player);
    string GetPrompt();
}

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction")]
    public Transform rayOrigin;
    public float interactRange = 2.5f;
    public LayerMask interactLayer;

    [HideInInspector] public CarrySlot carrySlot;

    IInteractable currentTarget;

    void Awake()
    {
        carrySlot = GetComponent<CarrySlot>();
        if (rayOrigin == null)
            rayOrigin = Camera.main.transform;
    }

    void Update()
    {
        HandleSlotSwitching();
        CheckForInteractable();

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentTarget != null)
            {
                // Looking at something interactable — interact with it (pickup or place)
                currentTarget.Interact(this);
            }
            else if (carrySlot.IsCarrying)
            {
                // Not looking at anything — drop the active item
                carrySlot.Drop();
            }
        }
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

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayer))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                currentTarget = interactable;
                return;
            }
        }

        currentTarget = null;
    }
}