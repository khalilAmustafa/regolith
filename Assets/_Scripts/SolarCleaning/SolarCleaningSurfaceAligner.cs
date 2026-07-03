using UnityEngine;

public class SolarCleaningSurfaceAligner : MonoBehaviour
{
    public Transform panelSurfaceReference;
    public Transform dirtOverlay;
    public Transform cleaningCamera;
    public Collider cleaningPanelCollider;
    public Vector2 panelSize = new Vector2(6f, 4f);
    public float overlayOffset = 0.025f;
    public float cameraDistance = 5f;
    public bool useReferenceForward = true;
    public bool flipNormal;
    public bool useManualOverlayTransform = true;
    public bool applyAlignmentOnBeginCleaning = false;
    public bool applyAlignmentOnRepairMenu = false;

    [ContextMenu("Apply Solar Cleaning Alignment")]
    public void ApplyAlignment()
    {
        if (panelSurfaceReference == null)
        {
            Debug.LogWarning("SolarCleaningSurfaceAligner needs panelSurfaceReference assigned to the real solar panel glass/front transform.", this);
            return;
        }

        Vector3 normal = ResolveNormal();
        Quaternion surfaceRotation = panelSurfaceReference.rotation;
        Vector3 surfacePosition = panelSurfaceReference.position;
        Vector2 safePanelSize = new Vector2(Mathf.Max(0.01f, panelSize.x), Mathf.Max(0.01f, panelSize.y));

        if (dirtOverlay != null)
        {
            if (!dirtOverlay.gameObject.activeSelf)
            {
                dirtOverlay.gameObject.SetActive(true);
            }

            if (useManualOverlayTransform)
            {
                Debug.Log("Manual overlay transform mode active; not moving DirtOverlay.", this);
            }
            else
            {
                // DirtOverlay is expected to be a Unity Quad: local X/Y are the cleaning UV axes, local Z is the panel normal.
                dirtOverlay.SetPositionAndRotation(surfacePosition + normal * overlayOffset, surfaceRotation);
                dirtOverlay.localScale = new Vector3(safePanelSize.x, safePanelSize.y, 1f);
            }

            dirtOverlay.gameObject.layer = 0;

            Renderer renderer = dirtOverlay.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        if (cleaningPanelCollider != null)
        {
            if (!cleaningPanelCollider.gameObject.activeSelf)
            {
                cleaningPanelCollider.gameObject.SetActive(true);
            }

            cleaningPanelCollider.enabled = true;
            Transform colliderTransform = cleaningPanelCollider.transform;
            colliderTransform.SetPositionAndRotation(surfacePosition + normal * overlayOffset, surfaceRotation);
            colliderTransform.localScale = Vector3.one;

            BoxCollider box = cleaningPanelCollider as BoxCollider;
            if (box != null)
            {
                box.center = Vector3.zero;
                box.size = new Vector3(safePanelSize.x, safePanelSize.y, 0.08f);
                box.isTrigger = true;
            }
        }

        if (cleaningCamera != null)
        {
            if (!cleaningCamera.gameObject.activeSelf)
            {
                cleaningCamera.gameObject.SetActive(true);
            }

            Camera cameraComponent = cleaningCamera.GetComponent<Camera>();
            cleaningCamera.position = surfacePosition + normal * Mathf.Max(0.1f, cameraDistance);
            cleaningCamera.rotation = Quaternion.LookRotation(surfacePosition - cleaningCamera.position, panelSurfaceReference.up);

            if (cameraComponent != null)
            {
                cameraComponent.fieldOfView = 40f;
                cameraComponent.nearClipPlane = 0.03f;
                cameraComponent.farClipPlane = 100f;
                cameraComponent.enabled = false;
            }
        }

        SolarPanelCleaningMinigame minigame = GetComponent<SolarPanelCleaningMinigame>();
        if (minigame != null)
        {
            minigame.fallbackPanelSize = safePanelSize;
        }
    }

    [ContextMenu("Flip Normal And Apply")]
    public void FlipNormalAndApply()
    {
        flipNormal = !flipNormal;
        ApplyAlignment();
    }

    [ContextMenu("Disable Auto Alignment")]
    public void DisableAutoAlignment()
    {
        useManualOverlayTransform = true;
        applyAlignmentOnBeginCleaning = false;
        applyAlignmentOnRepairMenu = false;
    }

    private void OnDrawGizmos()
    {
        if (panelSurfaceReference == null)
        {
            return;
        }

        Vector3 normal = ResolveNormal();
        Vector3 center = panelSurfaceReference.position;
        Vector3 right = panelSurfaceReference.right * (panelSize.x * 0.5f);
        Vector3 up = panelSurfaceReference.up * (panelSize.y * 0.5f);

        Vector3 a = center - right - up;
        Vector3 b = center + right - up;
        Vector3 c = center + right + up;
        Vector3 d = center - right + up;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(center, center + normal * Mathf.Max(0.25f, Mathf.Min(panelSize.x, panelSize.y) * 0.35f));

        Gizmos.color = Color.yellow;
        Vector3 cameraPosition = center + normal * Mathf.Max(0.1f, cameraDistance);
        Gizmos.DrawLine(cameraPosition, center);
        Gizmos.DrawWireSphere(cameraPosition, 0.15f);
    }

    private Vector3 ResolveNormal()
    {
        Vector3 normal = panelSurfaceReference != null ? panelSurfaceReference.forward : transform.forward;
        if (flipNormal)
        {
            normal = -normal;
        }

        return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
    }
}
