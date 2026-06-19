using UnityEngine;

public enum ObjectiveState
{
    CollectRegolith,
    ProcessOxygen,
    OpenBase,
    Complete
}

public class GameManager : MonoBehaviour
{
    [SerializeField] private ObjectiveState currentState = ObjectiveState.CollectRegolith;
    [SerializeField] private int regolithNeededForProcessing = 5;
    [SerializeField] private SimpleHUD hud;

    public ObjectiveState CurrentState => currentState;
    public string CurrentObjectiveText => GetObjectiveText(currentState);

    private void Awake()
    {
        if (hud == null)
        {
            hud = FindObjectOfType<SimpleHUD>();
        }
    }

    private void Start()
    {
        UpdateHud();
    }

    public void OnRegolithPickedUp()
    {
        PlayerInventory inventory = FindObjectOfType<PlayerInventory>();
        if (currentState == ObjectiveState.CollectRegolith && inventory != null && inventory.Regolith >= regolithNeededForProcessing)
        {
            SetObjective(ObjectiveState.ProcessOxygen);
        }
    }

    public void OnOxygenProcessed()
    {
        if (currentState == ObjectiveState.CollectRegolith || currentState == ObjectiveState.ProcessOxygen)
        {
            SetObjective(ObjectiveState.OpenBase);
        }
    }

    public void OnBaseOpened()
    {
        SetObjective(ObjectiveState.Complete);

        if (hud != null)
        {
            hud.ShowWin("Base opened. Mission complete.");
        }
    }

    public void SetHud(SimpleHUD newHud)
    {
        hud = newHud;
        UpdateHud();
    }

    private void SetObjective(ObjectiveState state)
    {
        currentState = state;
        UpdateHud();
    }

    private void UpdateHud()
    {
        if (hud != null)
        {
            hud.SetObjective(CurrentObjectiveText);
        }
    }

    private string GetObjectiveText(ObjectiveState state)
    {
        switch (state)
        {
            case ObjectiveState.CollectRegolith:
                return "Collect regolith samples";
            case ObjectiveState.ProcessOxygen:
                return "Process regolith into oxygen";
            case ObjectiveState.OpenBase:
                return "Use oxygen to open the base";
            case ObjectiveState.Complete:
                return "Mission complete";
            default:
                return string.Empty;
        }
    }
}
