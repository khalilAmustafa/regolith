using UnityEngine;
using UnityEngine.UI;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Text promptText;

    private SimpleHUD hud;
    private IInteractable currentInteractable;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        hud = FindObjectOfType<SimpleHUD>();
    }

    private void Update()
    {
        DetectInteractable();

        if (currentInteractable != null && Input.GetKeyDown(KeyCode.E))
        {
            currentInteractable.Interact(gameObject);
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

        string prompt = currentInteractable != null ? currentInteractable.GetPrompt() : string.Empty;
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
