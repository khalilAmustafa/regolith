using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;

public class RegolithCollectionManager : MonoBehaviour
{
    public Transform player;
    public PlayerSurvivalResources resources;
    public Transform regolithParent;
    public float detectionRadius = 2.5f;
    public KeyCode interactKey = KeyCode.E;
    public GameObject promptUI;
    public TMP_Text promptText;
    public string promptMessage = "Press E\nCollect regolith";
    public bool debugLogs = true;
    public Animator playerAnimator;
    public PlayerGatherAnimationController gatherAnimationController;
    public AnimationClip gatheringAnimationClip;
    public string gatherTriggerName = "Gather";
    public float fallbackGatherDuration = 1.2f;
    public bool collectAfterGatherAnimation = true;
    public bool useImportedGatherAnimation = true;

    private readonly List<RegolithCollectable> collectables = new List<RegolithCollectable>();
    private RegolithCollectable currentNearest;
    private bool isGathering;

    private void Start()
    {
        ResolveReferences();
        RefreshCollectables();
        SetPromptVisible(false);
    }

    private void OnDisable()
    {
        if (currentNearest != null)
        {
            currentNearest.SetHighlighted(false);
            currentNearest = null;
        }

        SetPromptVisible(false);
    }

    private void Update()
    {
        ResolveReferences();

        if (isGathering)
        {
            return;
        }

        RegolithCollectable nearest = FindNearestCollectable();
        if (nearest != currentNearest)
        {
            if (currentNearest != null)
            {
                currentNearest.SetHighlighted(false);
            }

            currentNearest = nearest;

            if (currentNearest != null)
            {
                currentNearest.SetHighlighted(true);
            }
        }

        SetPromptVisible(currentNearest != null);

        if (!isGathering && currentNearest != null && Input.GetKeyDown(interactKey))
        {
            TryCollectCurrent();
        }
    }

    public void RefreshCollectables()
    {
        collectables.Clear();

        if (regolithParent == null)
        {
            GameObject parentObject = GameObject.Find("Regolith_Road");
            regolithParent = parentObject != null ? parentObject.transform : null;
        }

        if (regolithParent == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning("RegolithCollectionManager: Regolith_Road parent is missing.", this);
            }

            return;
        }

