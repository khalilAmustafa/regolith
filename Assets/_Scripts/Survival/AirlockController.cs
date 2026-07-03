using System.Collections;
using UnityEngine;

public class AirlockController : MonoBehaviour, IInteractable
{
    public float storedOxygenCostToOpen = 30f;
    public float minimumSuitOxygenToEnter = 15f;
    public float pressurizationTime = 2f;

    public Animator airlockAnimator;
    public string openTriggerName = "Open";
    public string closeTriggerName = "Close";
    public string isOpenBoolName = "IsOpen";
    public bool useBoolParameter = false;

    public bool isOpen = false;
    public bool isPressurizing = false;

    public Transform enterPoint;
    public Collider blockerCollider;

    private void Reset()
    {
        EnsureTriggerCollider();
        airlockAnimator = GetComponentInParent<Animator>();
    }

    private void OnValidate()
    {
        storedOxygenCostToOpen = Mathf.Max(0f, storedOxygenCostToOpen);
        minimumSuitOxygenToEnter = Mathf.Max(0f, minimumSuitOxygenToEnter);
        pressurizationTime = Mathf.Max(0f, pressurizationTime);
        EnsureTriggerCollider();
    }

    public string GetPrompt(PlayerSurvivalResources player)
    {
        if (isOpen)
        {
            return "Airlock open";
        }

        if (isPressurizing)
        {
            return "Pressurizing airlock...";
        }

        if (player == null || player.storedOxygen < storedOxygenCostToOpen)
        {
            return "Need " + storedOxygenCostToOpen.ToString("0.#") + " stored O2 to pressurize airlock";
        }

        if (player.currentSuitOxygen < minimumSuitOxygenToEnter)
        {
            return "Suit O2 too low to enter safely";
        }

        return "Press E to pressurize and open airlock";
    }

    public bool CanInteract(PlayerSurvivalResources player)
    {
        return player != null
            && !isOpen
            && !isPressurizing
            && player.HasStoredOxygen(storedOxygenCostToOpen)
            && player.HasEnoughSuitOxygen(minimumSuitOxygenToEnter);
    }

    public void Interact(PlayerSurvivalResources player)
    {
        if (player == null)
        {
            return;
        }

        if (isOpen)
        {
            ShowMessage("Airlock already open");
            return;
        }

        if (isPressurizing)
        {
            return;
        }

        if (!player.HasStoredOxygen(storedOxygenCostToOpen))
        {
            ShowMessage("Need " + storedOxygenCostToOpen.ToString("0.#") + " stored O2 to pressurize airlock");
            return;
        }

        if (!player.HasEnoughSuitOxygen(minimumSuitOxygenToEnter))
        {
            ShowMessage("Suit O2 too low to enter safely");
            return;
        }

        if (!player.SpendStoredOxygen(storedOxygenCostToOpen))
        {
            ShowMessage("Need " + storedOxygenCostToOpen.ToString("0.#") + " stored O2 to pressurize airlock");
            return;
        }

        StartCoroutine(PressurizeAndOpen());
    }

    public void CloseAirlock()
    {
        isOpen = false;
        PlayCloseAnimation();

        if (blockerCollider != null)
        {
            blockerCollider.enabled = true;
        }
    }

    private IEnumerator PressurizeAndOpen()
    {
        isPressurizing = true;
        yield return new WaitForSeconds(pressurizationTime);
        isPressurizing = false;
        isOpen = true;

        PlayOpenAnimation();

        if (blockerCollider != null)
        {
            blockerCollider.enabled = false;
        }

        ShowMessage("Airlock opened");
    }

    private void PlayOpenAnimation()
    {
        if (airlockAnimator == null)
        {
            Debug.LogWarning("AirlockController has no Animator assigned.", this);
            return;
        }

        if (useBoolParameter)
        {
            if (HasAnimatorParameter(isOpenBoolName, AnimatorControllerParameterType.Bool))
            {
                airlockAnimator.SetBool(isOpenBoolName, true);
            }
            else
            {
                Debug.LogWarning("Airlock Animator missing bool parameter '" + isOpenBoolName + "'.", this);
            }
        }
        else if (HasAnimatorParameter(openTriggerName, AnimatorControllerParameterType.Trigger))
        {
            airlockAnimator.SetTrigger(openTriggerName);
        }
        else
        {
            Debug.LogWarning("Airlock Animator missing trigger parameter '" + openTriggerName + "'.", this);
        }
    }

    private void PlayCloseAnimation()
    {
        if (airlockAnimator == null)
        {
            Debug.LogWarning("AirlockController has no Animator assigned.", this);
            return;
        }

        if (useBoolParameter)
        {
            if (HasAnimatorParameter(isOpenBoolName, AnimatorControllerParameterType.Bool))
            {
                airlockAnimator.SetBool(isOpenBoolName, false);
            }
            else
            {
                Debug.LogWarning("Airlock Animator missing bool parameter '" + isOpenBoolName + "'.", this);
            }
        }
        else if (HasAnimatorParameter(closeTriggerName, AnimatorControllerParameterType.Trigger))
        {
            airlockAnimator.SetTrigger(closeTriggerName);
        }
        else
        {
            Debug.LogWarning("Airlock Animator missing trigger parameter '" + closeTriggerName + "'.", this);
        }
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (airlockAnimator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in airlockAnimator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
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
