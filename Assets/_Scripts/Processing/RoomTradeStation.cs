using System.Collections.Generic;
using UnityEngine;

public enum StationType
{
    OxygenProcessor,
    ConcreteProcessor,
    RegolithPanel,
    PowerStation
}

public class RoomTradeStation : MonoBehaviour
{
    [SerializeField] private string stationName = "Trade Station";
    [SerializeField] private StationType stationType = StationType.OxygenProcessor;
    [SerializeField] private int requiredRegolith = 3;
    [SerializeField] private int outputAmount = 10;
    [SerializeField] private bool requiresPower;
    [SerializeField] private Renderer[] highlightRenderers;
    [SerializeField] private Color highlightColor = Color.blue;
    [SerializeField] private Transform promptPoint;

    private MaterialPropertyBlock[] originalBlocks;
    private MaterialPropertyBlock highlightBlock;
    private bool initialized;
    private bool highlighted;
    private HashSet<string> loggedSetupWarnings = new HashSet<string>();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public string StationName => stationName;
    public StationType Type => stationType;
    public int RequiredRegolith => requiredRegolith;
    public int OutputAmount => outputAmount;
    public bool RequiresPower => requiresPower;
    public Transform PromptPoint => promptPoint;

    private void Awake()
    {
        NormalizeLegacyStationRole();
        Initialize();
    }

    private void Reset()
    {
        AutoCacheHighlightRenderers();
    }

    private void OnDisable()
    {
        Highlight(false);
    }

    private void OnValidate()
    {
        NormalizeLegacyStationRole();
        requiredRegolith = Mathf.Max(0, requiredRegolith);
        outputAmount = Mathf.Max(0, outputAmount);
        AutoCacheHighlightRenderers();
    }

    public string GetTradeText()
    {
        NormalizeLegacyStationRole();

        switch (stationType)
        {
            case StationType.OxygenProcessor:
                return $"{requiredRegolith} Regolith -> {outputAmount} O2";
            case StationType.RegolithPanel:
                return $"Regolith Panel - {requiredRegolith} Regolith -> {outputAmount} Concrete";
            case StationType.ConcreteProcessor:
                return $"{requiredRegolith} Regolith -> {outputAmount} Concrete";
            case StationType.PowerStation:
                return $"Regolith Panel - {requiredRegolith} Regolith -> {outputAmount} Concrete";
            default:
                return string.Empty;
        }
    }

    public bool CanTrade(PlayerInventory inventory)
    {
        return CanTrade(inventory, FindAnyObjectByType<BasePowerSystem>());
    }

    public bool CanTrade(PlayerInventory inventory, BasePowerSystem powerSystem)
    {
        NormalizeLegacyStationRole();

        if (inventory == null)
        {
            return false;
        }

        if (requiresPower && powerSystem != null && !powerSystem.IsPowerOnline)
        {
            return false;
        }

        return inventory.Regolith >= requiredRegolith;
    }

    public bool TryTrade(PlayerInventory inventory)
    {
        return TryTrade(inventory, FindAnyObjectByType<BasePowerSystem>());
    }

    public bool TryTrade(PlayerInventory inventory, BasePowerSystem powerSystem)
    {
        NormalizeLegacyStationRole();

        if (inventory == null)
        {
            Debug.LogWarning($"Missing PlayerInventory for {stationName}", this);
            return false;
        }

        if (requiresPower && powerSystem != null && !powerSystem.IsPowerOnline)
        {
            Debug.Log($"Power required for {stationName}", this);
            return false;
        }

        if (inventory.Regolith < requiredRegolith)
        {
            Debug.Log("Not enough Regolith", this);
            return false;
        }

        if (!inventory.TrySpendRegolith(requiredRegolith))
        {
            Debug.Log("Not enough Regolith", this);
            return false;
        }

        if (stationType == StationType.OxygenProcessor)
        {
            inventory.AddStoredOxygen(outputAmount);
            Debug.Log($"{stationName}: traded {requiredRegolith} Regolith for {outputAmount} Stored O2", this);
            return true;
        }

        inventory.AddConcrete(outputAmount);
        Debug.Log($"{stationName}: traded {requiredRegolith} Regolith for {outputAmount} Concrete", this);
        return true;
    }

    public void Highlight(bool enabled)
    {
        Initialize();

        bool shouldHighlight = enabled && isActiveAndEnabled && gameObject.activeInHierarchy;
        if (highlighted == shouldHighlight)
        {
            return;
        }

        highlighted = shouldHighlight;
        Debug.Log(
            $"RoomTradeStation.Highlight({shouldHighlight}) on {GetHierarchyPath(transform)} " +
            $"station='{stationName}' type={stationType}",
            this);
        // Rooms keep their normal color; always restore the original material state
        // so any previously applied highlight tint is cleared.
        ApplyHighlight(false);
    }

