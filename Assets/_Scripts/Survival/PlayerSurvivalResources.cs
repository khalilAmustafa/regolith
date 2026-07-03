using UnityEngine;

public class PlayerSurvivalResources : MonoBehaviour
{
    [Header("Suit O2")]
    public float suitO2 = 100f;
    public float maxSuitO2 = 100f;
    public float maxSuitOxygen = 100f;
    public float currentSuitOxygen = 100f;
    public float suitOxygenDrainPerSecond = 0.18f;

    [Header("Stored O2")]
    public float storedO2 = 0f;
    public float maxStoredO2 = 100f;
    public float storedOxygen = 0f;
    public float maxStoredOxygen = 100f;

    [Header("Materials")]
    public int regolith = 0;
    public int maxRegolith = 10;
    public float regolithAmount = 0f;
    public float maxRegolithAmount = 10f;
    public int concrete = 0;
    public int maxConcrete = 20;

    [Header("Power")]
    public bool powerOnline = false;
    public bool isDead = false;

    private void Awake()
    {
        ClampValues();
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        currentSuitOxygen -= suitOxygenDrainPerSecond * Time.deltaTime;
        ClampValues();

        if (currentSuitOxygen <= 0f)
        {
            isDead = true;
            Debug.Log("Player oxygen depleted");

            if (SurvivalHUD.Instance != null)
            {
                SurvivalHUD.Instance.ShowMessage("Player oxygen depleted");
            }
        }
    }

    private void OnValidate()
    {
        ClampValues();
    }

    public bool CanAddRegolith(int amount)
    {
        return amount > 0 && regolith < maxRegolith;
    }

    public bool AddRegolith(int amount)
    {
        if (!CanAddRegolith(amount))
        {
            return false;
        }

        regolith += amount;
        ClampValues();
        RefreshHUD();
        return true;
    }

    public void AddRegolith(float amount)
    {
        AddRegolith(Mathf.RoundToInt(amount));
    }

    public bool HasRegolith(int amount)
    {
        return amount <= 0 || regolith >= amount;
    }

    public bool ConsumeRegolith(int amount)
    {
        if (amount < 0 || regolith < amount)
        {
            return false;
        }

        regolith -= amount;
        ClampValues();
        RefreshHUD();
        return true;
    }

    public bool SpendRegolith(float amount)
    {
        return ConsumeRegolith(Mathf.CeilToInt(amount));
    }

    public int GetRegolith()
    {
        return regolith;
    }

    public int GetMaxRegolith()
    {
        return maxRegolith;
    }

    public bool CanAddStoredO2(float amount)
    {
        return amount > 0f && storedOxygen < maxStoredOxygen;
    }

    public bool AddStoredO2(float amount)
    {
        if (!CanAddStoredO2(amount))
        {
            return false;
        }

        storedOxygen += amount;
        ClampValues();
        RefreshHUD();
        return true;
    }

    public void AddStoredOxygen(float amount)
    {
        AddStoredO2(amount);
    }

    public bool SpendStoredOxygen(float amount)
    {
        if (amount < 0f || storedOxygen < amount)
        {
            return false;
        }

        storedOxygen -= amount;
        ClampValues();
        RefreshHUD();
        return true;
    }

    public float GetStoredO2()
    {
        return storedOxygen;
    }

    public float GetMaxStoredO2()
    {
        return maxStoredOxygen;
    }

    public bool CanAddConcrete(int amount)
    {
        return amount > 0 && concrete < maxConcrete;
    }

    public bool AddConcrete(int amount)
    {
        if (!CanAddConcrete(amount))
        {
            return false;
        }

        concrete += amount;
        ClampValues();
        RefreshHUD();
        return true;
    }

    public int GetConcrete()
    {
        return concrete;
    }

    public int GetMaxConcrete()
    {
        return maxConcrete;
    }

    public void SetPowerOnline(bool online)
    {
        powerOnline = online;
        RefreshHUD();
    }

    public bool IsPowerOnline()
    {
        return powerOnline;
    }

    public bool HasEnoughSuitOxygen(float minimum)
    {
        return currentSuitOxygen >= minimum;
    }

    public bool HasStoredOxygen(float amount)
    {
        return storedOxygen >= amount;
    }

    private void ClampValues()
    {
        maxSuitOxygen = Mathf.Max(0f, maxSuitOxygen);
        maxSuitO2 = maxSuitOxygen;
        maxRegolith = Mathf.Max(0, maxRegolith);
        maxRegolithAmount = maxRegolith;
        maxStoredOxygen = Mathf.Max(0f, maxStoredOxygen);
        maxStoredO2 = maxStoredOxygen;
        maxConcrete = Mathf.Max(0, maxConcrete);

        currentSuitOxygen = Mathf.Clamp(currentSuitOxygen, 0f, maxSuitOxygen);
        suitO2 = currentSuitOxygen;
        regolith = Mathf.Clamp(regolith, 0, maxRegolith);
        regolithAmount = regolith;
        storedOxygen = Mathf.Clamp(storedOxygen, 0f, maxStoredOxygen);
        storedO2 = storedOxygen;
        concrete = Mathf.Clamp(concrete, 0, maxConcrete);
        suitOxygenDrainPerSecond = Mathf.Max(0f, suitOxygenDrainPerSecond);
    }
    private void RefreshHUD()
    {
        // SurvivalHUD updates every frame; this hook keeps resource changes explicit.
    }
}
