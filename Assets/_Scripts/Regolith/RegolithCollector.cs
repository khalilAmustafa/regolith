using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerInventory))]
public class RegolithCollector : MonoBehaviour
{
    [SerializeField] private float collectRadius = 2f;
    [SerializeField] private KeyCode collectKey = KeyCode.E;
    [SerializeField] private LayerMask regolithLayer = ~0;
    [SerializeField] private Transform detectionOrigin;
    [SerializeField] private int overlapCapacity = 64;
    [SerializeField] private bool collectAfterGatherAnimation = true;
    [SerializeField] private PlayerGatherAnimationController gatherAnimationController;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private string gatherTriggerName = "Gather";
    [SerializeField] private string gatherStateName = "Gathering Objects";
    [SerializeField] private float fallbackGatherDuration = 1.2f;
    [SerializeField] private bool useImportedGatherAnimation = true;
    [SerializeField] private AudioSource pickupAudioSource;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private GameObject pickupVfxPrefab;

    private Collider[] overlapResults;
    private RegolithResource highlightedResource;
    private PlayerInventory inventory;
    private PlayerSurvivalResources survivalResources;
    private GameManager gameManager;
    private bool isGathering;

    private void Awake()
    {
        overlapResults = new Collider[Mathf.Max(8, overlapCapacity)];
        inventory = GetComponent<PlayerInventory>();
        survivalResources = GetComponent<PlayerSurvivalResources>();
        gameManager = FindAnyObjectByType<GameManager>();
        ResolveGatherAnimationReferences();
        if (pickupAudioSource == null)
        {
            pickupAudioSource = GetComponent<AudioSource>();
        }
    }

    private void OnValidate()
    {
        collectRadius = Mathf.Max(0.1f, collectRadius);
        overlapCapacity = Mathf.Clamp(overlapCapacity, 8, 256);
        fallbackGatherDuration = Mathf.Max(0.05f, fallbackGatherDuration);
    }

    public void Configure(float radius, KeyCode key, LayerMask layer, Transform origin = null)
    {
        collectRadius = Mathf.Max(0.1f, radius);
        collectKey = key;
        regolithLayer = layer;

        if (origin != null)
        {
            detectionOrigin = origin;
        }

        if (overlapResults == null || overlapResults.Length != Mathf.Max(8, overlapCapacity))
        {
            overlapResults = new Collider[Mathf.Max(8, overlapCapacity)];
        }
    }

    public void ConfigureGatherAnimation(
        PlayerGatherAnimationController controller,
        Animator animator,
        string triggerName = "Gather",
        string stateName = "Gathering Objects",
        float fallbackDuration = 1.2f,
        bool useImportedAnimation = true,
        bool waitForAnimation = true)
    {
        gatherAnimationController = controller;
        playerAnimator = animator;
        gatherTriggerName = string.IsNullOrWhiteSpace(triggerName) ? "Gather" : triggerName;
        gatherStateName = string.IsNullOrWhiteSpace(stateName) ? "Gathering Objects" : stateName;
        fallbackGatherDuration = Mathf.Max(0.05f, fallbackDuration);
        useImportedGatherAnimation = useImportedAnimation;
        collectAfterGatherAnimation = waitForAnimation;
        ApplyGatherAnimationSettings();
    }

    private void OnDisable()
    {
        SetHighlightedResource(null);
    }

    private void Update()
    {
        if (isGathering)
        {
            return;
        }

        RegolithResource nearest = FindNearestResource();
        SetHighlightedResource(nearest);

        if (highlightedResource != null && Input.GetKeyDown(collectKey))
        {
            CollectHighlightedResource();
        }
    }

    private RegolithResource FindNearestResource()
    {
        Transform originTransform = detectionOrigin != null ? detectionOrigin : transform;
        Vector3 origin = originTransform.position;

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            collectRadius,
            overlapResults,
            regolithLayer,
            QueryTriggerInteraction.Collide);

