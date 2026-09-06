using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 6.5f;
    public float crouchSpeed = 2.5f;
    public float gravity = -20f;
    public float jumpHeight = 1.2f;

    [Header("Look")]
    public Transform cameraRoot;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;

    [Header("Crouch")]
    public float crouchControllerHeight = 0.8f;
    public float crouchCameraY = 0.35f;
    public float standCameraY = 1.6f;

    [Header("Stamina")]
    [Tooltip("Sprinting is disabled when this hits empty. Found automatically if unset.")]
    public BurnoutSystem burnout;

    CharacterController controller;
    Vector3 velocity;
    float pitch;
    bool isCrouching;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (burnout == null) burnout = FindAnyObjectByType<BurnoutSystem>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
        HandleCrouch();
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);

        if (cameraRoot != null)
            cameraRoot.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 moveDir = (transform.right * horizontal + transform.forward * vertical).normalized;

        float speed = walkSpeed;
        if (IsSprinting())
            speed = sprintSpeed;
        else if (isCrouching)
            speed = crouchSpeed;

        controller.Move(moveDir * speed * Time.deltaTime);

        if (isGrounded && Input.GetKeyDown(KeyCode.Space) && !isCrouching)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            if (isCrouching)
            {
                if (CanStandUp())
                    SetCrouch(false);
            }
            else
            {
                SetCrouch(true);
            }
        }
    }

    void SetCrouch(bool crouch)
    {
        isCrouching = crouch;

        controller.height = crouch ? crouchControllerHeight : 2f;
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

        if (cameraRoot != null)
            cameraRoot.localPosition = new Vector3(0f, crouch ? crouchCameraY : standCameraY, 0f);
    }

    bool CanStandUp()
    {
        float skinWidth = 0.1f;
        float checkRadius = controller.radius - skinWidth;
        Vector3 checkStart = transform.position + Vector3.up * (controller.radius + skinWidth);
        float checkDistance = 2f - controller.radius * 2f;

        return !Physics.SphereCast(checkStart, checkRadius, Vector3.up, out _, checkDistance);
    }

    // Holding shift isn't enough — a burnt-out employee can only walk.
    public bool IsSprinting()
    {
        if (isCrouching) return false;
        if (!Input.GetKey(KeyCode.LeftShift)) return false;
        return burnout == null || burnout.CanSprint;
    }
}