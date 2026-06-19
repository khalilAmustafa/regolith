using UnityEngine;

public class OxygenProcessor : MonoBehaviour, IInteractable
{
    [SerializeField] private int requiredRegolith = 5;
    [SerializeField] private int oxygenOutput = 3;
    [SerializeField] private bool reusable;

    private bool activated;
    private Renderer cachedRenderer;

    private void Awake()
    {
        cachedRenderer = GetComponentInChildren<Renderer>();
    }

    public string GetPrompt()
    {
        if (activated && !reusable)
        {
            return "Oxygen processor complete";
        }

        return $"Press E to process {requiredRegolith} regolith";
    }

    public void Interact(GameObject player)
    {
        if (activated && !reusable)
        {
            return;
        }

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null || !inventory.SpendRegolith(requiredRegolith))
        {
            return;
        }

        inventory.AddOxygen(oxygenOutput);
        activated = true;
        SetActivatedVisual();

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnOxygenProcessed();
        }
    }

    private void SetActivatedVisual()
    {
        if (cachedRenderer == null)
        {
            return;
        }

        Material material = cachedRenderer.material;
        Color activeColor = new Color(0.2f, 0.8f, 1f);
        material.color = activeColor;

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", activeColor * 1.5f);
        }
    }
}
