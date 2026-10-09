using UnityEngine;

[RequireComponent(typeof(PlayerInventory))]
public class PlayerStationInteractor : MonoBehaviour
{
    [SerializeField] private float interactRadius = 1.25f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private LayerMask stationLayer = ~0;
    [SerializeField] private Transform detectionOrigin;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private BasePowerSystem powerSystem;
    [SerializeField] private int overlapCapacity = 32;

    private Collider[] overlapResults;
    private RoomTradeStation currentStation;
    private SimpleHUD simpleHud;
    private string lastLoggedStationPath;

    private void Awake()
    {
        overlapResults = new Collider[Mathf.Max(8, overlapCapacity)];
        ResolveReferences();
    }

    private void OnValidate()
    {
        interactRadius = Mathf.Max(0.1f, interactRadius);
        overlapCapacity = Mathf.Clamp(overlapCapacity, 8, 128);
    }

    private void OnDisable()
    {
        SetCurrentStation(null);
        SetPrompt(string.Empty);
    }

    private void Update()
    {
        ResolveReferences();

        RoomTradeStation nearest = FindNearestStation();
        SetCurrentStation(nearest);

        if (currentStation != null && Input.GetKeyDown(interactKey))
        {
            bool traded = currentStation.TryTrade(inventory, powerSystem);
            if (!traded)
            {
                ShowFailureMessage(currentStation);
            }
        }
    }

    public void Configure(float radius, KeyCode key, LayerMask layer, Transform origin = null, BasePowerSystem system = null)
    {
        interactRadius = Mathf.Max(0.1f, radius);
        interactKey = key;
        stationLayer = layer;

        if (origin != null)
        {
            detectionOrigin = origin;
        }

        if (system != null)
        {
            powerSystem = system;
        }

        if (overlapResults == null || overlapResults.Length != Mathf.Max(8, overlapCapacity))
        {
            overlapResults = new Collider[Mathf.Max(8, overlapCapacity)];
        }
    }

    private RoomTradeStation FindNearestStation()
    {
        Transform originTransform = detectionOrigin != null ? detectionOrigin : transform;
        Vector3 origin = originTransform.position;

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            interactRadius,
            overlapResults,
            stationLayer,
            QueryTriggerInteraction.Collide);

        RoomTradeStation nearest = null;
        float nearestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];
            overlapResults[i] = null;

            if (hit == null || !hit.enabled || !hit.gameObject.activeInHierarchy)
            {
                continue;
            }

            RoomTradeStation station = hit.GetComponentInParent<RoomTradeStation>();
            if (station == null || !station.isActiveAndEnabled || !station.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 closestPoint = hit.ClosestPoint(origin);
            float distanceSqr = (closestPoint - origin).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearest = station;
            }
        }

        return nearest;
    }

    private void SetCurrentStation(RoomTradeStation station)
    {
        if (currentStation == station)
        {
            if (currentStation != null)
            {
                SetPrompt("Press E: " + currentStation.GetTradeText());
            }

            return;
        }

        if (currentStation != null)
        {
            Debug.Log(
                $"PlayerStationInteractor unselecting station: {currentStation.StationName} at {RoomTradeStation.GetHierarchyPath(currentStation.transform)}",
                this);
            currentStation.Highlight(false);
        }

        currentStation = station;

        if (currentStation != null)
        {
            string currentPath = RoomTradeStation.GetHierarchyPath(currentStation.transform);
            if (lastLoggedStationPath != currentPath)
            {
                Debug.Log(
                    $"PlayerStationInteractor selected station: {currentStation.StationName} at {currentPath}",
                    this);
                lastLoggedStationPath = currentPath;
            }

            currentStation.Highlight(true);
            SetPrompt("Press E: " + currentStation.GetTradeText());
        }
        else
        {
            if (!string.IsNullOrEmpty(lastLoggedStationPath))
            {
                Debug.Log("PlayerStationInteractor cleared current station.", this);
                lastLoggedStationPath = null;
            }

            SetPrompt(string.Empty);
        }
    }

    private void ResolveReferences()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }

        if (powerSystem == null)
        {
            powerSystem = FindAnyObjectByType<BasePowerSystem>();
        }

        if (simpleHud == null)
        {
            simpleHud = FindAnyObjectByType<SimpleHUD>();
        }
    }

    private void SetPrompt(string text)
    {
        if (simpleHud != null)
        {
            simpleHud.SetPrompt(text);
        }

        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.SetPrompt(text);
        }
    }

    private void ShowFailureMessage(RoomTradeStation station)
    {
        string message = station != null && station.StationName == "Regolith Panel"
            ? "Not enough Regolith for Regolith Panel"
            : "Not enough Regolith";

        Debug.Log(message, this);
        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.ShowMessage(message);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform originTransform = detectionOrigin != null ? detectionOrigin : transform;
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(originTransform.position, interactRadius);
    }
}
