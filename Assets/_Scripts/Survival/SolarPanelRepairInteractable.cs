using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SolarPanelRepairInteractable : MonoBehaviour, IInteractable
{
    public SolarPanelRepairController repairController;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    public string GetPrompt(PlayerSurvivalResources player)
    {
        if (repairController == null || repairController.State != SolarPanelRepairState.Damaged)
        {
            return string.Empty;
        }

        int required = repairController.requiredRegolithPanelsToRepair;
        PlayerInventory inventory = GetInventory(player);
        int available = inventory != null ? inventory.Concrete : 0;

        if (available >= required)
        {
            return $"Press E\nRepair panel ({required} Regolith Panels)";
        }

        return $"Damaged panel\nNeed {required} Regolith Panels ({available}/{required})";
    }

    public bool CanInteract(PlayerSurvivalResources player)
    {
        return player != null
            && repairController != null
            && repairController.State == SolarPanelRepairState.Damaged;
    }

    public void Interact(PlayerSurvivalResources player)
    {
        if (player == null || repairController == null)
        {
            return;
        }

        repairController.TryRepair(GetInventory(player));
    }

    private static PlayerInventory GetInventory(PlayerSurvivalResources player)
    {
        return player != null ? player.GetComponent<PlayerInventory>() : null;
    }

    private void EnsureTriggerCollider()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}
