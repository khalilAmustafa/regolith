using UnityEngine;

public class PlayerAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private ThirdPersonPlayerController playerController;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponentInParent<ThirdPersonPlayerController>();
        }
    }

    public void OnJumpTakeoff()
    {
        if (playerController != null)
        {
            playerController.OnJumpTakeoff();
        }
    }
}
