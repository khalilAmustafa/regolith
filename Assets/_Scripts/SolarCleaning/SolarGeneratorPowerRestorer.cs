using UnityEngine;

public class SolarGeneratorPowerRestorer : MonoBehaviour
{
    [SerializeField] private float requiredCleanPercent = 50f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactRadius = 1.25f;
    [SerializeField] private Transform interactPoint;
    [SerializeField] private BasePowerSystem powerSystem;
    [SerializeField] private SolarPowerState solarPowerState;
    [SerializeField] private SolarPanelCleaningMinigame cleaningMinigame;
    [SerializeField] private string promptWhenLocked = "Clean at least 50% of solar panels first";
    [SerializeField] private string promptWhenReady = "Press E: Restore Power";
    [SerializeField] private string promptWhenOnline = "Power already ONLINE";
    [SerializeField] private Renderer[] highlightRenderers;
    [SerializeField] private Color highlightColor = Color.blue;

    private Transform player;
    private SimpleHUD simpleHud;
    private bool playerInRange;
    private bool highlighted;
    private MaterialPropertyBlock[] originalBlocks;
    private MaterialPropertyBlock highlightBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public float RequiredCleanPercent => requiredCleanPercent;
    public float InteractRadius => interactRadius;

    public void Configure(
        float newRequiredCleanPercent,
        KeyCode newInteractKey,
        float newInteractRadius,
        Transform newInteractPoint,
        BasePowerSystem newPowerSystem,
        SolarPowerState newSolarPowerState,
        SolarPanelCleaningMinigame newCleaningMinigame)
    {
        requiredCleanPercent = Mathf.Clamp(newRequiredCleanPercent, 0f, 100f);
        interactKey = newInteractKey;
        interactRadius = Mathf.Max(0.1f, newInteractRadius);
        interactPoint = newInteractPoint != null ? newInteractPoint : transform;
        powerSystem = newPowerSystem;
        solarPowerState = newSolarPowerState;
        cleaningMinigame = newCleaningMinigame;
        ResolveReferences();
    }

    public void AssignHighlightRenderers(Renderer[] renderers)
    {
        highlightRenderers = renderers;
        CacheOriginalMaterialBlocks();
    }

    private void Awake()
    {
        ResolveReferences();
        CacheOriginalMaterialBlocks();
    }

    private void OnValidate()
    {
        requiredCleanPercent = Mathf.Clamp(requiredCleanPercent, 0f, 100f);
        interactRadius = Mathf.Max(0.1f, interactRadius);
    }

    private void OnDisable()
    {
        Highlight(false);
        if (playerInRange)
        {
            SetPrompt(string.Empty);
        }
    }

    private void Update()
    {
        ResolveReferences();

        playerInRange = IsPlayerInRange();
        Highlight(playerInRange);

        if (!playerInRange)
        {
            SetPrompt(string.Empty);
            return;
        }

        string prompt = GetPromptText();
        SetPrompt(prompt);

        if (Input.GetKeyDown(interactKey))
        {
            TryRestorePower();
        }
    }

    public bool TryRestorePower()
    {
        ResolveReferences();

        if (powerSystem != null && powerSystem.IsPowerOnline)
        {
            ShowMessage(promptWhenOnline);
            Debug.Log(promptWhenOnline, this);
            return false;
        }

        float cleanPercent = GetCleanPercent();
        if (cleanPercent < requiredCleanPercent)
        {
            ShowMessage(promptWhenLocked);
            Debug.Log(promptWhenLocked + $" Current clean percent: {cleanPercent:0.#}%", this);
            return false;
        }

        if (powerSystem == null)
        {
            Debug.LogWarning("Solar generator cannot restore power because BasePowerSystem is missing.", this);
            return false;
        }

        powerSystem.SetPowerOnline(true);
        ShowMessage("Power restored");
        Debug.Log("Power restored from solar generator after solar panels reached 50% cleaning.", this);
        return true;
    }

    public float GetCleanPercent()
    {
        if (solarPowerState != null)
        {
            return solarPowerState.CleanPercentValue;
        }

        return 0f;
    }

    private string GetPromptText()
    {
        if (powerSystem != null && powerSystem.IsPowerOnline)
        {
            return promptWhenOnline;
        }

        return GetCleanPercent() >= requiredCleanPercent ? promptWhenReady : promptWhenLocked;
    }

    private bool IsPlayerInRange()
    {
        Transform origin = interactPoint != null ? interactPoint : transform;
        return player != null && Vector3.Distance(player.position, origin.position) <= interactRadius;
    }

    private void ResolveReferences()
    {
        if (interactPoint == null)
        {
            interactPoint = transform;
        }

        if (powerSystem == null)
        {
            powerSystem = FindAnyObjectByType<BasePowerSystem>();
        }

        if (solarPowerState == null)
        {
            solarPowerState = FindAnyObjectByType<SolarPowerState>();
        }

        if (cleaningMinigame == null)
        {
            cleaningMinigame = FindAnyObjectByType<SolarPanelCleaningMinigame>();
            if (cleaningMinigame != null && solarPowerState == null)
            {
                solarPowerState = cleaningMinigame.powerState;
            }
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (simpleHud == null)
        {
            simpleHud = FindAnyObjectByType<SimpleHUD>();
        }

        if (highlightRenderers == null || highlightRenderers.Length == 0)
        {
            highlightRenderers = GetComponentsInChildren<Renderer>(true);
            CacheOriginalMaterialBlocks();
        }
    }

    private void SetPrompt(string text)
    {
        if (simpleHud != null)
        {
            simpleHud.SetPrompt(text);
        }

        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.SetPrompt(text);
        }
    }

    private void ShowMessage(string text)
    {
        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.ShowMessage(text);
        }
    }

    private void CacheOriginalMaterialBlocks()
    {
        int count = highlightRenderers != null ? highlightRenderers.Length : 0;
        originalBlocks = new MaterialPropertyBlock[count];
        for (int i = 0; i < count; i++)
        {
            if (highlightRenderers[i] == null)
            {
                continue;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            highlightRenderers[i].GetPropertyBlock(block);
            originalBlocks[i] = block;
        }

        highlightBlock ??= new MaterialPropertyBlock();
    }

    private void Highlight(bool enabled)
    {
        if (highlighted == enabled)
        {
            return;
        }

        highlighted = enabled;
        if (highlightRenderers == null)
        {
            return;
        }

        for (int i = 0; i < highlightRenderers.Length; i++)
        {
            Renderer targetRenderer = highlightRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            if (!enabled)
            {
                targetRenderer.SetPropertyBlock(originalBlocks != null && i < originalBlocks.Length ? originalBlocks[i] : null);
                continue;
            }

            highlightBlock.Clear();
            highlightBlock.SetColor(BaseColorId, highlightColor);
            highlightBlock.SetColor(ColorId, highlightColor);
            highlightBlock.SetColor(EmissionColorId, highlightColor * 1.2f);
            targetRenderer.SetPropertyBlock(highlightBlock);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = interactPoint != null ? interactPoint : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin.position, interactRadius);
    }
}
