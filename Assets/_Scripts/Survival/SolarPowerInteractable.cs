using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SolarPowerInteractable : MonoBehaviour, IInteractable
{
    public bool isActivated = false;
    public SolarPanelCleaningMinigame cleaningMinigame;
    public SolarPowerState powerState;
    public Light statusLight;
    public Renderer emissiveRenderer;
    public Color onlineColor = new Color(0.2f, 0.9f, 1f);

    private void OnEnable()
    {
        if (powerState != null)
        {
            powerState.OnPowerOnline.AddListener(HandlePowerOnline);
            if (powerState.IsOnline)
            {
                HandlePowerOnline();
            }
        }
    }

    private void OnDisable()
    {
        if (powerState != null)
        {
            powerState.OnPowerOnline.RemoveListener(HandlePowerOnline);
        }
    }

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
        if (powerState != null && powerState.IsOnline)
        {
            return "Solar power online";
        }

        if (cleaningMinigame != null)
        {
            return cleaningMinigame.IsCleaning ? string.Empty : "Press E\nUse cleaning tool";
        }

        return isActivated ? "Solar power online" : "Use generator to restore power";
    }

    public bool CanInteract(PlayerSurvivalResources player)
    {
        if (powerState != null && powerState.IsOnline)
        {
            return false;
        }

        if (cleaningMinigame != null)
        {
            return player != null;
        }

        return false;
    }

    public void Interact(PlayerSurvivalResources player)
    {
        if (player == null)
        {
            return;
        }

        if (powerState != null && powerState.IsOnline)
        {
            return;
        }

        if (cleaningMinigame != null)
        {
            cleaningMinigame.BeginCleaning();
            return;
        }

        ShowMessage("Use the solar generator to restore power");
    }

    private void HandlePowerOnline()
    {
        isActivated = true;
        SetOnlineVisuals();
    }

    private void SetOnlineVisuals()
    {
        if (statusLight != null)
        {
            statusLight.enabled = true;
            statusLight.color = onlineColor;
        }

        if (emissiveRenderer != null)
        {
            Material material = emissiveRenderer.material;
            material.color = onlineColor;

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", onlineColor * 1.5f);
            }
        }
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