        collectables.AddRange(regolithParent.GetComponentsInChildren<RegolithCollectable>(true));
    }

    private void TryCollectCurrent()
    {
        if (currentNearest == null)
        {
            return;
        }

        if (!currentNearest.CanCollect(resources))
        {
            ShowInventoryFull();
            return;
        }

        RegolithCollectable collectedTarget = currentNearest;
        if (collectAfterGatherAnimation)
        {
            StartCoroutine(CollectAfterGatherAnimation(collectedTarget));
            return;
        }

        CompleteCollection(collectedTarget);
    }

    private IEnumerator CollectAfterGatherAnimation(RegolithCollectable target)
    {
        isGathering = true;
        SetPromptVisible(false);

        if (gatherAnimationController != null)
        {
            gatherAnimationController.animator = playerAnimator != null ? playerAnimator : gatherAnimationController.animator;
            gatherAnimationController.gatherTriggerName = gatherTriggerName;
            gatherAnimationController.gatherStateName = "Gathering Objects";
            gatherAnimationController.fallbackDuration = GetGatherDuration();
            gatherAnimationController.useImportedGatherAnimation = useImportedGatherAnimation;
            yield return gatherAnimationController.PlayGatherAnimation();
        }
        else
        {
            if (debugLogs)
            {
                Debug.LogWarning("RegolithCollectionManager: PlayerGatherAnimationController is missing. Using fallback gather duration.", this);
            }

            float waitTime = GetGatherDuration();
            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
            }
        }

        if (target != null)
        {
            CompleteCollection(target);
        }

        isGathering = false;
    }

    private void CompleteCollection(RegolithCollectable collectedTarget)
    {
        if (collectedTarget == null)
        {
            return;
        }

        bool collected = collectedTarget.Collect(resources);
        if (!collected)
        {
            ShowInventoryFull();
            return;
        }

        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.ShowMessage("+" + collectedTarget.amount + " Regolith");
        }

        currentNearest = null;
        RefreshCollectables();
        RegolithCollectable nextNearest = FindNearestCollectable();
        if (nextNearest != null)
        {
            currentNearest = nextNearest;
            currentNearest.SetHighlighted(true);
        }
    }

    private float GetGatherDuration()
    {
        if (!useImportedGatherAnimation)
        {
            return Mathf.Max(0.05f, fallbackGatherDuration);
        }

        if (gatheringAnimationClip != null)
        {
            return Mathf.Max(0.05f, gatheringAnimationClip.length);
        }

        if (debugLogs)
        {
            Debug.LogWarning("RegolithCollectionManager: Gathering Objects animation clip is missing. Using fallbackGatherDuration.", this);
        }

        return Mathf.Max(0.05f, fallbackGatherDuration);
    }

    private bool HasAnimatorTrigger(string triggerName)
    {
        if (playerAnimator == null || string.IsNullOrWhiteSpace(triggerName))
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in playerAnimator.parameters)
        {
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                return true;
            }
        }

        return false;
    }

    private RegolithCollectable FindNearestCollectable()
    {
        if (player == null)
        {
            return null;
        }

        float maxDistanceSqr = detectionRadius * detectionRadius;
        float bestDistanceSqr = float.PositiveInfinity;
        RegolithCollectable best = null;

        for (int i = collectables.Count - 1; i >= 0; i--)
        {
            RegolithCollectable collectable = collectables[i];
            if (collectable == null)
            {
                collectables.RemoveAt(i);
                continue;
            }

            if (collectable.collected || !collectable.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 targetPoint = collectable.transform.position;
            if (collectable.triggerCollider != null)
            {
                targetPoint = collectable.triggerCollider.ClosestPoint(player.position);
            }

            float distanceSqr = (targetPoint - player.position).sqrMagnitude;
            if (distanceSqr <= maxDistanceSqr && distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                best = collectable;
            }
        }

        return best;
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        if (resources == null)
        {
            resources = player != null
                ? player.GetComponent<PlayerSurvivalResources>()
                : FindAnyObjectByType<PlayerSurvivalResources>();
        }

        if (playerAnimator == null)
        {
            playerAnimator = player != null
                ? player.GetComponentInChildren<Animator>()
                : FindAnyObjectByType<Animator>();
        }

        if (gatherAnimationController == null)
        {
            gatherAnimationController = player != null ? player.GetComponent<PlayerGatherAnimationController>() : null;
            if (gatherAnimationController == null && player != null)
            {
                gatherAnimationController = player.gameObject.AddComponent<PlayerGatherAnimationController>();
            }

            if (gatherAnimationController == null)
            {
                gatherAnimationController = FindAnyObjectByType<PlayerGatherAnimationController>();
            }
        }

        if (gatherAnimationController != null)
        {
            gatherAnimationController.animator = playerAnimator;
            gatherAnimationController.playerControllerToDisable = player != null ? player.GetComponent<ThirdPersonPlayerController>() : null;
            gatherAnimationController.playerRigidbody = player != null ? player.GetComponent<Rigidbody>() : null;
            gatherAnimationController.useImportedGatherAnimation = useImportedGatherAnimation;
            gatherAnimationController.fallbackDuration = Mathf.Max(0.05f, fallbackGatherDuration);
        }

        if (regolithParent == null)
        {
            GameObject parentObject = GameObject.Find("Regolith_Road");
            regolithParent = parentObject != null ? parentObject.transform : null;
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptText != null)
        {
            promptText.text = visible ? promptMessage : string.Empty;
        }

        if (promptUI != null)
        {
            promptUI.SetActive(visible);
            return;
        }

        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.SetPrompt(visible ? promptMessage : string.Empty);
        }
    }

    private void ShowInventoryFull()
    {
        if (debugLogs)
        {
            Debug.Log("Regolith inventory full");
        }

        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.ShowMessage("Regolith inventory full");
        }
    }
}
