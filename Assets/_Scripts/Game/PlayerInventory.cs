using UnityEngine;

#pragma warning disable 0618
public class PlayerInventory : MonoBehaviour
{
    public int Regolith { get; private set; }
    public int Oxygen { get; private set; }

    private SimpleHUD hud;

    private void Awake()
    {
        hud = FindFirstObjectByType<SimpleHUD>();
        UpdateHud();
    }

    private void Start()
    {
        UpdateHud();
    }

    public void AddRegolith(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Regolith += amount;
        UpdateHud();
    }

    public bool SpendRegolith(int amount)
    {
        if (amount < 0 || Regolith < amount)
        {
            return false;
        }

        Regolith -= amount;
        UpdateHud();
        return true;
    }

    public void AddOxygen(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Oxygen += amount;
        UpdateHud();
    }

    public bool SpendOxygen(int amount)
    {
        if (amount < 0 || Oxygen < amount)
        {
            return false;
        }

        Oxygen -= amount;
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
            hud.SetInventory(Regolith, Oxygen);
        }
    }
}
#pragma warning restore 0618
