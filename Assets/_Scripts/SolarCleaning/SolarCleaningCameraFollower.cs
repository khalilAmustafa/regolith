using UnityEngine;

public class SolarCleaningCameraFollower : MonoBehaviour
{
    public Camera cleaningCamera;
    public Transform dirtOverlay;
    public Collider cleaningPanelCollider;
    public float cameraDistance = 1.2f;
    public float orthographicSize = 1.25f;
    public bool useOrthographicCamera = true;
    public float followSmoothTime = 0.18f;
    public float verticalFollowSmoothMultiplier = 2.4f;
    public float mouseInfluence = 0.65f;
    public float borderPadding = -0.08f;
    public bool invertNormal = false;
    public bool followOnlyWhenMouseOverOverlay = true;
    public bool showDebugGizmos = true;

    private Vector3 currentVelocity;
    private Vector2 currentFocusLocal;
    private Vector2 targetFocusLocal;
    private bool isFollowing;

    public void EnableFollow()
    {
        if (cleaningCamera == null || dirtOverlay == null)
        {
            Debug.LogWarning("SolarCleaningCameraFollower needs cleaningCamera and dirtOverlay assigned.", this);
            return;
        }

        isFollowing = true;
        currentVelocity = Vector3.zero;
        currentFocusLocal = Vector2.zero;
        targetFocusLocal = Vector2.zero;

        if (useOrthographicCamera)
        {
            cleaningCamera.orthographic = true;
            cleaningCamera.orthographicSize = orthographicSize;
        }

        CenterCameraOnOverlay();
    }

    public void DisableFollow()
    {
        isFollowing = false;
        currentVelocity = Vector3.zero;
    }

    public void TickFollow(Vector3 mousePosition)
    {
        if (!isFollowing || cleaningCamera == null || dirtOverlay == null)
        {
            return;
        }

        if (TryGetMouseLocalFocus(mousePosition, out Vector2 localFocus))
        {
            targetFocusLocal = ClampFocus(localFocus * Mathf.Max(0f, mouseInfluence));
        }
        else if (!followOnlyWhenMouseOverOverlay)
        {
            Vector3 viewport = cleaningCamera.ScreenToViewportPoint(mousePosition);
            Vector2 viewportOffset = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f) * Mathf.Max(0f, mouseInfluence);
            targetFocusLocal = ClampFocus(viewportOffset);
        }

        float smoothX = Mathf.Max(0.001f, followSmoothTime);
        float smoothY = Mathf.Max(0.001f, followSmoothTime * Mathf.Max(1f, verticalFollowSmoothMultiplier));
        float nextX = Mathf.SmoothDamp(currentFocusLocal.x, targetFocusLocal.x, ref currentVelocity.x, smoothX);
        float nextY = Mathf.SmoothDamp(currentFocusLocal.y, targetFocusLocal.y, ref currentVelocity.y, smoothY);
        currentFocusLocal = new Vector2(nextX, nextY);
        ApplyCameraTransform(currentFocusLocal);
    }

    [ContextMenu("Center Camera On Overlay")]
    public void CenterCameraOnOverlay()
    {
        currentFocusLocal = Vector2.zero;
        targetFocusLocal = Vector2.zero;
        currentVelocity = Vector3.zero;
        ApplyCameraTransform(Vector2.zero);
    }

    [ContextMenu("Toggle Invert Normal And Center")]
    public void ToggleInvertNormalAndCenter()
    {
        invertNormal = !invertNormal;
        CenterCameraOnOverlay();
    }

    private bool TryGetMouseLocalFocus(Vector3 mousePosition, out Vector2 localFocus)
    {
        localFocus = Vector2.zero;
        Ray ray = cleaningCamera.ScreenPointToRay(mousePosition);

        Plane overlayPlane = new Plane(dirtOverlay.forward, dirtOverlay.position);
        if (overlayPlane.Raycast(ray, out float distance)
            && TryWorldPointToOverlayLocal(ray.GetPoint(distance), out localFocus))
        {
            return true;
        }

        if (cleaningPanelCollider != null && cleaningPanelCollider.Raycast(ray, out RaycastHit hit, 250f))
        {
            return TryWorldPointToOverlayLocal(hit.point, out localFocus);
        }

        return false;
    }

    private bool TryWorldPointToOverlayLocal(Vector3 worldPoint, out Vector2 localFocus)
    {
        Vector3 local = dirtOverlay.InverseTransformPoint(worldPoint);
        localFocus = new Vector2(local.x, local.y);
        return local.x >= 0f - 0.5f
            && local.x <= 1f - 0.5f
            && local.y >= 0f - 0.5f
            && local.y <= 1f - 0.5f;
    }

    private Vector2 ClampFocus(Vector2 local)
    {
        if (cleaningCamera == null || dirtOverlay == null)
        {
            return Vector2.zero;
        }

        float halfViewHeightWorld = useOrthographicCamera ? cleaningCamera.orthographicSize : orthographicSize;
        float halfViewWidthWorld = halfViewHeightWorld * Mathf.Max(0.01f, cleaningCamera.aspect);
        float overlayWorldWidth = Mathf.Max(0.0001f, Mathf.Abs(dirtOverlay.lossyScale.x));
        float overlayWorldHeight = Mathf.Max(0.0001f, Mathf.Abs(dirtOverlay.lossyScale.y));
        float halfViewLocalX = halfViewWidthWorld / overlayWorldWidth;
        float halfViewLocalY = halfViewHeightWorld / overlayWorldHeight;

        float minX = -0.5f + halfViewLocalX + borderPadding;
        float maxX = 0.5f - halfViewLocalX - borderPadding;
        float minY = -0.5f + halfViewLocalY + borderPadding;
        float maxY = 0.5f - halfViewLocalY - borderPadding;

        if (minX > maxX)
        {
            minX = 0f;
            maxX = 0f;
        }

        if (minY > maxY)
        {
            minY = 0f;
            maxY = 0f;
        }

        return new Vector2(
            Mathf.Clamp(local.x, minX, maxX),
            Mathf.Clamp(local.y, minY, maxY));
    }

    private void ApplyCameraTransform(Vector2 focusLocal)
    {
        if (cleaningCamera == null || dirtOverlay == null)
        {
            return;
        }

        Vector3 normal = invertNormal ? -dirtOverlay.forward : dirtOverlay.forward;
        Vector3 focusWorld = dirtOverlay.TransformPoint(new Vector3(focusLocal.x, focusLocal.y, 0f));
        cleaningCamera.transform.position = focusWorld + normal.normalized * Mathf.Max(0.01f, cameraDistance);
        cleaningCamera.transform.rotation = Quaternion.LookRotation(-normal.normalized, dirtOverlay.up);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || dirtOverlay == null)
        {
            return;
        }

        Vector3 center = dirtOverlay.position;
        Vector3 right = dirtOverlay.right * (dirtOverlay.lossyScale.x * 0.5f);
        Vector3 up = dirtOverlay.up * (dirtOverlay.lossyScale.y * 0.5f);
        Vector3 normal = invertNormal ? -dirtOverlay.forward : dirtOverlay.forward;

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(center - right - up, center + right - up);
        Gizmos.DrawLine(center + right - up, center + right + up);
        Gizmos.DrawLine(center + right + up, center - right + up);
        Gizmos.DrawLine(center - right + up, center - right - up);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center, center + normal.normalized * Mathf.Max(0.2f, cameraDistance));
    }
}
