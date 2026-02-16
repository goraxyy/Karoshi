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
        CheckForInteractable();

        if (Input.GetKeyDown(KeyCode.E) && currentTarget != null)
            currentTarget.Interact(this);
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
                // TODO: Show UI prompt with currentTarget.GetPrompt()
                return;
            }
        }

        currentTarget = null;
    }
}