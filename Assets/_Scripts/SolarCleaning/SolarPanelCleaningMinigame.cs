using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class SolarPanelCleaningMinigame : MonoBehaviour
{
    public Camera mainCamera;
    public Camera cleaningCamera;
    public MonoBehaviour playerControllerToDisable;
    public MonoBehaviour cameraControllerToDisable;
    public MonoBehaviour rootMotionRelayToDisable;
    public Rigidbody playerRigidbody;
    public CharacterController playerCharacterController;
    public Animator playerAnimator;
    public Renderer dirtOverlayRenderer;
    public Collider cleaningPanelCollider;
    public Transform cleaningSurfaceTransform;
    public SolarCleaningSurfaceAligner surfaceAligner;
    public SolarCleaningCameraFollower cameraFollower;
    public SolarPowerState powerState;
    public SolarCleaningUI cleaningUI;
    public RectTransform brushCursor;
    public Canvas cleaningCanvas;
    public GameObject cleanerToolPrefab;
    public Transform cleanerTool;
    public KeyCode exitKey = KeyCode.Escape;
    public bool requireMouseButton = true;
    public bool forceClickToClean = true;
    public bool autoApplyAlignmentOnBegin = false;
    public bool autoPositionCameraOnBegin = false;
    public bool useCameraFollower = true;
    public bool use3DToolCleaner = true;
    public bool freezeRigidbodyDuringCleaning = true;
    public bool disableCharacterControllerDuringCleaning = true;
    public bool disableAnimatorRootMotionDuringCleaning = true;
    public int maskResolution = 1024;
    public float brushRadiusUV = 0.055f;
    public float brushStrength = 1f;
    public float cleanPercentUpdateInterval = 0.15f;
    public float brushCursorSize = 90f;
    public float cleanerToolSurfaceOffset = 0.08f;
    public Vector3 cleanerToolLocalPositionOffset = Vector3.zero;
    public Vector3 cleanerToolLocalEulerOffset = new Vector3(25f, 0f, -20f);
    public Vector3 cleanerToolVisibleScale = Vector3.one * 24f;
    public float cleanerToolFollowSmoothTime = 0.12f;
    public float cleanerToolMotionTilt = 6f;
    public float cleanerToolRotationSmoothTime = 0.08f;
    public bool cleanerToolFaceMovementDirection = true;
    public bool cleanerToolInvertMovementDirection = true;
    public Vector2 fallbackPanelSize = new Vector2(6f, 4f);

    public bool IsCleaning { get; private set; }

    private Texture2D runtimeMask;
    private Color32[] maskPixels;
    private float nextCleanPercentUpdateTime;
    private bool paintedSinceLastSample;
    private bool playerControllerWasEnabled;
    private bool cameraControllerWasEnabled;
    private bool rootMotionRelayWasEnabled;
    private bool rigidbodyWasKinematic;
    private RigidbodyConstraints rigidbodyConstraints;
    private bool animatorApplyRootMotion;
    private bool characterControllerWasEnabled;
    private bool playerMotionFrozen;
    private Vector3 cleanerToolVelocity;
    private Quaternion cleanerToolSmoothedRotation = Quaternion.identity;
    private bool hasCleanerToolPose;

    private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
    private static Sprite brushCursorSprite;

    private void Awake()
    {
        ResolveAlignerReferences();
        ResolveCameraFollowerReferences();
        if (forceClickToClean)
        {
            requireMouseButton = true;
        }

        if (cleaningCamera != null)
        {
            cleaningCamera.enabled = false;
        }

        if (cleaningUI != null)
        {
            cleaningUI.Hide();
        }

        SetBrushCursorVisible(false);
        SetCleanerToolVisible(false);
    }

    private void Update()
    {
        if (!IsCleaning)
        {
            return;
        }

        if (Input.GetKeyDown(exitKey))
        {
            EndCleaning();
            return;
        }

        if (cameraFollower != null && useCameraFollower)
        {
            cameraFollower.TickFollow(Input.mousePosition);
        }

        UpdateCleanerVisual();

        bool canPaint = forceClickToClean ? Input.GetMouseButton(0) : (!requireMouseButton || Input.GetMouseButton(0));
        if (canPaint)
        {
            TryCleanUnderMouse();
        }
        else
        {
            if (!hasCleanerToolPose)
            {
                SetBrushCursorVisible(false);
                SetCleanerToolVisible(false);
            }
        }

        FreezePlayerMotion();

        if (Time.unscaledTime >= nextCleanPercentUpdateTime)
        {
            UpdateCleanPercent(paintedSinceLastSample);
            paintedSinceLastSample = false;
            nextCleanPercentUpdateTime = Time.unscaledTime + Mathf.Max(0.02f, cleanPercentUpdateInterval);
        }
    }

    public void BeginCleaning()
    {
        if (IsCleaning)
        {
            return;
        }

        ResolveAlignerReferences();
        bool shouldApplyAlignment = autoApplyAlignmentOnBegin
            || (surfaceAligner != null && surfaceAligner.applyAlignmentOnBeginCleaning);
        if (shouldApplyAlignment && surfaceAligner != null && surfaceAligner.panelSurfaceReference != null)
        {
            surfaceAligner.ApplyAlignment();
        }
        else if (autoPositionCameraOnBegin)
        {
            PositionCameraFromCurrentOverlay();
        }

        if (!CanEnterCleaningMode())
        {
            return;
        }

        EnsureMask();
        IsCleaning = true;

        ResolveRuntimeControlReferences();
        if (playerControllerToDisable != null)
        {
            playerControllerWasEnabled = playerControllerToDisable.enabled;
            playerControllerToDisable.enabled = false;
        }
        else
        {
            Debug.LogWarning("SolarPanelCleaningMinigame has no playerControllerToDisable assigned. Player movement may remain active during cleaning.", this);
        }

        if (cameraControllerToDisable != null)
        {
            cameraControllerWasEnabled = cameraControllerToDisable.enabled;
            cameraControllerToDisable.enabled = false;
        }

        if (rootMotionRelayToDisable != null)
        {
            rootMotionRelayWasEnabled = rootMotionRelayToDisable.enabled;
            rootMotionRelayToDisable.enabled = false;
        }

        FreezePlayerMotion();

        if (cleaningCamera != null)
        {
            cleaningCamera.enabled = true;
        }
        else
        {
            Debug.LogWarning("SolarPanelCleaningMinigame has no cleaningCamera assigned.", this);
        }

        if (mainCamera != null && mainCamera != cleaningCamera)
        {
            mainCamera.enabled = false;
        }

        ResolveCameraFollowerReferences();
        if (cameraFollower != null && useCameraFollower)
        {
            cameraFollower.EnableFollow();
        }

        if (cleaningUI != null)
        {
            cleaningUI.Show();
            cleaningUI.SetProgress(powerState != null ? powerState.CleanPercent : CalculateCleanPercent());
        }

        EnsureCleanerTool();
        bool has3DTool = use3DToolCleaner && cleanerTool != null;
        SetCleanerToolVisible(has3DTool);
        hasCleanerToolPose = false;
        cleanerToolVelocity = Vector3.zero;

        if (!has3DTool)
        {
            EnsureBrushCursor();
            SetBrushCursorVisible(true);
            UpdateBrushCursor();
        }
        else
        {
            SetBrushCursorVisible(false);
            SetCleanerToolVisible(UpdateCleanerTool());
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        nextCleanPercentUpdateTime = Time.unscaledTime;
    }

    public void EndCleaning()
    {
        if (!IsCleaning)
        {
            return;
        }

        IsCleaning = false;

        if (playerControllerToDisable != null)
        {
            playerControllerToDisable.enabled = playerControllerWasEnabled;
        }

        if (cameraControllerToDisable != null)
        {
            cameraControllerToDisable.enabled = cameraControllerWasEnabled;
        }

        if (rootMotionRelayToDisable != null)
        {
            rootMotionRelayToDisable.enabled = rootMotionRelayWasEnabled;
        }

        RestorePlayerMotion();

        if (cleaningCamera != null)
        {
            cleaningCamera.enabled = false;
        }

        if (mainCamera != null)
        {
            mainCamera.enabled = true;
        }

        if (cleaningUI != null)
        {
            cleaningUI.Hide();
        }

        if (cameraFollower != null)
        {
            cameraFollower.DisableFollow();
        }

        SetBrushCursorVisible(false);
        SetCleanerToolVisible(false);
        hasCleanerToolPose = false;
        cleanerToolVelocity = Vector3.zero;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void EnsureMask()
    {
        if (runtimeMask != null)
        {
            ApplyMaskToMaterial();
            return;
        }

        int resolution = Mathf.Clamp(maskResolution, 128, 4096);
        runtimeMask = new Texture2D(resolution, resolution, TextureFormat.R8, false, true)
        {
            name = "Runtime_Solar_Clean_Mask",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        maskPixels = new Color32[resolution * resolution];
        for (int i = 0; i < maskPixels.Length; i++)
        {
            maskPixels[i] = new Color32(255, 255, 255, 255);
        }

        runtimeMask.SetPixels32(maskPixels);
        runtimeMask.Apply(false, false);
        ApplyMaskToMaterial();
    }

    private void ApplyMaskToMaterial()
    {
        if (dirtOverlayRenderer == null)
        {
            Debug.LogWarning("SolarPanelCleaningMinigame has no dirtOverlayRenderer assigned. Dirt cannot visually erase until the overlay renderer is assigned.", this);
            return;
        }

        dirtOverlayRenderer.material.SetTexture(MaskTexId, runtimeMask);
    }

    private void TryCleanUnderMouse()
    {
        if (cleaningCamera == null)
        {
            return;
        }

        if (TryGetMouseOverlayHit(Input.mousePosition, out Vector2 uv, out _, out _))
        {
            PaintMask(uv);
        }
    }

    private bool TryGetMouseOverlayHit(Vector3 mousePosition, out Vector2 uv, out Vector3 worldPoint, out Vector3 normal)
    {
        uv = Vector2.zero;
        worldPoint = Vector3.zero;
        normal = Vector3.forward;

        if (cleaningCamera == null)
        {
            return false;
        }

        Ray ray = cleaningCamera.ScreenPointToRay(mousePosition);
        if (TryRaycastManualOverlayPlane(ray, out uv, out worldPoint))
        {
            Transform overlay = GetOverlayTransform();
            normal = overlay != null ? overlay.forward : Vector3.forward;
            return true;
        }

        if (cleaningPanelCollider != null && cleaningPanelCollider.Raycast(ray, out RaycastHit hit, 250f))
        {
            if (TryGetOverlayUv(hit.point, out uv) || TryGetFallbackUv(hit.point, out uv))
            {
                worldPoint = hit.point;
                normal = GetOverlayTransform() != null ? GetOverlayTransform().forward : hit.normal;
                return true;
            }
        }

        return false;
    }

    private bool TryRaycastManualOverlayPlane(Ray ray, out Vector2 uv)
    {
        return TryRaycastManualOverlayPlane(ray, out uv, out _);
    }

    private bool TryRaycastManualOverlayPlane(Ray ray, out Vector2 uv, out Vector3 worldPoint)
    {
        uv = Vector2.zero;
        worldPoint = Vector3.zero;

        Transform overlay = GetOverlayTransform();
        if (overlay == null)
        {
            return false;
        }

        Plane overlayPlane = new Plane(overlay.forward, overlay.position);
        if (!overlayPlane.Raycast(ray, out float distance))
        {
            return false;
        }

        worldPoint = ray.GetPoint(distance);
        return TryGetOverlayUv(worldPoint, out uv);
    }

    private bool TryGetOverlayUv(Vector3 worldPoint, out Vector2 uv)
    {
        uv = Vector2.zero;

        Transform overlay = GetOverlayTransform();
        if (overlay == null)
        {
            return false;
        }

        Vector3 localPoint = overlay.InverseTransformPoint(worldPoint);
        float u = localPoint.x + 0.5f;
        float v = localPoint.y + 0.5f;
        if (u < -0.02f || u > 1.02f || v < -0.02f || v > 1.02f)
        {
            return false;
        }

        uv = new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        return true;
    }

    private Transform GetOverlayTransform()
    {
        if (cleaningSurfaceTransform != null)
        {
            return cleaningSurfaceTransform;
        }

        return dirtOverlayRenderer != null ? dirtOverlayRenderer.transform : null;
    }

    private static bool IsUsableUv(Vector2 uv)
    {
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f && (uv.x > 0.001f || uv.y > 0.001f);
    }

    private bool TryGetFallbackUv(Vector3 worldPoint, out Vector2 uv)
    {
        uv = Vector2.zero;

        Transform surface = cleaningSurfaceTransform != null
            ? cleaningSurfaceTransform
            : cleaningPanelCollider != null ? cleaningPanelCollider.transform : null;
        if (surface == null)
        {
            return false;
        }

        Vector3 surfaceLocalPoint = surface.InverseTransformPoint(worldPoint);
        float u = surfaceLocalPoint.x + 0.5f;
        float v = surfaceLocalPoint.y + 0.5f;

        if (cleaningPanelCollider is BoxCollider box)
        {
            Vector3 colliderLocal = box.transform.InverseTransformPoint(worldPoint) - box.center;
            float panelWidth = Mathf.Max(box.size.x, 0.0001f);
            float panelHeight = Mathf.Max(box.size.y, 0.0001f);
            u = colliderLocal.x / panelWidth + 0.5f;
            v = colliderLocal.y / panelHeight + 0.5f;
        }
        else if (cleaningSurfaceTransform != null && (dirtOverlayRenderer == null || cleaningSurfaceTransform != dirtOverlayRenderer.transform))
        {
            float panelWidth = Mathf.Max(fallbackPanelSize.x, 0.0001f);
            float panelHeight = Mathf.Max(fallbackPanelSize.y, 0.0001f);
            u = surfaceLocalPoint.x / panelWidth + 0.5f;
            v = surfaceLocalPoint.y / panelHeight + 0.5f;
        }

        if (u < -0.02f || u > 1.02f || v < -0.02f || v > 1.02f)
        {
            return false;
        }

        uv = new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        return true;
    }

    private bool CanEnterCleaningMode()
    {
        bool valid = true;

        if (cleaningCamera == null)
        {
            Debug.LogError("SolarPanelCleaningMinigame cannot begin: cleaningCamera is not assigned.", this);
            valid = false;
        }

        if (dirtOverlayRenderer == null)
        {
            Debug.LogError("SolarPanelCleaningMinigame cannot begin: dirtOverlayRenderer is not assigned.", this);
            valid = false;
        }
        else
        {
            if (!dirtOverlayRenderer.gameObject.activeSelf)
            {
                dirtOverlayRenderer.gameObject.SetActive(true);
            }

            if (!dirtOverlayRenderer.enabled)
            {
                dirtOverlayRenderer.enabled = true;
            }
        }

        if (cleaningPanelCollider == null)
        {
            Debug.LogError("SolarPanelCleaningMinigame cannot begin: cleaningPanelCollider is not assigned.", this);
            valid = false;
        }

        if (powerState == null)
        {
            Debug.LogError("SolarPanelCleaningMinigame cannot begin: powerState is not assigned.", this);
            valid = false;
        }

        if (surfaceAligner != null && surfaceAligner.panelSurfaceReference == null && !surfaceAligner.useManualOverlayTransform)
        {
            Debug.LogError("SolarPanelCleaningMinigame cannot begin: SolarCleaningSurfaceAligner.panelSurfaceReference is not assigned. Assign the real solar panel glass/front transform and click Apply Solar Cleaning Alignment.", this);
            valid = false;
        }
        return valid;
    }

    private void ResolveAlignerReferences()
    {
        if (surfaceAligner == null)
        {
            surfaceAligner = GetComponent<SolarCleaningSurfaceAligner>();
        }

        if (surfaceAligner == null)
        {
            return;
        }

        if (surfaceAligner.dirtOverlay == null && dirtOverlayRenderer != null)
        {
            surfaceAligner.dirtOverlay = dirtOverlayRenderer.transform;
        }

        if (surfaceAligner.cleaningCamera == null && cleaningCamera != null)
        {
            surfaceAligner.cleaningCamera = cleaningCamera.transform;
        }

        if (surfaceAligner.cleaningPanelCollider == null && cleaningPanelCollider != null)
        {
            surfaceAligner.cleaningPanelCollider = cleaningPanelCollider;
        }

        if (surfaceAligner.panelSize.x > 0.01f && surfaceAligner.panelSize.y > 0.01f)
        {
            fallbackPanelSize = surfaceAligner.panelSize;
        }
    }

    private void ResolveCameraFollowerReferences()
    {
        if (cameraFollower == null)
        {
            cameraFollower = GetComponent<SolarCleaningCameraFollower>();
        }

        if (cameraFollower == null)
        {
            return;
        }

        if (cameraFollower.cleaningCamera == null)
        {
            cameraFollower.cleaningCamera = cleaningCamera;
        }

        if (cameraFollower.dirtOverlay == null)
        {
            cameraFollower.dirtOverlay = GetOverlayTransform();
        }

        if (cameraFollower.cleaningPanelCollider == null)
        {
            cameraFollower.cleaningPanelCollider = cleaningPanelCollider;
        }
    }

    private void ResolveRuntimeControlReferences()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return;
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = player.GetComponent<Rigidbody>();
        }

        if (playerCharacterController == null)
        {
            playerCharacterController = player.GetComponent<CharacterController>();
        }

        if (playerControllerToDisable == null)
        {
            playerControllerToDisable = player.GetComponent<ThirdPersonPlayerController>();
        }

        if (playerAnimator == null)
        {
            playerAnimator = player.GetComponentInChildren<Animator>();
        }

        if (rootMotionRelayToDisable == null)
        {
            rootMotionRelayToDisable = player.GetComponentInChildren<RootMotionRelay>();
        }

        if (cameraControllerToDisable == null && mainCamera != null)
        {
            ThirdPersonCameraFollow follow = mainCamera.GetComponent<ThirdPersonCameraFollow>();
            if (follow != null)
            {
                cameraControllerToDisable = follow;
            }
        }
    }

    private void FreezePlayerMotion()
    {
        if (!playerMotionFrozen)
        {
            if (playerRigidbody != null)
            {
                rigidbodyWasKinematic = playerRigidbody.isKinematic;
                rigidbodyConstraints = playerRigidbody.constraints;
            }

            if (playerCharacterController != null)
            {
                characterControllerWasEnabled = playerCharacterController.enabled;
            }

            if (playerAnimator != null)
            {
                animatorApplyRootMotion = playerAnimator.applyRootMotion;
            }

            playerMotionFrozen = true;
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;

            if (freezeRigidbodyDuringCleaning)
            {
                playerRigidbody.constraints = rigidbodyConstraints
                    | RigidbodyConstraints.FreezePositionX
                    | RigidbodyConstraints.FreezePositionZ
                    | RigidbodyConstraints.FreezeRotation;
            }
        }

        if (playerCharacterController != null && disableCharacterControllerDuringCleaning)
        {
            playerCharacterController.enabled = false;
        }

        if (playerAnimator != null && disableAnimatorRootMotionDuringCleaning)
        {
            playerAnimator.applyRootMotion = false;
            SetAnimatorMovementIdle();
        }
    }

    private void RestorePlayerMotion()
    {
        if (!playerMotionFrozen)
        {
            return;
        }

        if (playerCharacterController != null && disableCharacterControllerDuringCleaning)
        {
            playerCharacterController.enabled = characterControllerWasEnabled;
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.constraints = rigidbodyConstraints;
            playerRigidbody.isKinematic = rigidbodyWasKinematic;
        }

        if (playerAnimator != null && disableAnimatorRootMotionDuringCleaning)
        {
            playerAnimator.applyRootMotion = animatorApplyRootMotion;
            SetAnimatorMovementIdle();
        }

        playerMotionFrozen = false;
    }

    private void SetAnimatorMovementIdle()
    {
        if (playerAnimator == null)
        {
            return;
        }

        playerAnimator.SetFloat("Speed", 0f);
        playerAnimator.SetFloat("VerticalSpeed", 0f);
        playerAnimator.ResetTrigger("Hop");
        playerAnimator.ResetTrigger("ForwardJump");
        playerAnimator.ResetTrigger("TurnLeft");
        playerAnimator.ResetTrigger("TurnRight");
        playerAnimator.ResetTrigger("Turn180");
        playerAnimator.ResetTrigger("Stop");
    }

    private void PositionCameraFromCurrentOverlay()
    {
        Transform overlay = GetOverlayTransform();
        if (overlay == null || cleaningCamera == null)
        {
            return;
        }

        float distance = surfaceAligner != null ? Mathf.Max(0.1f, surfaceAligner.cameraDistance) : 5f;
        cleaningCamera.transform.position = overlay.position + overlay.forward * distance;
        cleaningCamera.transform.rotation = Quaternion.LookRotation(overlay.position - cleaningCamera.transform.position, overlay.up);
    }

    private void EnsureCleanerTool()
    {
        if (!use3DToolCleaner || cleanerTool != null)
        {
            return;
        }

        if (cleanerToolPrefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(cleanerToolPrefab, transform);
        instance.name = "RuntimeCleaningTool";
        cleanerTool = instance.transform;
        RemoveRuntimeCleanerColliders(instance);
    }

    private void UpdateCleanerVisual()
    {
        if (use3DToolCleaner && cleanerTool != null)
        {
            SetCleanerToolVisible(UpdateCleanerTool());
            return;
        }

        bool insideOverlay = TryGetMouseOverlayHit(Input.mousePosition, out _, out _, out _);
        SetBrushCursorVisible(insideOverlay);
        if (insideOverlay)
        {
            UpdateBrushCursor();
        }
    }

    private bool UpdateCleanerTool()
    {
        if (cleanerTool == null || cleaningCamera == null)
        {
            return false;
        }

        if (!TryGetCleanerSurfacePoint(out Vector3 surfacePoint, out Vector3 surfaceNormal, out Transform surface))
        {
            return false;
        }

        Vector3 right = surface != null ? surface.right : Vector3.right;
        Vector3 up = surface != null ? surface.up : Vector3.up;
        Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.forward;

        Vector3 targetPosition = surfacePoint
            + normal * cleanerToolSurfaceOffset
            + right * cleanerToolLocalPositionOffset.x
            + up * cleanerToolLocalPositionOffset.y
            + normal * cleanerToolLocalPositionOffset.z;

        if (!hasCleanerToolPose)
        {
            cleanerTool.position = targetPosition;
            cleanerToolVelocity = Vector3.zero;
            cleanerToolSmoothedRotation = BuildCleanerToolRotation(normal, up, Vector3.zero, right);
            cleanerTool.rotation = cleanerToolSmoothedRotation;
            hasCleanerToolPose = true;
        }
        else
        {
            Vector3 previousPosition = cleanerTool.position;
            cleanerTool.position = Vector3.SmoothDamp(
                cleanerTool.position,
                targetPosition,
                ref cleanerToolVelocity,
                Mathf.Max(0.001f, cleanerToolFollowSmoothTime));

            Vector3 motion = cleanerTool.position - previousPosition;
            Quaternion targetRotation = BuildCleanerToolRotation(normal, up, motion, right);
            float rotationLerp = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.001f, cleanerToolRotationSmoothTime));
            cleanerToolSmoothedRotation = Quaternion.Slerp(cleanerToolSmoothedRotation, targetRotation, rotationLerp);
            cleanerTool.rotation = cleanerToolSmoothedRotation;
        }

        cleanerTool.localScale = cleanerToolVisibleScale;
        return true;
    }

    private Quaternion BuildCleanerToolRotation(Vector3 normal, Vector3 up, Vector3 motion, Vector3 right)
    {
        Quaternion baseRotation = Quaternion.LookRotation(normal, up) * Quaternion.Euler(cleanerToolLocalEulerOffset);
        if (motion.sqrMagnitude < 0.000001f || cleanerToolMotionTilt <= 0f)
        {
            return baseRotation;
        }

        Vector3 planarMotion = Vector3.ProjectOnPlane(motion, normal);
        if (planarMotion.sqrMagnitude < 0.000001f)
        {
            return baseRotation;
        }

        Vector3 direction = planarMotion.normalized;
        if (cleanerToolFaceMovementDirection)
        {
            Vector3 facingDirection = cleanerToolInvertMovementDirection ? -direction : direction;
            baseRotation = Quaternion.LookRotation(normal, facingDirection) * Quaternion.Euler(cleanerToolLocalEulerOffset);
        }

        float horizontal = Mathf.Clamp(Vector3.Dot(direction, right.normalized), -1f, 1f);
        float vertical = Mathf.Clamp(Vector3.Dot(direction, up.normalized), -1f, 1f);
        Quaternion motionTilt = Quaternion.AngleAxis(-vertical * cleanerToolMotionTilt, right.normalized)
            * Quaternion.AngleAxis(horizontal * cleanerToolMotionTilt, up.normalized);
        return motionTilt * baseRotation;
    }

    private bool TryGetCleanerSurfacePoint(out Vector3 point, out Vector3 normal, out Transform surface)
    {
        point = Vector3.zero;
        normal = Vector3.forward;
        surface = GetOverlayTransform();

        if (TryGetMouseOverlayHit(Input.mousePosition, out _, out point, out normal))
        {
            return true;
        }

        return false;
    }

    private void SetCleanerToolVisible(bool visible)
    {
        if (cleanerTool != null)
        {
            cleanerTool.gameObject.SetActive(visible);
        }
    }

    private static void RemoveRuntimeCleanerColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }

    private void EnsureBrushCursor()
    {
        if (brushCursor != null)
        {
            brushCursor.sizeDelta = Vector2.one * Mathf.Max(8f, brushCursorSize);
            return;
        }

        if (cleaningCanvas == null && cleaningUI != null && cleaningUI.root != null)
        {
            cleaningCanvas = cleaningUI.root.GetComponent<Canvas>();
        }

        if (cleaningCanvas == null)
        {
            return;
        }

        GameObject cursorObject = new GameObject("BrushCursor", typeof(RectTransform), typeof(Image));
        cursorObject.transform.SetParent(cleaningCanvas.transform, false);
        brushCursor = cursorObject.GetComponent<RectTransform>();
        brushCursor.sizeDelta = Vector2.one * Mathf.Max(8f, brushCursorSize);

        Image image = cursorObject.GetComponent<Image>();
        image.sprite = GetBrushCursorSprite();
        image.color = new Color(1f, 1f, 1f, 0.72f);
        image.raycastTarget = false;
    }

    private void UpdateBrushCursor()
    {
        if (brushCursor == null)
        {
            return;
        }

        brushCursor.position = Input.mousePosition;
    }

    private void SetBrushCursorVisible(bool visible)
    {
        if (brushCursor != null)
        {
            brushCursor.gameObject.SetActive(visible);
        }
    }

    private static Sprite GetBrushCursorSprite()
    {
        if (brushCursorSprite != null)
        {
            return brushCursorSprite;
        }

        const int size = 96;
        const float radius = size * 0.42f;
        const float ringWidth = 5f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime_Solar_Brush_Cursor",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 ring = new Color32(255, 255, 255, 210);
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = Mathf.Abs(distance - radius) <= ringWidth ? ring : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        brushCursorSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return brushCursorSprite;
    }

    [ContextMenu("Lock Current Overlay As Manual")]
    public void LockCurrentOverlayAsManual()
    {
        ResolveAlignerReferences();
        if (surfaceAligner != null)
        {
            surfaceAligner.useManualOverlayTransform = true;
            surfaceAligner.applyAlignmentOnBeginCleaning = false;
            surfaceAligner.applyAlignmentOnRepairMenu = false;
        }

        autoApplyAlignmentOnBegin = false;
        autoPositionCameraOnBegin = false;

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        if (surfaceAligner != null)
        {
            EditorUtility.SetDirty(surfaceAligner);
        }

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private void PaintMask(Vector2 uv)
    {
        EnsureMask();

        int width = runtimeMask.width;
        int height = runtimeMask.height;
        int centerX = Mathf.RoundToInt(uv.x * (width - 1));
        int centerY = Mathf.RoundToInt(uv.y * (height - 1));
        int radius = Mathf.Max(1, Mathf.RoundToInt(brushRadiusUV * width));
        int radiusSq = radius * radius;
        byte eraseAmount = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * brushStrength), 1, 255);

        int xMin = Mathf.Max(0, centerX - radius);
        int xMax = Mathf.Min(width - 1, centerX + radius);
        int yMin = Mathf.Max(0, centerY - radius);
        int yMax = Mathf.Min(height - 1, centerY + radius);
        bool changed = false;

        for (int y = yMin; y <= yMax; y++)
        {
            int dy = y - centerY;
            for (int x = xMin; x <= xMax; x++)
            {
                int dx = x - centerX;
                int distanceSq = dx * dx + dy * dy;
                if (distanceSq > radiusSq)
                {
                    continue;
                }

                float falloff = 1f - (Mathf.Sqrt(distanceSq) / radius);
                int index = y * width + x;
                byte current = maskPixels[index].r;
                byte next = (byte)Mathf.Max(0, current - Mathf.RoundToInt(eraseAmount * Mathf.Clamp01(falloff)));
                if (next == current)
                {
                    continue;
                }

                maskPixels[index] = new Color32(next, next, next, 255);
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        runtimeMask.SetPixels32(maskPixels);
        runtimeMask.Apply(false, false);
        paintedSinceLastSample = true;
    }

    private void UpdateCleanPercent(bool forceSample)
    {
        if (!forceSample && powerState != null)
        {
            if (cleaningUI != null)
            {
                cleaningUI.SetProgress(powerState.CleanPercent);
            }

            return;
        }

        float cleanPercent = CalculateCleanPercent();
        if (powerState != null)
        {
            powerState.SetCleanPercent(cleanPercent);
            cleanPercent = powerState.CleanPercent;
        }

        if (cleaningUI != null)
        {
            cleaningUI.SetProgress(cleanPercent);
        }
    }

    private float CalculateCleanPercent()
    {
        EnsureMask();

        long erasedAmount = 0;
        for (int i = 0; i < maskPixels.Length; i++)
        {
            erasedAmount += 255 - maskPixels[i].r;
        }

        return erasedAmount / (255f * maskPixels.Length);
    }

}
