using UnityEngine;

public class RegolithCollectable : MonoBehaviour
{
    public int amount = 1;
    public bool collected = false;
    public bool deactivateOnCollect = true;
    public Renderer[] renderers;
    public Collider triggerCollider;
    public GameObject visualRoot;
    public bool isHighlighted = false;

    public Color highlightEmissionColor = new Color(0.55f, 0.85f, 1f, 1f);
    public float highlightEmissionIntensity = 0.65f;
    public GameObject highlightObject;
    public GameObject meshOutlineObject;
    public Color normalTint = Color.white;
    public Color highlightedTint = new Color(0.49f, 0.73f, 1f, 1f);
    public bool useBlueTintHighlight = true;
    public Renderer[] cachedRenderers;
    public bool useEmissionHighlight = true;
    public bool useScalePulse = true;
    public float pulseAmount = 0.025f;
    public float pulseSpeed = 3f;
    public float highlightSizeMultiplier = 1.10f;
    public float highlightThickness = 0.02f;
    public Vector3 highlightLocalOffset = new Vector3(0f, 0.01f, 0f);
    public Vector3 highlightOffset = new Vector3(0f, 0.04f, 0f);

    private Vector3 originalScale;
    private Vector3 originalHighlightScale;
    private MaterialPropertyBlock propertyBlock;
    private bool initialized;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        Initialize();
    }

    private void OnValidate()
    {
        amount = Mathf.Max(1, amount);
        pulseAmount = Mathf.Max(0f, pulseAmount);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
        highlightSizeMultiplier = Mathf.Max(1f, highlightSizeMultiplier);
        highlightThickness = Mathf.Max(0.001f, highlightThickness);
    }

    private void Update()
    {
        if (!initialized)
        {
            Initialize();
        }

        // Highlight is now material tint only. No runtime visual object pulse.
    }

    public void Initialize()
    {
        if (visualRoot == null)
        {
            visualRoot = gameObject;
        }

        originalScale = visualRoot.transform.localScale;

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetHighlightableRenderers();
        }

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = renderers;
        }

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (highlightObject != null)
        {
            highlightObject.SetActive(false);
        }

        if (meshOutlineObject != null)
        {
            meshOutlineObject.SetActive(false);
        }

        propertyBlock ??= new MaterialPropertyBlock();
        initialized = true;
    }

    public bool CanCollect(PlayerSurvivalResources resources)
    {
        return !collected
            && resources != null
            && resources.CanAddRegolith(amount);
    }

    public bool Collect(PlayerSurvivalResources resources)
    {
        if (collected || resources == null)
        {
            return false;
        }

        if (!resources.AddRegolith(amount))
        {
            return false;
        }

        collected = true;
        SetHighlighted(false);
        Debug.Log("Collected regolith +" + amount);

        if (deactivateOnCollect)
        {
            gameObject.SetActive(false);
        }

        return true;
    }

    public void SetHighlighted(bool highlighted)
    {
        Initialize();

        bool shouldHighlight = highlighted && !collected;
        isHighlighted = shouldHighlight;

        if (highlightObject != null)
        {
            highlightObject.SetActive(false);
        }

        if (meshOutlineObject != null)
        {
            meshOutlineObject.SetActive(false);
        }

        if (visualRoot != null)
        {
            visualRoot.transform.localScale = originalScale;
        }

        ApplyEmissionHighlight(shouldHighlight);
    }

    private void ApplyEmissionHighlight(bool highlighted)
    {
        Renderer[] targets = cachedRenderers != null && cachedRenderers.Length > 0 ? cachedRenderers : renderers;
        if (!useBlueTintHighlight || targets == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in targets)
        {
            if (targetRenderer == null)
            {
                continue;
            }

            if (!highlighted)
            {
                targetRenderer.SetPropertyBlock(null);
                continue;
            }

            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, highlightedTint);
            propertyBlock.SetColor(ColorId, highlightedTint);

            if (useEmissionHighlight)
            {
                Color emission = highlightedTint * Mathf.Max(0f, highlightEmissionIntensity);
                propertyBlock.SetColor(EmissionColorId, emission);
            }

            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private Renderer[] GetHighlightableRenderers()
    {
        Renderer[] allRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        if (allRenderers == null || allRenderers.Length == 0)
        {
            return allRenderers;
        }

        System.Collections.Generic.List<Renderer> filtered = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer candidate in allRenderers)
        {
            if (candidate == null)
            {
                continue;
            }

            if (highlightObject != null && candidate.transform.IsChildOf(highlightObject.transform))
            {
                continue;
            }

            if (meshOutlineObject != null && candidate.transform.IsChildOf(meshOutlineObject.transform))
            {
                continue;
            }

            string lowerName = candidate.gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("highlight") || lowerName.Contains("collect_ring") || lowerName.Contains("outline") || lowerName.Contains("selection"))
            {
                continue;
            }

            filtered.Add(candidate);
        }

        return filtered.ToArray();
    }
}
