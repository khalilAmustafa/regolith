using UnityEngine;

// Lives on "Astronaut Visual" (the object that owns the Animator). It reads the Animator's
// per-frame root-motion delta and forwards it to the parent ThirdPersonPlayerController, which
// applies it through the CharacterController. It NEVER moves Astronaut Visual itself.
//
// Requires Animator "Apply Root Motion" = ON for the deltas to be non-zero. Until the Phase-2
// conversion enables ThirdPersonPlayerController.useRootMotion, ApplyRootMotion is a no-op, so
// attaching this is safe.
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class RootMotionRelay : MonoBehaviour
{
    [SerializeField] private ThirdPersonPlayerController playerController;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (playerController == null)
        {
            playerController = GetComponentInParent<ThirdPersonPlayerController>();
        }
    }

    private void OnAnimatorMove()
    {
        if (playerController == null || animator == null)
        {
            return;
        }

        // Forward only - do not apply anything to this transform (Astronaut Visual).
        playerController.ApplyRootMotion(animator.deltaPosition, animator.deltaRotation);
    }
}
