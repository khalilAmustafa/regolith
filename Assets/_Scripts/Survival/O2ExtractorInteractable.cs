using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class O2ExtractorInteractable : MonoBehaviour, IInteractable
{
    public float regolithCost = 3f;
    public float oxygenProduced = 35f;
    public float processingTime = 2f;
    public bool isProcessing = false;
    public bool requirePower = true;

    public ParticleSystem processingParticles;
    public Light processingLight;
    public AudioSource processingAudio;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        regolithCost = Mathf.Max(0f, regolithCost);
        oxygenProduced = Mathf.Max(0f, oxygenProduced);
        processingTime = Mathf.Max(0f, processingTime);
        EnsureTriggerCollider();
    }

    public string GetPrompt(PlayerSurvivalResources player)
    {
        if (isProcessing)
        {
            return "O2 extractor processing...";
        }

        if (requirePower && (player == null || !player.powerOnline))
        {
            return "Restore solar power first";
        }

        if (player == null || player.regolithAmount < regolithCost)
        {
            return "Need " + regolithCost.ToString("0.#") + " regolith for O2 extraction";
        }

        return "Press E to process regolith into O2";
    }

    public bool CanInteract(PlayerSurvivalResources player)
    {
        if (player == null || isProcessing)
        {
            return false;
        }

        if (requirePower && !player.powerOnline)
        {
            return false;
        }

        return player.regolithAmount >= regolithCost;
    }

    public void Interact(PlayerSurvivalResources player)
    {
        if (player == null)
        {
            return;
        }

        if (requirePower && !player.powerOnline)
        {
            ShowMessage("Restore solar power first");
            return;
        }

        if (isProcessing)
        {
            ShowMessage("O2 extractor already processing");
            return;
        }

        if (player.regolithAmount < regolithCost)
        {
            ShowMessage("Need " + regolithCost.ToString("0.#") + " regolith for O2 extraction");
            return;
        }

        if (!player.SpendRegolith(regolithCost))
        {
            ShowMessage("Need " + regolithCost.ToString("0.#") + " regolith for O2 extraction");
            return;
        }

        StartCoroutine(ProcessOxygen(player));
    }

    private IEnumerator ProcessOxygen(PlayerSurvivalResources player)
    {
        isProcessing = true;
        SetProcessingVisuals(true);

        yield return new WaitForSeconds(processingTime);

        if (player != null)
        {
            player.AddStoredOxygen(oxygenProduced);
        }

        SetProcessingVisuals(false);
        isProcessing = false;
        ShowMessage("+" + oxygenProduced.ToString("0.#") + " Stored O2");
    }

    private void SetProcessingVisuals(bool active)
    {
        if (processingParticles != null)
        {
            if (active)
            {
                processingParticles.Play();
            }
            else
            {
                processingParticles.Stop();
            }
        }

        if (processingLight != null)
        {
            processingLight.enabled = active;
        }

        if (processingAudio != null)
        {
            if (active)
            {
                processingAudio.Play();
            }
            else
            {
                processingAudio.Stop();
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
