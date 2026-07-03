using UnityEngine;

#pragma warning disable 0618
public class BaseEntrance : MonoBehaviour, IInteractable
{
    [SerializeField] private int requiredOxygen = 3;
    [SerializeField] private Transform doorTransform;
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 3f, 0f);
    [SerializeField] private float openSpeed = 2f;

    private bool opening;
    private bool opened;
    private Vector3 closedPosition;
    private Vector3 openPosition;

    private void Awake()
    {
        if (doorTransform == null)
        {
            doorTransform = transform;
        }

        closedPosition = doorTransform.position;
        openPosition = closedPosition + openOffset;
    }

    private void Update()
    {
        if (!opening || doorTransform == null)
        {
            return;
        }

        doorTransform.position = Vector3.MoveTowards(doorTransform.position, openPosition, openSpeed * Time.deltaTime);
        if (Vector3.Distance(doorTransform.position, openPosition) <= 0.01f)
        {
            opening = false;
            opened = true;
        }
    }

    public string GetPrompt(PlayerSurvivalResources player)
    {
        return opened ? "Base entrance open" : $"Press E to open base ({requiredOxygen} oxygen)";
    }

    public bool CanInteract(PlayerSurvivalResources player)
    {
        return player != null && !opened && !opening && player.storedOxygen >= requiredOxygen;
    }

    public void Interact(PlayerSurvivalResources player)
    {
        if (opened || opening)
        {
            return;
        }

        if (player == null || !player.SpendStoredOxygen(requiredOxygen))
        {
            return;
        }

        opening = true;

        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnBaseOpened();
        }
    }

    public void SetDoor(Transform door)
    {
        doorTransform = door;
        if (doorTransform != null)
        {
            closedPosition = doorTransform.position;
            openPosition = closedPosition + openOffset;
        }
    }
}
#pragma warning restore 0618
