using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float sprintSpeed = 2.6f;
    [SerializeField] private float acceleration = 4f;
    [SerializeField] private float deceleration = 3.5f;
    [SerializeField] private float rotationSpeed = 7f;
    [SerializeField] private float gravity = -3.2f;
    [SerializeField] private float groundedStickSpeed = -2f;

    [Header("Lunar Hop (animation-only)")]
    [SerializeField] private float hopDuration = 0.55f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private LayerMask groundMask;

    [SerializeField] private bool logRuntimeDebug;
    [SerializeField] private float debugLogInterval = 0.5f;

    [Header("Runtime Debug")]
    [SerializeField] private bool debugIsGrounded;
    [SerializeField] private float debugVerticalSpeed;
    [SerializeField] private float debugSpeed;
    [SerializeField] private string debugAnimatorStateName;
    [SerializeField] private Vector3 debugGroundCheckWorldPosition;
    [SerializeField] private Vector3 debugCapsuleBottomWorldPosition;

    private CharacterController characterController;
    private Transform cameraTransform;
    private Vector2 moveInput;
    private Vector3 moveDirection;
    private Vector3 horizontalVelocity;
    private float verticalSpeed;
    private bool isGrounded;
    private float airborneTime;
    private bool isHopping;
    private float hopTimer;
    private float nextDebugLogTime;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        ApplyDefaultGroundMaskIfNeeded();
        EnsureGroundCheck();
        UpdateGrounded();
        SyncDebugFields();

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        UpdateGrounded();
        ReadInput();
        UpdateRotation();
        HandleJumpInput();
        ApplyMovement();
        UpdateAnimator();
        SyncDebugFields();
        LogDebugIfNeeded();
    }

    private void ReadInput()
    {
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        moveDirection = GetCameraRelativeDirection(moveInput);
    }

    private void HandleJumpInput()
    {
        // Animation-only hop: lock out movement for the hop duration so the in-place vertical
        // clip does not slide. No vertical jump velocity is applied.
        if (isGrounded && !isHopping && Input.GetButtonDown("Jump"))
        {
            isHopping = true;
            hopTimer = hopDuration;
            horizontalVelocity = Vector3.zero;

            if (animator != null)
            {
                animator.ResetTrigger("Jump");
                animator.SetTrigger("Jump");
            }
        }
    }

    private void UpdateRotation()
    {
        // No steering while the hop plays; camera is a separate script and stays free.
        if (isHopping || moveDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void UpdateGrounded()
    {
        isGrounded = groundCheck != null && Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

        if (isGrounded)
        {
            airborneTime = 0f;

            // Small downward stick keeps the CharacterController firmly grounded, including
            // throughout the in-place hop. Never pushes the player up.
            if (verticalSpeed < 0f)
            {
                verticalSpeed = groundedStickSpeed;
            }
        }
        else
        {
            airborneTime += Time.deltaTime;
        }
    }

    private void ApplyMovement()
    {
        if (isHopping)
        {
            hopTimer -= Time.deltaTime;
            if (hopTimer <= 0f)
            {
                isHopping = false;
            }
        }

        if (isHopping)
        {
            // Movement is locked during the in-place hop: hold horizontal velocity at zero so
            // the astronaut does not slide while the vertical clip plays.
            horizontalVelocity = Vector3.zero;
        }
        else
        {
            // Normal grounded movement: smoothly accelerate/decelerate toward the input.
            float targetSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
            Vector3 desiredVelocity = moveDirection * targetSpeed;
            float rate = desiredVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude ? acceleration : deceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, rate * Time.deltaTime);
        }

        // Gravity applies only while airborne (real falls). Grounded vertical speed is held at
        // the stick value by UpdateGrounded. No jump velocity is ever applied.
        if (!isGrounded)
        {
            verticalSpeed += gravity * Time.deltaTime;
        }

        Vector3 motion = horizontalVelocity;
        motion.y = verticalSpeed;
        characterController.Move(motion * Time.deltaTime);
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        float normalizedSpeed = sprintSpeed > 0f ? Mathf.Clamp01(horizontalVelocity.magnitude / sprintSpeed) : 0f;
        animator.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
        animator.SetBool("Grounded", isGrounded);
        animator.SetFloat("VerticalSpeed", verticalSpeed);
        animator.SetFloat("AirborneTime", airborneTime);
    }

    private void EnsureGroundCheck()
    {
        if (groundCheck != null)
        {
            return;
        }

        Transform existing = transform.Find("GroundCheck");
        if (existing != null)
        {
            groundCheck = existing;
            return;
        }

        GameObject check = new GameObject("GroundCheck");
        check.transform.SetParent(transform);
        check.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        check.transform.localRotation = Quaternion.identity;
        groundCheck = check.transform;
    }

    private void ApplyDefaultGroundMaskIfNeeded()
    {
        if (groundMask.value != 0)
        {
            return;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        groundMask = groundLayer >= 0 ? 1 << groundLayer : 1 << 0;
    }

    private void SyncDebugFields()
    {
        debugIsGrounded = isGrounded;
        debugVerticalSpeed = verticalSpeed;
        debugSpeed = horizontalVelocity.magnitude;
        debugGroundCheckWorldPosition = groundCheck != null ? groundCheck.position : Vector3.zero;
        debugCapsuleBottomWorldPosition = GetCapsuleBottomWorldPosition();

        if (animator != null && animator.isActiveAndEnabled)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            debugAnimatorStateName = ResolveAnimatorStateName(stateInfo);
        }
    }

    private string ResolveAnimatorStateName(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.IsName("Locomotion"))
        {
            return "Locomotion";
        }

        if (stateInfo.IsName("Lunar_Hop"))
        {
            return "Lunar_Hop";
        }

        if (stateInfo.IsName("Falling"))
        {
            return "Falling";
        }

        if (stateInfo.IsName("Landing"))
        {
            return "Landing";
        }

        return stateInfo.shortNameHash.ToString();
    }

    private Vector3 GetCapsuleBottomWorldPosition()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (characterController == null)
        {
            return transform.position;
        }

        float bottomOffset = (characterController.height * 0.5f) - characterController.radius;
        Vector3 localBottom = characterController.center + Vector3.down * bottomOffset;
        return transform.TransformPoint(localBottom);
    }

    private void LogDebugIfNeeded()
    {
        if (!logRuntimeDebug || Time.time < nextDebugLogTime)
        {
            return;
        }

        nextDebugLogTime = Time.time + debugLogInterval;
        Debug.Log($"Player grounded={debugIsGrounded}, airborneTime={airborneTime:F2}, verticalSpeed={debugVerticalSpeed:F2}, speed={debugSpeed:F2}, state={debugAnimatorStateName}, groundCheck={debugGroundCheckWorldPosition}, capsuleBottom={debugCapsuleBottomWorldPosition}");
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (cameraTransform == null)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return (forward * input.y + right * input.x).normalized;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetCapsuleBottomWorldPosition(), 0.06f);
    }
}
