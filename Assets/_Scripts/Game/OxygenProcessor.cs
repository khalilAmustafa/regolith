using UnityEngine;

#pragma warning disable 0618
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

    public string GetPrompt(PlayerSurvivalResources player)
    {
        if (activated && !reusable)
        {
            return "Oxygen processor complete";
        }

        return $"Press E to process {requiredRegolith} regolith";
    }

    public bool CanInteract(PlayerSurvivalResources player)
    {
        return player != null && (reusable || !activated) && player.regolithAmount >= requiredRegolith;
    }

    public void Interact(PlayerSurvivalResources player)
    {
        if (activated && !reusable)
        {
            return;
        }

        if (player == null || !player.SpendRegolith(requiredRegolith))
        {
            return;
        }

        player.AddStoredOxygen(oxygenOutput);
        activated = true;
        SetActivatedVisual();

        GameManager gameManager = FindFirstObjectByType<GameManager>();
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
#pragma warning restore 0618