    public void AssignHighlightRenderers(Renderer[] renderers)
    {
        highlightRenderers = FilterValidRenderers(renderers, "AssignHighlightRenderers");
        initialized = false;
        Initialize();
    }

    public void Configure(
        string newStationName,
        StationType newStationType,
        int newRequiredRegolith,
        int newOutputAmount,
        bool newRequiresPower)
    {
        stationName = string.IsNullOrWhiteSpace(newStationName) ? stationName : newStationName;
        stationType = newStationType;
        requiredRegolith = Mathf.Max(0, newRequiredRegolith);
        outputAmount = Mathf.Max(0, newOutputAmount);
        requiresPower = newRequiresPower;
        NormalizeLegacyStationRole();
    }

    public void SetPromptPoint(Transform point)
    {
        promptPoint = point;
    }

    private void Initialize()
    {
        NormalizeLegacyStationRole();

        if (initialized)
        {
            if (highlightBlock == null)
            {
                LogHighlightSetupWarning("highlightBlock", highlighted);
                highlightBlock = new MaterialPropertyBlock();
            }

            return;
        }

        AutoCacheHighlightRenderers();

        int rendererCount = highlightRenderers != null ? highlightRenderers.Length : 0;
        originalBlocks = new MaterialPropertyBlock[rendererCount];
        for (int i = 0; i < rendererCount; i++)
        {
            Renderer targetRenderer = highlightRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            MaterialPropertyBlock originalBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(originalBlock);
            originalBlocks[i] = originalBlock;
        }

        highlightBlock ??= new MaterialPropertyBlock();
        initialized = true;
    }

    private void ApplyHighlight(bool enabled)
    {
        NormalizeLegacyStationRole();

        if (highlightRenderers == null || highlightRenderers.Length == 0)
        {
            LogHighlightSetupWarning("highlightRenderers", enabled);
            return;
        }

        if (highlightBlock == null)
        {
            LogHighlightSetupWarning("highlightBlock", enabled);
            highlightBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < highlightRenderers.Length; i++)
        {
            Renderer targetRenderer = highlightRenderers[i];
            if (targetRenderer == null)
            {
                LogHighlightSetupWarning($"highlightRenderers[{i}]", enabled);
                continue;
            }

            if (!enabled)
            {
                MaterialPropertyBlock originalBlock = originalBlocks != null && i < originalBlocks.Length
                    ? originalBlocks[i]
                    : null;
                targetRenderer.SetPropertyBlock(originalBlock);
                continue;
            }

            highlightBlock.Clear();
            highlightBlock.SetColor(BaseColorId, highlightColor);
            highlightBlock.SetColor(ColorId, highlightColor);
            highlightBlock.SetColor(EmissionColorId, highlightColor * 1.2f);
            targetRenderer.SetPropertyBlock(highlightBlock);
        }
    }

    private void AutoCacheHighlightRenderers()
    {
        highlightRenderers = FilterValidRenderers(highlightRenderers, "highlightRenderers");
        if (highlightRenderers == null || highlightRenderers.Length == 0)
        {
            highlightRenderers = FilterValidRenderers(GetComponentsInChildren<Renderer>(true), "GetComponentsInChildren<Renderer>");
        }
    }

    private Renderer[] FilterValidRenderers(Renderer[] renderers, string fieldName)
    {
        if (renderers == null || renderers.Length == 0)
        {
            return renderers;
        }

        List<Renderer> validRenderers = new List<Renderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                LogHighlightSetupWarning($"{fieldName}[{i}]", highlighted);
                continue;
            }

            validRenderers.Add(renderer);
        }

        return validRenderers.ToArray();
    }

    private void LogHighlightSetupWarning(string fieldName, bool enabled)
    {
        loggedSetupWarnings ??= new HashSet<string>();

        string warningKey = fieldName + ":" + enabled;
        if (!loggedSetupWarnings.Add(warningKey))
        {
            return;
        }

        Debug.LogWarning(
            $"RoomTradeStation highlight setup incomplete on {GetHierarchyPath(transform)}: " +
            $"{fieldName} is null or missing. highlightEnabled={enabled}, station='{stationName}', type={stationType}",
            this);
    }

    private void NormalizeLegacyStationRole()
    {
        bool isLegacyPowerRoom = stationType == StationType.PowerStation
            || string.Equals(stationName, "Power Station", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(stationName, "Power Room", System.StringComparison.OrdinalIgnoreCase);
        bool isRegolithPanelWithWrongRecipe = stationType == StationType.RegolithPanel
            && string.Equals(stationName, "Regolith Panel", System.StringComparison.OrdinalIgnoreCase)
            && (requiredRegolith != 5 || outputAmount != 1 || requiresPower);

        if (!isLegacyPowerRoom && !isRegolithPanelWithWrongRecipe)
        {
            return;
        }

        stationName = "Regolith Panel";
        stationType = StationType.RegolithPanel;
        requiredRegolith = 5;
        outputAmount = 1;
        requiresPower = false;
    }

    public static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "<null transform>";
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
