using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AirlockEnterTrigger : MonoBehaviour
{
    public AirlockController airlock;

    private void Reset()
    {
        EnsureTriggerCollider();
        airlock = GetComponentInParent<AirlockController>();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerSurvivalResources player = other.GetComponentInParent<PlayerSurvivalResources>();
        if (player == null)
        {
            return;
        }

        if (airlock != null && airlock.isOpen)
        {
            Debug.Log("Mission complete: LUNAR COLONY ONLINE");

            if (SurvivalHUD.Instance != null)
            {
                SurvivalHUD.Instance.ShowMessage("LUNAR COLONY ONLINE", 5f);
            }
        }
        else if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.ShowMessage("Airlock is sealed");
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
}
