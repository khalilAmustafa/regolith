using System.Collections;
using UnityEngine;

public class PlayerGatherAnimationController : MonoBehaviour
{
    public Animator animator;
    public MonoBehaviour playerControllerToDisable;
    public MonoBehaviour rootMotionRelayToDisable;
    public Rigidbody playerRigidbody;
    public string gatherTriggerName = "Gather";
    public string gatherStateName = "Gathering Objects";
    public float fallbackDuration = 1.5f;
    public bool useImportedGatherAnimation = true;
    public bool lockRootTransformDuringGather = true;

    private Transform playerRoot;
    private Vector3 lockedPosition;
    private Quaternion lockedRotation;
    private bool previousRootMotion;
    private bool controllerWasEnabled;
    private bool rootMotionRelayWasEnabled;
    private bool rigidbodyWasKinematic;
    private RigidbodyConstraints previousConstraints;
    private Vector3 previousVelocity;
    private Vector3 previousAngularVelocity;
    private bool isGathering;
    private static readonly string[] MovementTriggers =
    {
        "Hop",
        "ForwardJump",
        "TurnLeft",
        "TurnRight",
        "Turn180",
        "Stop"
    };

    private void Awake()
    {
        ResolveReferences();
    }

    public IEnumerator PlayGatherAnimation()
    {
        ResolveReferences();

        CaptureState();
        ApplyGatherLock();

        bool enteredGatherState = false;
        if (useImportedGatherAnimation && CanPlayGatherAnimation())
        {
            ResetMovementTriggers();
            animator.ResetTrigger(gatherTriggerName);
            animator.SetTrigger(gatherTriggerName);
            animator.Update(0f);
            yield return WaitForGatherState();
            enteredGatherState = IsCurrentOrNextGatherState();
        }
        else
        {
            if (useImportedGatherAnimation)
            {
                Debug.LogWarning("Gathering Objects animation is incompatible; using fallback gather delay.", this);
            }

            yield return WaitLocked(fallbackDuration);
        }

        if (enteredGatherState)
        {
            yield return WaitForGatherStateEndOrFallback();
        }

        RestoreState();
    }

    private void LateUpdate()
    {
        if (isGathering)
        {
            KeepRootLocked();
        }
    }

    private void FixedUpdate()
    {
        if (!isGathering || playerRigidbody == null)
        {
            return;
        }

        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
    }

    private IEnumerator WaitForGatherState()
    {
        float timer = 0f;
        while (timer < 0.5f)
        {
            KeepRootLocked();

            if (IsCurrentOrNextGatherState())
            {
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("Gathering Objects animation did not enter its state quickly; using fallback gather delay.", this);
        yield return WaitLocked(fallbackDuration);
    }

    private IEnumerator WaitForGatherStateEndOrFallback()
    {
        float timer = 0f;
        float maxDuration = Mathf.Max(0.1f, fallbackDuration + 0.5f);

        while (timer < maxDuration)
        {
            KeepRootLocked();

            if (animator != null)
            {
                AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
                if (IsGatherState(current) && current.normalizedTime >= 0.95f && !animator.IsInTransition(0))
                {
                    yield break;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitLocked(float duration)
    {
        float timer = 0f;
        float clampedDuration = Mathf.Max(0.05f, duration);
        while (timer < clampedDuration)
        {
            KeepRootLocked();
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private bool CanPlayGatherAnimation()
    {
        return animator != null
            && animator.runtimeAnimatorController != null
            && HasAnimatorTrigger(gatherTriggerName);
    }

    private bool IsGatherState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName(gatherStateName) || stateInfo.shortNameHash == Animator.StringToHash(gatherStateName);
    }

    private bool IsCurrentOrNextGatherState()
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
        return IsGatherState(current) || IsGatherState(next);
    }

    private bool HasAnimatorTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                return true;
            }
        }

        return false;
    }

    private void CaptureState()
    {
        playerRoot = playerControllerToDisable != null ? playerControllerToDisable.transform : transform;
        lockedPosition = playerRoot.position;
        lockedRotation = playerRoot.rotation;

        if (animator != null)
        {
            previousRootMotion = animator.applyRootMotion;
        }

        if (playerControllerToDisable != null)
        {
            controllerWasEnabled = playerControllerToDisable.enabled;
        }

        if (rootMotionRelayToDisable != null)
        {
            rootMotionRelayWasEnabled = rootMotionRelayToDisable.enabled;
        }

        if (playerRigidbody != null)
        {
            rigidbodyWasKinematic = playerRigidbody.isKinematic;
            previousConstraints = playerRigidbody.constraints;
            previousVelocity = playerRigidbody.linearVelocity;
            previousAngularVelocity = playerRigidbody.angularVelocity;
        }
    }

    private void ApplyGatherLock()
    {
        isGathering = true;

        if (playerControllerToDisable != null)
        {
            playerControllerToDisable.enabled = false;
        }

        if (rootMotionRelayToDisable != null)
        {
            rootMotionRelayToDisable.enabled = false;
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.SetFloat("Speed", 0f);
            animator.SetFloat("VerticalSpeed", 0f);
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.constraints = previousConstraints
                | RigidbodyConstraints.FreezePositionX
                | RigidbodyConstraints.FreezePositionZ
                | RigidbodyConstraints.FreezeRotation;
        }

        KeepRootLocked();
    }

    private void RestoreState()
    {
        KeepRootLocked();

        if (animator != null)
        {
            animator.applyRootMotion = previousRootMotion;
            animator.SetFloat("Speed", 0f);
            animator.SetFloat("VerticalSpeed", 0f);
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.constraints = previousConstraints;
            playerRigidbody.isKinematic = rigidbodyWasKinematic;
            playerRigidbody.linearVelocity = previousVelocity * 0f;
            playerRigidbody.angularVelocity = previousAngularVelocity * 0f;
        }

        if (playerControllerToDisable != null)
        {
            playerControllerToDisable.enabled = controllerWasEnabled;
        }

        if (rootMotionRelayToDisable != null)
        {
            rootMotionRelayToDisable.enabled = rootMotionRelayWasEnabled;
        }

        isGathering = false;
    }

    private void KeepRootLocked()
    {
        if (!lockRootTransformDuringGather || playerRoot == null)
        {
            return;
        }

        playerRoot.SetPositionAndRotation(lockedPosition, lockedRotation);
    }

    private void ResetMovementTriggers()
    {
        if (animator == null)
        {
            return;
        }

        foreach (string triggerName in MovementTriggers)
        {
            if (HasAnimatorTrigger(triggerName))
            {
                animator.ResetTrigger(triggerName);
            }
        }
    }

    private void ResolveReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (playerControllerToDisable == null)
        {
            playerControllerToDisable = GetComponent<ThirdPersonPlayerController>();
        }

        if (rootMotionRelayToDisable == null)
        {
            RootMotionRelay relay = GetComponentInChildren<RootMotionRelay>();
            rootMotionRelayToDisable = relay;
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody>();
        }
    }
}
