using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SolarPanelInteractionTrigger : MonoBehaviour
{
    public SolarPanelCleaningMinigame minigame;
    public GameObject promptUI;
    public KeyCode interactKey = KeyCode.E;
    public string playerTag = "Player";
    public float playerBoundsPadding = 0.75f;

    private bool playerInside;
    private Collider triggerCollider;
    private Transform playerTransform;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning("SolarPanelInteractionTrigger requires a trigger collider. Enabling Is Trigger.", this);
            triggerCollider.isTrigger = true;
        }

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            playerTransform = player.transform;
        }

        SetPromptVisible(false);
    }

    private void Update()
    {
        RefreshPlayerInsideFallback();

        if (minigame != null && minigame.IsCleaning)
        {
            SetPromptVisible(false);
            return;
        }

        SetPromptVisible(playerInside);

        if (!playerInside || !Input.GetKeyDown(interactKey))
        {
            return;
        }

        if (minigame == null)
        {
            Debug.LogWarning("SolarPanelInteractionTrigger cannot start cleaning because Minigame is not assigned.", this);
            return;
        }

        minigame.BeginCleaning();
        SetPromptVisible(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerInside = true;
        if (minigame == null || !minigame.IsCleaning)
        {
            SetPromptVisible(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerInside = false;
        SetPromptVisible(false);
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag))
        {
            playerTransform = other.transform;
            return true;
        }

        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
            {
                playerTransform = current;
                return true;
            }

            current = current.parent;
        }

        PlayerSurvivalResources resources = other.GetComponentInParent<PlayerSurvivalResources>();
        if (resources != null && resources.CompareTag(playerTag))
        {
            playerTransform = resources.transform;
            return true;
        }

        return false;
    }

    private void RefreshPlayerInsideFallback()
    {
        if (triggerCollider == null)
        {
            return;
        }

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        if (playerTransform == null)
        {
            playerInside = false;
            return;
        }

        Bounds paddedBounds = triggerCollider.bounds;
        paddedBounds.Expand(playerBoundsPadding);
        Vector3 probePosition = playerTransform.position + Vector3.up;
        playerInside = paddedBounds.Contains(probePosition) || paddedBounds.Contains(playerTransform.position);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptUI != null && promptUI.activeSelf != visible)
        {
            promptUI.SetActive(visible);
        }
    }
}
