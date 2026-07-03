using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SolarPowerHUDAdapter : MonoBehaviour
{
    public SolarPowerState powerState;
    public TMP_Text powerLabel;
    public Text powerFallbackLabel;
    public string offlineText = "Power: OFFLINE";
    public string onlineText = "Power: ONLINE";

    private void Start()
    {
        if (powerState == null)
        {
            Debug.LogWarning("SolarPowerHUDAdapter has no SolarPowerState assigned. Assign it if this HUD label should follow the cleaning minigame.", this);
            UpdateLabel(false);
            return;
        }

        powerState.OnPowerOnline.AddListener(HandlePowerOnline);
        UpdateLabel(powerState.IsOnline);

        if (powerLabel == null && powerFallbackLabel == null)
        {
            Debug.LogWarning("SolarPowerHUDAdapter has no HUD power label assigned. SolarPowerState still works; assign a label to show Power: ONLINE/OFFLINE.", this);
        }
    }

    private void OnDestroy()
    {
        if (powerState != null)
        {
            powerState.OnPowerOnline.RemoveListener(HandlePowerOnline);
        }
    }

    private void HandlePowerOnline()
    {
        UpdateLabel(true);
    }

    private void UpdateLabel(bool online)
    {
        string value = online ? onlineText : offlineText;

        if (powerLabel != null)
        {
            powerLabel.text = value;
        }

        if (powerFallbackLabel != null)
        {
            powerFallbackLabel.text = value;
        }
    }
}
