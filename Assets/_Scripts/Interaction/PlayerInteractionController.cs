using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractionController : MonoBehaviour
{
    private const int MaxOverlapResults = 32;

    private readonly List<IInteractable> nearbyInteractables = new List<IInteractable>();
    private readonly Collider[] overlapResults = new Collider[MaxOverlapResults];

    [SerializeField] private PlayerSurvivalResources playerResources;
    [SerializeField] private float interactionScanRadius = 3f;

    private IInteractable currentInteractable;

    private void Awake()
    {
        if (playerResources == null)
        {
            playerResources = GetComponent<PlayerSurvivalResources>();
        }
    }

    private void Update()
    {
        if (playerResources == null)
        {
            playerResources = GetComponent<PlayerSurvivalResources>();
        }

        RefreshNearbyInteractables();
        currentInteractable = GetClosestInteractable();

        if (currentInteractable == null)
        {
            SetPrompt(string.Empty);
            return;
        }

        SetPrompt(currentInteractable.GetPrompt(playerResources));

        if (Input.GetKeyDown(KeyCode.E))
        {
            currentInteractable.Interact(playerResources);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        IInteractable interactable = FindInteractable(other);
        if (interactable != null && !nearbyInteractables.Contains(interactable))
        {
            nearbyInteractables.Add(interactable);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        IInteractable interactable = FindInteractable(other);
        if (interactable != null)
        {
            nearbyInteractables.Remove(interactable);

            if (currentInteractable == interactable)
            {
                currentInteractable = null;
                SetPrompt(string.Empty);
            }
        }
    }

    private IInteractable FindInteractable(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
        {
            return interactable;
        }

        return other.GetComponentInParent<IInteractable>();
    }

    private IInteractable GetClosestInteractable()
    {
        IInteractable closest = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < nearbyInteractables.Count; i++)
        {
            IInteractable interactable = nearbyInteractables[i];
            if (interactable == null)
            {
                continue;
            }

            Component component = interactable as Component;
            if (component == null)
            {
                continue;
            }

            float distanceSqr = (component.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closest = interactable;
            }
        }

        return closest;
    }

    private void RefreshNearbyInteractables()
    {
        nearbyInteractables.Clear();

        float radius = interactionScanRadius > 0f ? interactionScanRadius : 3f;
        Vector3 scanCenter = transform.position + Vector3.up;
        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            scanCenter = transform.TransformPoint(characterController.center);
        }

        int count = Physics.OverlapSphereNonAlloc(
            scanCenter,
            radius,
            overlapResults,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            IInteractable interactable = FindInteractable(overlapResults[i]);
            if (interactable != null && !nearbyInteractables.Contains(interactable))
            {
                nearbyInteractables.Add(interactable);
            }
        }

        RemoveInvalidInteractables();
    }

    private void RemoveInvalidInteractables()
    {
        for (int i = nearbyInteractables.Count - 1; i >= 0; i--)
        {
            IInteractable interactable = nearbyInteractables[i];
            if (interactable == null || interactable as Component == null)
            {
                nearbyInteractables.RemoveAt(i);
            }
        }
    }

    private void SetPrompt(string prompt)
    {
        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.SetPrompt(prompt);
        }
    }
}