        RegolithResource nearest = null;
        float nearestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];
            overlapResults[i] = null;

            if (hit == null || !hit.enabled || !hit.gameObject.activeInHierarchy)
            {
                continue;
            }

            RegolithResource resource = hit.GetComponentInParent<RegolithResource>();
            if (resource == null || resource.IsCollected || !resource.isActiveAndEnabled || !resource.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 closestPoint = hit.ClosestPoint(origin);
            float distanceSqr = (closestPoint - origin).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearest = resource;
            }
        }

        return nearest;
    }

    private void SetHighlightedResource(RegolithResource resource)
    {
        if (highlightedResource == resource)
        {
            return;
        }

        if (highlightedResource != null)
        {
            highlightedResource.Highlight(false);
        }

        highlightedResource = resource;

        if (highlightedResource != null)
        {
            highlightedResource.Highlight(true);
        }
    }

    private void CollectHighlightedResource()
    {
        RegolithResource resource = highlightedResource;
        if (resource == null)
        {
            return;
        }

        int amount = resource.Amount;
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }

        if (survivalResources == null)
        {
            survivalResources = GetComponent<PlayerSurvivalResources>();
        }

        if (!CanStoreRegolith(amount))
        {
            ShowInventoryFull();
            return;
        }

        if (collectAfterGatherAnimation)
        {
            StartCoroutine(CollectAfterGatherAnimation(resource));
            return;
        }

        CompleteCollection(resource);
    }

    private IEnumerator CollectAfterGatherAnimation(RegolithResource resource)
    {
        isGathering = true;

        if (gatherAnimationController == null)
        {
            ResolveGatherAnimationReferences();
        }

        if (gatherAnimationController != null)
        {
            ApplyGatherAnimationSettings();
            yield return gatherAnimationController.PlayGatherAnimation();
        }
        else
        {
            Debug.LogWarning("RegolithCollector: PlayerGatherAnimationController is missing. Using fallback gather delay.", this);
            yield return new WaitForSeconds(Mathf.Max(0.05f, fallbackGatherDuration));
        }

        if (resource != null && resource == highlightedResource && !resource.IsCollected && resource.isActiveAndEnabled && resource.gameObject.activeInHierarchy)
        {
            if (CanStoreRegolith(resource.Amount))
            {
                CompleteCollection(resource);
            }
            else
            {
                ShowInventoryFull();
            }
        }

        isGathering = false;
    }

    private void CompleteCollection(RegolithResource resource)
    {
        if (resource == null)
        {
            return;
        }

        int amount = resource.Amount;

        if (survivalResources != null)
        {
            if (!survivalResources.AddRegolith(amount))
            {
                ShowInventoryFull();
                return;
            }

            if (inventory != null)
            {
                inventory.SetRegolith(survivalResources.GetRegolith(), survivalResources.GetMaxRegolith());
            }
        }
        else
        {
            if (inventory == null)
            {
                Debug.LogWarning("RegolithCollector: cannot collect because PlayerInventory is missing.", this);
                return;
            }

            if (!inventory.TryAddRegolith(amount))
            {
                ShowInventoryFull();
                return;
            }
        }

        Vector3 pickupPosition = resource.transform.position;
        resource.Collect();
        SetHighlightedResource(null);
        PlayPickupFeedback(pickupPosition);
        Debug.Log($"RegolithCollector: collected {amount} regolith from '{resource.name}'. Player inventory: {inventory.Regolith}", this);

        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        if (gameManager != null)
        {
            gameManager.OnRegolithPickedUp();
        }
    }

    private void ResolveGatherAnimationReferences()
    {
        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
        }

        if (gatherAnimationController == null)
        {
            gatherAnimationController = GetComponent<PlayerGatherAnimationController>();
        }

        if (gatherAnimationController == null && Application.isPlaying)
        {
            gatherAnimationController = gameObject.AddComponent<PlayerGatherAnimationController>();
        }

        ApplyGatherAnimationSettings();
    }

    private void ApplyGatherAnimationSettings()
    {
        if (gatherAnimationController == null)
        {
            return;
        }

        gatherAnimationController.animator = playerAnimator != null ? playerAnimator : gatherAnimationController.animator;
        gatherAnimationController.playerControllerToDisable ??= GetComponent<ThirdPersonPlayerController>();
        gatherAnimationController.rootMotionRelayToDisable ??= GetComponentInChildren<RootMotionRelay>();
        gatherAnimationController.playerRigidbody ??= GetComponent<Rigidbody>();
        gatherAnimationController.gatherTriggerName = gatherTriggerName;
        gatherAnimationController.gatherStateName = gatherStateName;
        gatherAnimationController.fallbackDuration = fallbackGatherDuration;
        gatherAnimationController.useImportedGatherAnimation = useImportedGatherAnimation;
        gatherAnimationController.lockRootTransformDuringGather = true;
    }

    private bool CanStoreRegolith(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (survivalResources != null)
        {
            return survivalResources.CanAddRegolith(amount);
        }

        return inventory != null && inventory.CanAddRegolith(amount);
    }

    private void ShowInventoryFull()
    {
        Debug.Log("RegolithCollector: Regolith inventory full. Collection blocked.", this);

        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.ShowMessage("Regolith inventory full");
        }
    }

    private void PlayPickupFeedback(Vector3 position)
    {
        if (pickupSound != null)
        {
            if (pickupAudioSource != null)
            {
                pickupAudioSource.PlayOneShot(pickupSound);
            }
            else
            {
                AudioSource.PlayClipAtPoint(pickupSound, position);
            }
        }

        if (pickupVfxPrefab != null)
        {
            Instantiate(pickupVfxPrefab, position, Quaternion.identity);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform originTransform = detectionOrigin != null ? detectionOrigin : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(originTransform.position, collectRadius);
    }
}
