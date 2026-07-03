using UnityEngine;
using UnityEngine.Events;

public class SolarPowerState : MonoBehaviour
{
    public float requiredCleanPercent = 0.75f;
    public UnityEvent<float> OnCleanPercentChanged = new UnityEvent<float>();
    public UnityEvent OnPowerOnline = new UnityEvent();

    public bool IsOnline { get; private set; }
    public float CleanPercent { get; private set; }

    public void SetCleanPercent(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(CleanPercent, clamped))
        {
            return;
        }

        CleanPercent = clamped;
        OnCleanPercentChanged.Invoke(CleanPercent);

        if (CleanPercent >= requiredCleanPercent)
        {
            SetOnline();
        }
    }

    public void SetOnline()
    {
        if (IsOnline)
        {
            return;
        }

        IsOnline = true;
        Debug.Log("Solar power ONLINE");
        OnPowerOnline.Invoke();
    }

    public void ResetPower()
    {
        IsOnline = false;
        CleanPercent = 0f;
        OnCleanPercentChanged.Invoke(CleanPercent);
    }
}
