using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float jumpHeight = 2.2f;
    [SerializeField] private float gravity = -4.9f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float jumpTakeoffDelay = 0.18f;
    [SerializeField] private bool useJumpTakeoffDelayFallback = true;
    [SerializeField] private bool logRuntimeDebug;
    [SerializeField] private float debugLogInterval = 0.5f;

    [Header("Runtime Debug")]
    [SerializeField] private bool debugIsGrounded;
    [SerializeField] private bool debugIsPreparingJump;
    [SerializeField] private float debugVerticalSpeed;
    [SerializeField] private float debugSpeed;
    [SerializeField] private string debugAnimatorStateName;
    [SerializeField] private Vector3 debugGroundCheckWorldPosition;
    [SerializeField] private Vector3 debugCapsuleBottomWorldPosition;

    private CharacterController characterController;
    private Transform cameraTransform;
    private Vector2 moveInput;
    private Vector3 moveDirection;
    private float currentSpeed;
    private float verticalSpeed;
    private bool isGrounded;
    private bool isPreparingJump;
    private float jumpPrepareTimer;
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

    public void OnJumpTakeoff()
    {
        if (!isPreparingJump)
        {
            return;
        }

        verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
        isPreparingJump = false;
        isGrounded = false;
    }

    private void ReadInput()
    {
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        moveDirection = GetCameraRelativeDirection(moveInput);
        currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
    }

    private void HandleJumpInput()
    {
        if (isGrounded && !isPreparingJump && Input.GetButtonDown("Jump"))
        {
            isPreparingJump = true;
            jumpPrepareTimer = 0f;

            if (animator != null)
            {
                animator.ResetTrigger("Jump");
                animator.SetTrigger("Jump");
            }
        }

        if (!isPreparingJump || !useJumpTakeoffDelayFallback)
        {
            return;
        }

        jumpPrepareTimer += Time.deltaTime;
        if (jumpPrepareTimer >= jumpTakeoffDelay)
        {
            OnJumpTakeoff();
        }
    }

    private void UpdateRotation()
    {
        if (moveDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void UpdateGrounded()
    {
        isGrounded = groundCheck != null && Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

        if (isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -1f;
        }
    }

    private void ApplyMovement()
    {
        if (!isPreparingJump)
        {
            verticalSpeed += gravity * Time.deltaTime;
        }
        else if (isGrounded)
        {
            verticalSpeed = -1f;
        }

        Vector3 motion = moveDirection * currentSpeed;
        motion.y = verticalSpeed;
        characterController.Move(motion * Time.deltaTime);
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        float normalizedSpeed = sprintSpeed > 0f ? Mathf.Clamp01(moveInput.magnitude * currentSpeed / sprintSpeed) : 0f;
        animator.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
        animator.SetBool("Grounded", isGrounded);
        animator.SetFloat("VerticalSpeed", verticalSpeed);
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
        debugIsPreparingJump = isPreparingJump;
        debugVerticalSpeed = verticalSpeed;
        debugSpeed = sprintSpeed > 0f ? Mathf.Clamp01(moveInput.magnitude * currentSpeed / sprintSpeed) : 0f;
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

        if (stateInfo.IsName("JumpStart"))
        {
            return "JumpStart";
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
        Debug.Log($"Player grounded={debugIsGrounded}, preparingJump={debugIsPreparingJump}, verticalSpeed={debugVerticalSpeed:F2}, speed={debugSpeed:F2}, state={debugAnimatorStateName}, groundCheck={debugGroundCheckWorldPosition}, capsuleBottom={debugCapsuleBottomWorldPosition}");
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
