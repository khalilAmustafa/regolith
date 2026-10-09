using UnityEngine;

public class RegolithResource : MonoBehaviour
{
    [SerializeField] private int amount = 1;
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Color highlightColor = Color.blue;
    [SerializeField] private bool deactivateOnCollect = true;

    private MaterialPropertyBlock[] originalBlocks;
    private MaterialPropertyBlock highlightBlock;
    private bool initialized;
    private bool collected;
    private bool highlighted;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public int Amount => amount;
    public bool IsCollected => collected;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (initialized && highlighted)
        {
            ApplyHighlight(true);
        }
    }

    private void OnDisable()
    {
        if (initialized)
        {
            ApplyHighlight(false);
        }
    }

    private void OnValidate()
    {
        amount = Mathf.Max(1, amount);
    }

    public void Highlight(bool enabled)
    {
        Initialize();

        bool shouldHighlight = enabled && !collected && isActiveAndEnabled && gameObject.activeInHierarchy;
        if (highlighted == shouldHighlight)
        {
            return;
        }

        highlighted = shouldHighlight;
        ApplyHighlight(shouldHighlight);
    }

    public void Collect()
    {
        if (collected)
        {
            return;
        }

        collected = true;
        Highlight(false);
        Debug.Log($"RegolithResource: collected '{name}' for {amount} regolith.", this);

        if (deactivateOnCollect)
        {
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Initialize()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        int rendererCount = renderers != null ? renderers.Length : 0;
        originalBlocks = new MaterialPropertyBlock[rendererCount];

        for (int i = 0; i < rendererCount; i++)
        {
            Renderer targetRenderer = renderers[i];
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

    public void AssignRenderers(Renderer[] resourceRenderers)
    {
        renderers = resourceRenderers;
        initialized = false;
        Initialize();
    }

    private void ApplyHighlight(bool enabled)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            if (!enabled)
            {
                if (originalBlocks != null && i < originalBlocks.Length && originalBlocks[i] != null)
                {
                    targetRenderer.SetPropertyBlock(originalBlocks[i]);
                }
                else
                {
                    targetRenderer.SetPropertyBlock(null);
                }

                continue;
            }

            highlightBlock.Clear();
            highlightBlock.SetColor(BaseColorId, highlightColor);
            highlightBlock.SetColor(ColorId, highlightColor);
            highlightBlock.SetColor(EmissionColorId, highlightColor * 1.2f);
            targetRenderer.SetPropertyBlock(highlightBlock);
        }
    }
}
