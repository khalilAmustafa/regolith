using UnityEngine;
using UnityEngine.UI;

#pragma warning disable 0618
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private Text promptText;

    private SimpleHUD hud;
    private IInteractable currentInteractable;
    private PlayerSurvivalResources playerResources;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        hud = FindFirstObjectByType<SimpleHUD>();
        playerResources = GetComponent<PlayerSurvivalResources>();
    }

    private void Update()
    {
        DetectInteractable();

        if (currentInteractable != null && Input.GetKeyDown(KeyCode.E))
        {
            currentInteractable.Interact(playerResources);
        }
    }

    private void DetectInteractable()
    {
        currentInteractable = null;

        Transform rayOrigin = cameraTransform != null ? cameraTransform : transform;
        Vector3 origin = rayOrigin.position;
        Vector3 direction = cameraTransform != null ? rayOrigin.forward : transform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, interactDistance))
        {
            currentInteractable = hit.collider.GetComponentInParent<IInteractable>();
        }

        string prompt = currentInteractable != null ? currentInteractable.GetPrompt(playerResources) : string.Empty;
        SetPrompt(prompt);
    }

    private void SetPrompt(string prompt)
    {
        if (promptText != null)
        {
            promptText.text = prompt;
        }

        if (hud != null)
        {
            hud.SetPrompt(prompt);
        }
    }

    public void SetCamera(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }

    public void SetPromptText(Text newPromptText)
    {
        promptText = newPromptText;
    }
}
#pragma warning restore 0618
