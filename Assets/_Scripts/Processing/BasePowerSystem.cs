using UnityEngine;

public class BasePowerSystem : MonoBehaviour
{
    [SerializeField] private bool powerOnline;
    [SerializeField] private SolarPowerState solarPowerState;
    [SerializeField] private PlayerSurvivalResources playerResources;

    public bool IsPowerOnline => powerOnline;

    private void Awake()
    {
        ResolveReferences();
        if (solarPowerState != null && solarPowerState.IsOnline)
        {
            SetPowerOnline(true);
        }
        else if (playerResources != null)
        {
            powerOnline = playerResources.IsPowerOnline();
        }
    }

    public void SetPowerOnline(bool online)
    {
        ResolveReferences();
        powerOnline = online;

        if (online && solarPowerState != null && !solarPowerState.IsOnline)
        {
            solarPowerState.SetOnline();
        }

        if (playerResources != null)
        {
            playerResources.SetPowerOnline(online);
        }

        Debug.Log("Power: " + (powerOnline ? "ONLINE" : "OFFLINE"), this);
    }

    private void ResolveReferences()
    {
        if (solarPowerState == null)
        {
            solarPowerState = FindAnyObjectByType<SolarPowerState>();
        }

        if (playerResources == null)
        {
            playerResources = FindAnyObjectByType<PlayerSurvivalResources>();
        }
    }
}
