using UnityEngine;

public enum SolarPanelRepairState
{
    Normal,
    Damaged,
    Repaired
}

/// <summary>
/// Owns the damaged solar panel visual flow:
/// Normal (no crack, no patch) -> Damaged on impact (crack only) ->
/// Repaired after spending Regolith Panels (patch only).
/// The crack itself is still shown/hidden by the existing ImpactCrackController,
/// so the cinematic and strike timing are untouched.
/// </summary>
public class SolarPanelRepairController : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Existing crack controller. Its spawned crack is used as the damaged visual.")]
    public ImpactCrackController impactCrackController;
    [Tooltip("Optional explicit crack object. If left empty, the crack spawned by the ImpactCrackController is used.")]
    public GameObject crackVisual;
    [Tooltip("Patch panel object shown only after a successful repair.")]
    public GameObject patchPanelVisual;

    [Header("Repair")]
    public int requiredRegolithPanelsToRepair = 1;
    public bool debugLogs = true;

    [SerializeField] private SolarPanelRepairState state = SolarPanelRepairState.Normal;

    public SolarPanelRepairState State => state;

    private void OnEnable()
    {
        if (impactCrackController != null)
        {
            impactCrackController.CrackShown += HandleCrackShown;
        }
    }

    private void OnDisable()
    {
        if (impactCrackController != null)
        {
            impactCrackController.CrackShown -= HandleCrackShown;
        }
    }

    private void Start()
    {
        // Before impact nothing damage-related is visible. The crack is already
        // hidden by ImpactCrackController.Start(); enforce the patch here.
        if (state == SolarPanelRepairState.Normal)
        {
            SetActiveSafe(crackVisual, false);
            SetActiveSafe(patchPanelVisual, false);
        }
    }

    private void HandleCrackShown()
    {
        // The crack was just made visible by the existing impact flow.
        // Only record the state change and make sure the patch stays hidden.
        if (state == SolarPanelRepairState.Repaired)
        {
            Log("Crack shown after repair; ignoring.");
            return;
        }

        state = SolarPanelRepairState.Damaged;
        SetActiveSafe(patchPanelVisual, false);
        Log("Solar panel is now Damaged.");
    }

    public bool TryRepair(PlayerInventory inventory)
    {
        if (state != SolarPanelRepairState.Damaged)
        {
            Log($"Repair ignored: panel state is {state}.");
            return false;
        }

        if (inventory == null)
        {
            Debug.LogWarning("SolarPanelRepairController: no PlayerInventory provided for repair.", this);
            return false;
        }

        if (inventory.Concrete < requiredRegolithPanelsToRepair
            || !inventory.TrySpendConcrete(requiredRegolithPanelsToRepair))
        {
            ShowMessage($"Need {requiredRegolithPanelsToRepair} Regolith Panels to repair ({inventory.Concrete} available)");
            Log($"Repair failed: {inventory.Concrete}/{requiredRegolithPanelsToRepair} Regolith Panels.");
            return false;
        }

        SetState(SolarPanelRepairState.Repaired);
        ShowMessage("Solar panel repaired");
        Log($"Repair succeeded: spent {requiredRegolithPanelsToRepair} Regolith Panels.");
        return true;
    }

    public void SetState(SolarPanelRepairState newState)
    {
        state = newState;
        ApplyState();
    }

    private void ApplyState()
    {
        bool crackActive = state == SolarPanelRepairState.Damaged;
        bool patchActive = state == SolarPanelRepairState.Repaired;

        if (crackVisual != null)
        {
            crackVisual.SetActive(crackActive);
        }
        else if (impactCrackController != null)
        {
            // The impact flow calls ShowCrack() itself, so this only ever needs
            // to hide the spawned crack (Normal/Repaired states).
            if (!crackActive)
            {
                impactCrackController.HideCrack();
            }
        }

        SetActiveSafe(patchPanelVisual, patchActive);
        Log($"Applied state {state}: crack={crackActive}, patch={patchActive}.");
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void ShowMessage(string message)
    {
        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.ShowMessage(message);
        }
    }

    private void Log(string message)
    {
        if (debugLogs)
        {
            Debug.Log("SolarPanelRepairController: " + message, this);
        }
    }
}
