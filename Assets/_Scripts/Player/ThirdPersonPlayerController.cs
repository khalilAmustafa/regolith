using UnityEngine;

// Apollo-inspired, root-motion-driven third-person controller.
// - Horizontal movement comes ONLY from animation root motion, applied via CharacterController.Move
//   inside ApplyRootMotion (called by RootMotionRelay on Astronaut Visual). There is no scripted
//   horizontal velocity, so the two never both move the Player.
// - Vertical is scripted (lunar gravity + grounded stick) and folded into the same Move call.
// - Committed actions (hops, turns, stop) lock input/steering and end on Animator normalizedTime.
// - Turn clips have 0 deg root rotation, so committed turns rotate the Player with scripted rotation.
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("Locomotion (root-motion driven)")]
    [SerializeField] private float accelerationRate = 1.5f;   // Speed param units / second
    [SerializeField] private float decelerationRate = 1.2f;
    [SerializeField] private float walkRotationSpeed = 5f;    // heavy-but-responsive facing at walk
    [SerializeField] private float lopeRotationSpeed = 3f;    // heavier facing at lope

    [Header("Idle committed turns (only when nearly stopped)")]
    [Range(0f, 120f)] [SerializeField] private float idleTurnMinAngle = 35f;
    [Range(90f, 180f)] [SerializeField] private float idleTurn180MinAngle = 120f;
    [SerializeField] private float idleTurnMaxSpeed = 0.15f;

    [Header("Hard stop (committed StopWalk)")]
    [SerializeField] private float hardStopSpeedThreshold = 0.65f;

    [Header("Gravity / Ground")]
    [SerializeField] private float gravity = -1.62f;
    [SerializeField] private float groundedStickSpeed = -0.75f;
    [SerializeField] private float fallAirborneThreshold = 0.30f;

    [Header("Root Motion")]
    [SerializeField] private bool useRootMotion = true;
    [SerializeField] private float forwardJumpRootScale = 0.66f;
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

    private enum CommittedAction { None, Hop, ForwardJump, TurnLeft, TurnRight, Turn180, Stop }

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int AirborneTimeHash = Animator.StringToHash("AirborneTime");

    private static readonly int HopTrig = Animator.StringToHash("Hop");
    private static readonly int ForwardJumpTrig = Animator.StringToHash("ForwardJump");
    private static readonly int TurnLeftTrig = Animator.StringToHash("TurnLeft");
    private static readonly int TurnRightTrig = Animator.StringToHash("TurnRight");
    private static readonly int Turn180Trig = Animator.StringToHash("Turn180");
    private static readonly int StopTrig = Animator.StringToHash("Stop");

    // Committed-state short-name hashes - must match the Animator state names built in Phase 2.
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

    private bool isCommitted;
    private CommittedAction committedAction = CommittedAction.None;
    private int committedStateHash;
    private bool committedStateEntered;
    private float committedFallbackTimer;
    private float currentRootScale = 1f;

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

        UpdateGrounded();
    }

    private void Update()
    {
        UpdateGrounded();
        ReadInput();

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
    }

    // Single point where animation-driven horizontal movement reaches the Player. Vertical
    // (gravity / grounded stick) is folded into the same Move so there is exactly one Move/frame.
    public void ApplyRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
    {
        if (!useRootMotion || characterController == null)
        {
            return;
        }

        Vector3 motion = new Vector3(deltaPosition.x, 0f, deltaPosition.z) * currentRootScale;
        motion.y = verticalSpeed * Time.deltaTime;
        characterController.Move(motion);
        // deltaRotation is intentionally unused: all locomotion/turn clips measured 0 deg root
        // rotation, so facing is handled by scripted rotation instead.
    }

    private void ReadInput()
    {
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        desiredDirection = GetCameraRelativeDirection(moveInput);
    }

    private void UpdateGrounded()
    {
        isGrounded = groundCheck != null && Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

        if (isGrounded)
        {
            airborneTime = 0f;
        }
        else
        {
            airborneTime += Time.deltaTime;
        }
    }

    private void UpdateVertical()
    {
        if (isGrounded)
        {
            verticalSpeed = groundedStickSpeed;
        }
        else
        {
            verticalSpeed += gravity * Time.deltaTime;
        }
    }

    private void UpdateLocomotion()
    {
        bool hasInput = moveInput.sqrMagnitude > 0.01f;

        // After a committed Stop, ignore still-held input until it is released.
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

        // Space: forward jump when moving, in-place hop when essentially still.
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            StartCommitted(hasInput ? CommittedAction.ForwardJump : CommittedAction.Hop);
            return;
        }

        // Committed turn animations are POLISH only: allowed exclusively when essentially
        // stopped and movement is freshly pressed toward a large angle. While moving (or holding
        // input) we never trigger them - we blend and smoothly rotate instead.
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

        // Hard stop is polish only: a sudden release from a lope. Normal walking stops just blend
        // to Idle via Speed smoothing below.
        if (released && isGrounded && speedParam > hardStopSpeedThreshold)
        {
            StartCommitted(CommittedAction.Stop);
            return;
        }

        // Default turning: smoothly rotate toward camera-relative movement direction, heavier at
        // lope than at walk. Stays in Locomotion - never interrupts root-motion movement.
        if (hasInput)
        {
            Quaternion target = Quaternion.LookRotation(desiredDirection, Vector3.up);
            float rotSpeed = Mathf.Lerp(walkRotationSpeed, lopeRotationSpeed, Mathf.Clamp01(speedParam));
            transform.rotation = Quaternion.Slerp(transform.rotation, target, rotSpeed * Time.deltaTime);
        }

        // Drive the root-motion locomotion blend tree: 0 = Idle, 0.45 = Walk, 1.0 = Lope.
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
                currentRootScale = forwardJumpRootScale;
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

    // Rotate the Player to face the movement direction captured at the start of the committed
    // turn, over the given duration. Lands exactly on target (no overshoot / ping-pong); the turn
    // CLIP chosen by the angle bucket plays as the visual. Turn clips measured 0 deg root rotation,
    // so the rotation MUST be scripted.
    private void SetupScriptedTurn(float signedAngle, float duration)
    {
        isTurning = true;
        turnTargetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
        turnDegPerSecond = Mathf.Max(1f, Mathf.Abs(signedAngle)) / Mathf.Max(0.1f, duration);
    }

    private void UpdateCommittedAction()
    {
        // Committed actions ignore all WASD / steering input - only camera (a separate script)
        // and the scripted turn rotation run.
        if (isTurning)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, turnTargetRotation, turnDegPerSecond * Time.deltaTime);
        }

        committedFallbackTimer += Time.deltaTime;

        bool ended = false;

        if (animator != null)
        {
            // GetCurrentAnimatorStateInfo returns the SOURCE state during a transition, so while
            // the committed clip plays (and even as it transitions back) the current state stays
            // the committed state with a climbing normalizedTime. We only end once it has actually
            // been entered AND reached >= 0.90 (or we have somehow left it afterwards).
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);

            if (current.shortNameHash == committedStateHash)
            {
                committedStateEntered = true;
                if (current.normalizedTime >= 0.90f)
                {
                    ended = true;
                }
            }
            else if (committedStateEntered)
            {
                ended = true;
            }
        }

        // Documented fallback: if the expected state can't be detected, end after a generous
        // timeout so committedAction never sticks forever.
        if (!ended && committedFallbackTimer > 3f)
        {
            ended = true;
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

        if (ending == CommittedAction.Stop)
        {
            // Stop always ends in idle and does not rebuild speed from still-held input.
            speedParam = 0f;
            requireInputRelease = true;
        }
        // Turns / hops: speedParam rebuilds from current held input (incl. Shift) next frame
        // in UpdateLocomotion.
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
