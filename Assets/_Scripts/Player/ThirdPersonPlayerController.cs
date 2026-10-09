using UnityEngine;
using UnityEngine.Serialization;

// Apollo-inspired, root-motion-driven third-person controller.
// Faster but still moon-like:
// - Lunar gravity stays scientifically correct at -1.62 m/s².
// - Horizontal speed is increased through root-motion scaling, not fake Earth gravity.
// - Concept: lighter astronaut + lighter suit, so movement feels less bulky.
// - Horizontal movement still comes ONLY from animation root motion.
// - Vertical movement is scripted lunar gravity.
// - Committed actions still lock input until animation ends.
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("Locomotion (root-motion driven)")]
    [SerializeField] private float accelerationRate = 2.2f;
    [SerializeField] private float decelerationRate = 1.8f;
    [SerializeField] private float walkRotationSpeed = 6.5f;
    [SerializeField] private float lopeRotationSpeed = 4.5f;

    [Header("Idle committed turns (only when nearly stopped)")]
    [Range(0f, 120f)] [SerializeField] private float idleTurnMinAngle = 35f;
    [Range(90f, 180f)] [SerializeField] private float idleTurn180MinAngle = 120f;
    [SerializeField] private float idleTurnMaxSpeed = 0.15f;

    [Header("Hard stop (committed StopWalk)")]
    [SerializeField] private float hardStopSpeedThreshold = 0.65f;

    [Header("Gravity / Ground")]
    [SerializeField] private float gravity = -1.62f;
    [SerializeField] private float groundedStickSpeed = -0.75f;

    [Header("Lunar Jump")]
    [SerializeField] private float jumpHeight = 1.35f;
    [FormerlySerializedAs("forwardJumpRootScale")]
    [SerializeField] private float jumpForwardBoost = 1.20f;
    [SerializeField] private float airMoveMultiplier = 1.10f;
    [SerializeField] private float airControl = 0.65f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;
    [SerializeField] private float jumpTakeoffNormalizedTime = 0.18f;
    [SerializeField] private float maxAirHorizontalSpeed = 3.25f;
    [SerializeField] private float postTakeoffGroundIgnoreTime = 0.08f;
    [SerializeField] private bool debugJump;

    [Header("Falling Animation")]
    [Tooltip("Drop below the highest airborne point (meters) before the falling animation plays. Filters out bumps, steps and slopes; jumps bypass this check.")]
    [SerializeField] private float fallAnimationMinDrop = 0.5f;

    [Header("Root Motion")]
    [SerializeField] private bool useRootMotion = true;

    [Tooltip("Normal walking root-motion multiplier. Faster because astronaut + suit are lighter.")]
    [SerializeField] private float walkRootScale = 1.18f;

    [Tooltip("Emergency lope root-motion multiplier. Keep below 1.45 for believable lunar movement.")]
    [SerializeField] private float lopeRootScale = 1.28f;

    [SerializeField] private float hopRootScale = 1.0f;

    [Header("Committed Turn Durations (seconds)")]
    [SerializeField] private float sideTurnDuration = 1.0f;
    [SerializeField] private float turn180Duration = 1.4f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private LayerMask groundMask;

    [Header("Runtime Debug")]
    [SerializeField] private bool debugIsGrounded;
    [SerializeField] private float debugSpeedParam;
    [SerializeField] private float debugAirborneTime;
    [SerializeField] private string debugCommittedAction = "None";

    private enum CommittedAction
    {
        None,
        Hop,
        ForwardJump,
        TurnLeft,
        TurnRight,
        Turn180,
        Stop
    }

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int AirborneTimeHash = Animator.StringToHash("AirborneTime");
    private static readonly int FallingHash = Animator.StringToHash("Falling");

    private static readonly int HopTrig = Animator.StringToHash("Hop");
    private static readonly int ForwardJumpTrig = Animator.StringToHash("ForwardJump");
    private static readonly int TurnLeftTrig = Animator.StringToHash("TurnLeft");
    private static readonly int TurnRightTrig = Animator.StringToHash("TurnRight");
    private static readonly int Turn180Trig = Animator.StringToHash("Turn180");
    private static readonly int StopTrig = Animator.StringToHash("Stop");

    private static readonly int HopState = Animator.StringToHash("JumpHop");
    private static readonly int ForwardJumpState = Animator.StringToHash("ForwardJump");
    private static readonly int TurnLeftState = Animator.StringToHash("TurnLeft");
    private static readonly int TurnRightState = Animator.StringToHash("TurnRight");
    private static readonly int Turn180State = Animator.StringToHash("Turn180");
    private static readonly int StopState = Animator.StringToHash("StopWalk");

    private CharacterController characterController;
    private Transform cameraTransform;

    private Vector2 moveInput;
    private Vector3 desiredDirection;

    private float speedParam;
    private float verticalSpeed;
    private bool isGrounded;
    private float airborneTime;
    private float lastGroundedTime;
    private float jumpBufferCounter;
    private bool jumpedThisFrame;
    private bool wasGroundedLastFrame;
    private Vector3 airHorizontalVelocity;
    private float airbornePeakHeight;

    private bool isCommitted;
    private CommittedAction committedAction = CommittedAction.None;
    private int committedStateHash;
    private bool committedStateEntered;
    private float committedFallbackTimer;
    private float currentRootScale = 1f;
    private bool pendingJump;
    private bool pendingMovingJump;
    private bool jumpTakeoffPerformed;
    private bool warnedMissingJumpEvent;
    private bool landingReported;
    private float ignoreGroundedUntilTime;

    private bool isTurning;
    private Quaternion turnTargetRotation;
    private float turnDegPerSecond;

    private bool requireInputRelease;
    private bool prevHasInput;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        ApplyDefaultGroundMaskIfNeeded();
        EnsureGroundCheck();

        if (animator != null)
        {
            animator.applyRootMotion = useRootMotion;
        }

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        airbornePeakHeight = transform.position.y;
        UpdateGrounded();
    }

    private void OnValidate()
    {
        jumpHeight = Mathf.Max(0.1f, jumpHeight);
        jumpForwardBoost = Mathf.Max(0f, jumpForwardBoost);
        airMoveMultiplier = Mathf.Max(0f, airMoveMultiplier);
        airControl = Mathf.Clamp01(airControl);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
        jumpTakeoffNormalizedTime = Mathf.Clamp01(jumpTakeoffNormalizedTime);
        maxAirHorizontalSpeed = Mathf.Max(jumpForwardBoost, maxAirHorizontalSpeed);
        postTakeoffGroundIgnoreTime = Mathf.Max(0f, postTakeoffGroundIgnoreTime);
        fallAnimationMinDrop = Mathf.Max(0.05f, fallAnimationMinDrop);
    }

    private void Update()
    {
        UpdateGrounded();
        DetectLanding();
        ReadInput();
        UpdateJumpBuffer();

        if (isCommitted)
        {
            UpdateCommittedAction();
        }
        else
        {
            UpdateLocomotion();
        }

        UpdateVertical();
        UpdateAnimatorParameters();
        SyncDebug();
        wasGroundedLastFrame = isGrounded;
        jumpedThisFrame = false;
    }

    public void ApplyRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
    {
        if (!useRootMotion || characterController == null)
        {
            return;
        }

        float locomotionScale = Mathf.Lerp(walkRootScale, lopeRootScale, Mathf.Clamp01(speedParam));
        float finalRootScale = isCommitted ? currentRootScale : locomotionScale;
        if (!isGrounded)
        {
            finalRootScale *= airMoveMultiplier;
        }

        Vector3 motion = new Vector3(deltaPosition.x, 0f, deltaPosition.z) * finalRootScale;
        UpdateAirHorizontalVelocity();
        motion += airHorizontalVelocity * Time.deltaTime;
        motion.y = verticalSpeed * Time.deltaTime;

        characterController.Move(motion);

        // Root rotation is intentionally ignored.
        // Facing is handled by scripted rotation for more stable lunar movement.
    }

    private void ReadInput()
    {
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        desiredDirection = GetCameraRelativeDirection(moveInput);
    }

    private void UpdateGrounded()
    {
        bool detectedGrounded =
            groundCheck != null &&
            Physics.CheckSphere(
                groundCheck.position,
                groundCheckRadius,
                groundMask,
                QueryTriggerInteraction.Ignore
            );

        isGrounded = Time.time >= ignoreGroundedUntilTime && detectedGrounded;

        if (isGrounded)
        {
            airborneTime = 0f;
            lastGroundedTime = Time.time;
            airbornePeakHeight = transform.position.y;
        }
        else
        {
            airborneTime += Time.deltaTime;
            airbornePeakHeight = Mathf.Max(airbornePeakHeight, transform.position.y);
        }
    }

    private bool IsFallingForAnimation()
    {
        if (isGrounded || verticalSpeed >= 0f)
        {
            return false;
        }

        if (jumpTakeoffPerformed)
        {
            return true;
        }

        float dropFromPeak = airbornePeakHeight - transform.position.y;
        return dropFromPeak >= fallAnimationMinDrop;
    }

    private void UpdateVertical()
    {
        if (isGrounded && !jumpedThisFrame && !jumpTakeoffPerformed)
        {
            verticalSpeed = groundedStickSpeed;
            airHorizontalVelocity = Vector3.zero;
        }
        else
        {
            verticalSpeed += gravity * Time.deltaTime;
        }
    }

    private void UpdateLocomotion()
    {
        bool hasInput = moveInput.sqrMagnitude > 0.01f;

        if (requireInputRelease)
        {
            if (!hasInput)
            {
                requireInputRelease = false;
            }
            else
            {
                hasInput = false;
            }
        }

        bool newlyPressed = hasInput && !prevHasInput;
        bool released = !hasInput && prevHasInput;
        prevHasInput = hasInput;

        if (CanConsumeBufferedJump())
        {
            RequestJump(hasInput);
            return;
        }

        if (hasInput && newlyPressed && isGrounded && speedParam < idleTurnMaxSpeed)
        {
            float angle = Vector3.SignedAngle(transform.forward, desiredDirection, Vector3.up);
            float absAngle = Mathf.Abs(angle);

            if (absAngle > idleTurn180MinAngle)
            {
                StartCommitted(CommittedAction.Turn180, angle);
                return;
            }

            if (absAngle > idleTurnMinAngle)
            {
                StartCommitted(angle < 0f ? CommittedAction.TurnLeft : CommittedAction.TurnRight, angle);
                return;
            }
        }

        if (released && isGrounded && speedParam > hardStopSpeedThreshold)
        {
            StartCommitted(CommittedAction.Stop);
            return;
        }

        if (hasInput)
        {
            Quaternion target = Quaternion.LookRotation(desiredDirection, Vector3.up);
            float rotSpeed = Mathf.Lerp(walkRotationSpeed, lopeRotationSpeed, Mathf.Clamp01(speedParam));
            transform.rotation = Quaternion.Slerp(transform.rotation, target, rotSpeed * Time.deltaTime);
        }

        float targetSpeed = !hasInput ? 0f : (Input.GetKey(KeyCode.LeftShift) ? 1.0f : 0.45f);
        float rate = targetSpeed > speedParam ? accelerationRate : decelerationRate;

        speedParam = Mathf.MoveTowards(speedParam, targetSpeed, rate * Time.deltaTime);
    }

    private void StartCommitted(CommittedAction action, float signedAngle = 0f)
    {
        isCommitted = true;
        committedAction = action;
        committedFallbackTimer = 0f;
        committedStateEntered = false;
        currentRootScale = 1f;
        isTurning = false;

        switch (action)
        {
            case CommittedAction.Hop:
                currentRootScale = hopRootScale;
                committedStateHash = HopState;
                FireTrigger(HopTrig);
                break;

            case CommittedAction.ForwardJump:
                currentRootScale = jumpForwardBoost;
                committedStateHash = ForwardJumpState;
                FireTrigger(ForwardJumpTrig);
                break;

            case CommittedAction.TurnLeft:
                committedStateHash = TurnLeftState;
                SetupScriptedTurn(signedAngle, sideTurnDuration);
                FireTrigger(TurnLeftTrig);
                break;

            case CommittedAction.TurnRight:
                committedStateHash = TurnRightState;
                SetupScriptedTurn(signedAngle, sideTurnDuration);
                FireTrigger(TurnRightTrig);
                break;

            case CommittedAction.Turn180:
                committedStateHash = Turn180State;
                SetupScriptedTurn(signedAngle, turn180Duration);
                FireTrigger(Turn180Trig);
                break;

            case CommittedAction.Stop:
                committedStateHash = StopState;
                FireTrigger(StopTrig);
                break;
        }
    }

    private void SetupScriptedTurn(float signedAngle, float duration)
    {
        isTurning = true;
        turnTargetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
        turnDegPerSecond = Mathf.Max(1f, Mathf.Abs(signedAngle)) / Mathf.Max(0.1f, duration);
    }

    private void UpdateCommittedAction()
    {
        bool jumpAction = committedAction == CommittedAction.Hop || committedAction == CommittedAction.ForwardJump;

        if (isTurning)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                turnTargetRotation,
                turnDegPerSecond * Time.deltaTime
            );
        }

        committedFallbackTimer += Time.deltaTime;

        bool ended = false;

        if (animator != null)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);

            if (current.shortNameHash == committedStateHash)
            {
                committedStateEntered = true;

                if (pendingJump && !jumpTakeoffPerformed && current.normalizedTime >= jumpTakeoffNormalizedTime)
                {
                    if (!warnedMissingJumpEvent)
                    {
                        warnedMissingJumpEvent = true;
                        LogJump("Jump animation event missing; using normalized-time fallback takeoff.");
                    }

                    PerformJumpTakeoff("normalized-time fallback");
                }

                if (current.normalizedTime >= 0.90f)
                {
                    ended = true;
                }
            }
            else if (committedStateEntered)
            {
                if (pendingJump && !jumpTakeoffPerformed)
                {
                    CancelPendingJump("jump animation exited before takeoff");
                }

                ended = true;
            }
        }
        else if (pendingJump && !jumpTakeoffPerformed)
        {
            PerformJumpTakeoff("missing animator immediate fallback");
        }

        if (!ended && committedFallbackTimer > 3f)
        {
            if (pendingJump && !jumpTakeoffPerformed)
            {
                CancelPendingJump("jump animation timed out before takeoff");
            }

            ended = true;
        }

        if (jumpAction && jumpTakeoffPerformed && !isGrounded)
        {
            ended = false;
        }

        if (ended)
        {
            EndCommitted();
        }
    }

    private void EndCommitted()
    {
        CommittedAction ending = committedAction;

        isCommitted = false;
        committedAction = CommittedAction.None;
        isTurning = false;
        currentRootScale = 1f;
        pendingJump = false;

        if (ending == CommittedAction.Stop)
        {
            speedParam = 0f;
            requireInputRelease = true;
        }
    }

    private void UpdateJumpBuffer()
    {
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
            LogJump("Jump input buffered.");
            return;
        }

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    private bool CanConsumeBufferedJump()
    {
        return jumpBufferCounter > 0f
            && !jumpedThisFrame
            && Time.time - lastGroundedTime <= coyoteTime;
    }

    private void ConsumeJumpBuffer()
    {
        jumpBufferCounter = 0f;
        lastGroundedTime = float.NegativeInfinity;
    }

    private void RequestJump(bool movingJump)
    {
        if (pendingJump || jumpTakeoffPerformed)
        {
            LogJump("Jump request ignored because a jump is already pending or airborne.");
            return;
        }

        pendingJump = true;
        pendingMovingJump = movingJump;
        jumpTakeoffPerformed = false;
        landingReported = false;

        LogJump(movingJump ? "Forward jump requested; waiting for animation takeoff." : "Hop requested; waiting for animation takeoff.");
        StartCommitted(movingJump ? CommittedAction.ForwardJump : CommittedAction.Hop);
    }

    public void AnimationJumpTakeoff()
    {
        LogJump("AnimationJumpTakeoff event received.");
        PerformJumpTakeoff("animation event");
    }

    private void PerformJumpTakeoff(string source)
    {
        if (!pendingJump)
        {
            LogJump($"Takeoff ignored from {source}; no jump is pending.");
            return;
        }

        if (jumpTakeoffPerformed)
        {
            LogJump($"Takeoff ignored from {source}; jump already took off.");
            return;
        }

        bool stillAllowed = isCommitted
            || isGrounded
            || Time.time - lastGroundedTime <= coyoteTime;
        if (!stillAllowed)
        {
            CancelPendingJump($"takeoff from {source} failed coyote/ground check");
            return;
        }

        verticalSpeed = Mathf.Sqrt(2f * Mathf.Abs(gravity) * Mathf.Max(0.01f, jumpHeight));
        jumpedThisFrame = true;
        jumpTakeoffPerformed = true;
        pendingJump = false;
        isGrounded = false;
        ignoreGroundedUntilTime = Time.time + postTakeoffGroundIgnoreTime;
        ConsumeJumpBuffer();

        if (!pendingMovingJump || desiredDirection.sqrMagnitude <= 0.01f)
        {
            airHorizontalVelocity = Vector3.zero;
            LogJump($"Jump takeoff applied from {source}. verticalSpeed={verticalSpeed:0.###}");
            return;
        }

        Vector3 boostDirection = desiredDirection.normalized;
        airHorizontalVelocity = Vector3.ClampMagnitude(boostDirection * jumpForwardBoost, maxAirHorizontalSpeed);
        LogJump($"Jump takeoff applied from {source}. verticalSpeed={verticalSpeed:0.###}, airBoost={airHorizontalVelocity.magnitude:0.###}");
    }

    private void CancelPendingJump(string reason)
    {
        if (!pendingJump)
        {
            return;
        }

        pendingJump = false;
        pendingMovingJump = false;
        jumpTakeoffPerformed = false;
        LogJump("Jump cancelled/interrupted: " + reason);
    }

    private void UpdateAirHorizontalVelocity()
    {
        if (isGrounded)
        {
            airHorizontalVelocity = Vector3.zero;
            return;
        }

        if (desiredDirection.sqrMagnitude <= 0.01f || airControl <= 0f)
        {
            airHorizontalVelocity = Vector3.ClampMagnitude(airHorizontalVelocity, maxAirHorizontalSpeed);
            return;
        }

        Vector3 controlledVelocity = airHorizontalVelocity + desiredDirection.normalized * (airControl * Time.deltaTime);
        airHorizontalVelocity = Vector3.ClampMagnitude(controlledVelocity, maxAirHorizontalSpeed);
    }

    private void DetectLanding()
    {
        if (!isGrounded || wasGroundedLastFrame || !jumpTakeoffPerformed || landingReported)
        {
            return;
        }

        jumpTakeoffPerformed = false;
        landingReported = true;
        airHorizontalVelocity = Vector3.zero;
        LogJump("Landing detected.");
    }

    private void FireTrigger(int trigger)
    {
        if (animator == null)
        {
            return;
        }

        animator.ResetTrigger(HopTrig);
        animator.ResetTrigger(ForwardJumpTrig);
        animator.ResetTrigger(TurnLeftTrig);
        animator.ResetTrigger(TurnRightTrig);
        animator.ResetTrigger(Turn180Trig);
        animator.ResetTrigger(StopTrig);

        animator.SetTrigger(trigger);
    }

    private void UpdateAnimatorParameters()
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat(SpeedHash, speedParam);
        animator.SetBool(GroundedHash, isGrounded);
        animator.SetFloat(VerticalSpeedHash, verticalSpeed);
        animator.SetFloat(AirborneTimeHash, airborneTime);
        animator.SetBool(FallingHash, IsFallingForAnimation());
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

    private void SyncDebug()
    {
        debugIsGrounded = isGrounded;
        debugSpeedParam = speedParam;
        debugAirborneTime = airborneTime;
        debugCommittedAction = isCommitted ? committedAction.ToString() : "None";
    }

    private void LogJump(string message)
    {
        if (debugJump)
        {
            Debug.Log("[ThirdPersonPlayerController Jump] " + message, this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
