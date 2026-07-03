using UnityEngine;

public class SolarPowerResourceSync : MonoBehaviour
{
    public SolarPowerState solarPowerState;
    public PlayerSurvivalResources resources;

    private void Start()
    {
        ResolveReferences();

        if (solarPowerState != null)
        {
            solarPowerState.OnPowerOnline.AddListener(HandlePowerOnline);
            SyncNow();
        }
    }

    private void OnDestroy()
    {
        if (solarPowerState != null)
        {
            solarPowerState.OnPowerOnline.RemoveListener(HandlePowerOnline);
        }
    }

    private void Update()
    {
        SyncNow();
    }

    private void HandlePowerOnline()
    {
        SyncNow();
    }

    private void SyncNow()
    {
        ResolveReferences();

        if (resources != null && solarPowerState != null)
        {
            resources.SetPowerOnline(solarPowerState.IsOnline);
        }
    }

    private void ResolveReferences()
    {
        if (resources == null)
        {
            resources = FindAnyObjectByType<PlayerSurvivalResources>();
        }

        if (solarPowerState == null)
        {
            solarPowerState = FindAnyObjectByType<SolarPowerState>();
        }
    }
}
