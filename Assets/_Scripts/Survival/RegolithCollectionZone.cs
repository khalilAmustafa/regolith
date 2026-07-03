using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RegolithCollectionZone : MonoBehaviour, IInteractable
{
    public float collectAmount = 1f;
    public bool infinite = true;
    public float remainingAmount = 999f;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        collectAmount = Mathf.Max(0f, collectAmount);
        remainingAmount = Mathf.Max(0f, remainingAmount);
        EnsureTriggerCollider();
    }

    public string GetPrompt(PlayerSurvivalResources player)
    {
        if (!infinite && remainingAmount <= 0f)
        {
            return "Regolith depleted";
        }

        return "Press E to collect regolith";
    }

    public bool CanInteract(PlayerSurvivalResources player)
    {
        if (player == null)
        {
            return false;
        }

        if (!infinite && remainingAmount <= 0f)
        {
            return false;
        }

        return player.regolithAmount < player.maxRegolithAmount;
    }

    public void Interact(PlayerSurvivalResources player)
    {
        if (player == null)
        {
            return;
        }

        if (player.regolithAmount >= player.maxRegolithAmount)
        {
            ShowMessage("Regolith storage full");
            return;
        }

        if (!infinite && remainingAmount <= 0f)
        {
            ShowMessage("Regolith depleted");
            return;
        }

        float amountToCollect = infinite ? collectAmount : Mathf.Min(collectAmount, remainingAmount);
        player.AddRegolith(amountToCollect);

        if (!infinite)
        {
            remainingAmount = Mathf.Max(0f, remainingAmount - amountToCollect);
        }

        ShowMessage("+" + amountToCollect.ToString("0.#") + " Regolith");
    }

    private void EnsureTriggerCollider()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void ShowMessage(string message)
    {
        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.ShowMessage(message);
        }
    }
}
