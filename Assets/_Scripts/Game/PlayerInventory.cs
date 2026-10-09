using UnityEngine;
using UnityEngine.Serialization;

#pragma warning disable 0618
public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private int regolith;
    [SerializeField] private int maxRegolith = 10;
    [FormerlySerializedAs("oxygen")]
    [SerializeField] private int storedOxygen;
    [SerializeField] private int maxStoredOxygen = 100;
    [SerializeField] private int concrete;

    public int Regolith => regolith;
    public int MaxRegolith => maxRegolith;
    public int StoredOxygen => storedOxygen;
    public int MaxStoredOxygen => maxStoredOxygen;
    public int Oxygen => storedOxygen;
    public int Concrete => concrete;

    private SimpleHUD hud;
    private PlayerSurvivalResources survivalResources;

    private void Awake()
    {
        hud = FindFirstObjectByType<SimpleHUD>();
        survivalResources = GetComponent<PlayerSurvivalResources>();
        SyncFromSurvivalResources();
        UpdateHud();
    }

    private void Start()
    {
        UpdateHud();
    }

    private void OnValidate()
    {
        maxRegolith = Mathf.Max(0, maxRegolith);
        regolith = Mathf.Clamp(regolith, 0, maxRegolith);
        maxStoredOxygen = Mathf.Max(0, maxStoredOxygen);
        storedOxygen = Mathf.Clamp(storedOxygen, 0, maxStoredOxygen);
        concrete = Mathf.Max(0, concrete);
    }

    public void AddRegolith(int amount)
    {
        TryAddRegolith(amount);
    }

    public bool TryAddRegolith(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (!CanAddRegolith(amount))
        {
            Debug.Log($"PlayerInventory: regolith inventory full ({regolith}/{maxRegolith}). Cannot add {amount}.", this);
            return false;
        }

        int previous = regolith;
        regolith += amount;
        regolith = Mathf.Clamp(regolith, 0, maxRegolith);
        SyncSurvivalRegolith();
        LogResources();
        UpdateHud();
        return regolith > previous;
    }

    public bool CanAddRegolith(int amount)
    {
        return amount > 0 && regolith < maxRegolith;
    }

    public bool TrySpendRegolith(int amount)
    {
        return SpendRegolith(amount);
    }

    public bool SpendRegolith(int amount)
    {
        if (amount < 0 || regolith < amount)
        {
            Debug.Log($"PlayerInventory: not enough regolith to spend {amount}. Current regolith: {regolith}/{maxRegolith}", this);
            return false;
        }

        regolith -= amount;
        SyncSurvivalRegolith();
        LogResources();
        UpdateHud();
        return true;
    }

    public void SetRegolith(int amount, int maxAmount)
    {
        maxRegolith = Mathf.Max(0, maxAmount);
        int previous = regolith;
        regolith = Mathf.Clamp(amount, 0, maxRegolith);

        if (regolith != previous)
        {
            LogResources();
        }

        UpdateHud();
    }

    public void AddOxygen(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        AddStoredOxygen(amount);
    }

    public void AddStoredOxygen(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        int previous = storedOxygen;
        storedOxygen = Mathf.Clamp(storedOxygen + amount, 0, maxStoredOxygen);
        SyncSurvivalOxygen(storedOxygen - previous);
        LogResources();
        UpdateHud();
    }

    public bool SpendOxygen(int amount)
    {
        if (amount < 0 || storedOxygen < amount)
        {
            return false;
        }

        storedOxygen -= amount;
        LogResources();
        UpdateHud();
        return true;
    }

    public void AddConcrete(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        concrete += amount;
        SyncSurvivalConcrete(amount);
        LogResources();
        UpdateHud();
    }

    public bool TrySpendConcrete(int amount)
    {
        if (amount < 0 || concrete < amount)
        {
            Debug.Log($"PlayerInventory: not enough Regolith Panels to spend {amount}. Current: {concrete}", this);
            return false;
        }

        concrete -= amount;
        SyncSurvivalConcreteTotal();
        LogResources();
        UpdateHud();
        return true;
    }

    private void UpdateHud()
    {
        if (hud == null)
        {
            hud = FindFirstObjectByType<SimpleHUD>();
        }

        if (hud != null)
        {
            hud.SetInventory(regolith, storedOxygen, concrete);
        }
    }

    private void LogResources()
    {
        Debug.Log($"Regolith: {regolith} | O2: {storedOxygen} | Concrete: {concrete}", this);
    }

    private void ResolveSurvivalResources()
    {
        if (survivalResources == null)
        {
            survivalResources = GetComponent<PlayerSurvivalResources>();
        }
    }

    private void SyncFromSurvivalResources()
    {
        ResolveSurvivalResources();
        if (survivalResources == null)
        {
            return;
        }

        maxRegolith = Mathf.Max(maxRegolith, survivalResources.GetMaxRegolith());
        maxStoredOxygen = Mathf.Max(maxStoredOxygen, Mathf.RoundToInt(survivalResources.GetMaxStoredO2()));
        regolith = Mathf.Clamp(survivalResources.GetRegolith(), 0, maxRegolith);
        storedOxygen = Mathf.Clamp(Mathf.RoundToInt(survivalResources.GetStoredO2()), 0, maxStoredOxygen);
        concrete = Mathf.Max(concrete, survivalResources.GetConcrete());
    }

    private void SyncSurvivalRegolith()
    {
        ResolveSurvivalResources();
        if (survivalResources == null)
        {
            return;
        }

        survivalResources.regolith = Mathf.Clamp(regolith, 0, survivalResources.maxRegolith);
        survivalResources.regolithAmount = survivalResources.regolith;
    }

    private void SyncSurvivalOxygen(int addedAmount)
    {
        ResolveSurvivalResources();
        if (survivalResources == null || addedAmount <= 0)
        {
            return;
        }

        survivalResources.AddStoredO2(addedAmount);
    }

    private void SyncSurvivalConcreteTotal()
    {
        ResolveSurvivalResources();
        if (survivalResources == null)
        {
            return;
        }

        survivalResources.concrete = Mathf.Max(0, concrete);
    }

    private void SyncSurvivalConcrete(int addedAmount)
    {
        ResolveSurvivalResources();
        if (survivalResources == null || addedAmount <= 0)
        {
            return;
        }

        survivalResources.AddConcrete(addedAmount);
    }
}
#pragma warning restore 0618
