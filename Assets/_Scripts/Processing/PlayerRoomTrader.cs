using UnityEngine;

[RequireComponent(typeof(PlayerInventory))]
public class PlayerRoomTrader : MonoBehaviour
{
    [SerializeField] private float interactRadius = 1.25f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private LayerMask stationLayer = ~0;
    [SerializeField] private Transform detectionOrigin;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private int overlapCapacity = 32;

    private Collider[] overlapResults;
    private RoomTradeStation currentStation;
    private SimpleHUD simpleHud;

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
            if (inventory == null || inventory.Regolith < currentStation.RequiredRegolith)
            {
                ShowMessage("Not enough Regolith");
                currentStation.TryTrade(inventory);
                return;
            }

            bool traded = currentStation.TryTrade(inventory);
            if (!traded)
            {
                ShowMessage("Trade failed");
            }
        }
    }

    public void Configure(float radius, KeyCode key, LayerMask layer, Transform origin = null)
    {
        interactRadius = Mathf.Max(0.1f, radius);
        interactKey = key;
        stationLayer = layer;

        if (origin != null)
        {
            detectionOrigin = origin;
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
                SetPrompt("Press E to trade: " + currentStation.GetTradeText());
            }

            return;
        }

        if (currentStation != null)
        {
            currentStation.Highlight(false);
        }

        currentStation = station;

        if (currentStation != null)
        {
            currentStation.Highlight(true);
            SetPrompt("Press E to trade: " + currentStation.GetTradeText());
        }
        else
        {
            SetPrompt(string.Empty);
        }
    }

    private void ResolveReferences()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
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

    private void ShowMessage(string text)
    {
        Debug.Log(text, this);

        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.ShowMessage(text);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform originTransform = detectionOrigin != null ? detectionOrigin : transform;
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(originTransform.position, interactRadius);
    }
}
