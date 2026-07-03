using System.Collections;
using UnityEngine;

public class MicrometeoriteOpeningCinematic : MonoBehaviour
{
    [Header("Scene References")]
    public Transform micrometeorite;
    public Transform asteroidStartPoint;
    public Transform asteroidImpactPoint;
    public Camera asteroidFollowCamera;
    public Camera impactCamera;
    public Camera mainGameplayCamera;
    public GameObject impactFX;
    public AudioSource impactAudio;
    public HabitatDamageVisualState habitatDamageVisualState;

    [Header("Timing")]
    public float startDelay = 1.5f;
    public float travelDuration = 2.0f;
    public float cameraSwitchDistance = 5f;
    public float postImpactHoldTime = 1.5f;
    public bool playOnStart = true;
    public KeyCode debugKey = KeyCode.I;

    [Header("Camera Motion")]
    public Vector3 followCameraLocalOffset = new Vector3(2.3f, 0.65f, -2.3f);
    public float impactShakeDuration = 0.28f;
    public float impactShakeMagnitude = 0.13f;

    [Header("Asteroid Motion")]
    public float xAxisSpinDegreesPerSecond = 35f;

    private MonoBehaviour playerControllerToDisable;
    private MonoBehaviour gameplayCameraControllerToDisable;
    private bool playerControllerWasEnabled;
    private bool cameraControllerWasEnabled;
    private bool mainCameraWasEnabled;
    private bool isRunning;

    private void Start()
    {
        ResolveRuntimeReferences();
        SetInitialState();

        if (playOnStart)
        {
            StartCoroutine(PlayAfterDelay());
        }
    }

    private void Update()
    {
        if (!isRunning && Input.GetKeyDown(debugKey))
        {
            StartCoroutine(PlaySequence());
        }
    }

    private IEnumerator PlayAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, startDelay));

        if (!isRunning)
        {
            yield return PlaySequence();
        }
    }

    public IEnumerator PlaySequence()
    {
        if (isRunning)
        {
            yield break;
        }

        ResolveRuntimeReferences();

        if (!CanPlay())
        {
            Debug.LogWarning("Micrometeorite cinematic cannot play: missing micrometeorite, start point, impact point, or cameras.", this);
            yield break;
        }

        isRunning = true;
        Debug.Log("Micrometeorite cinematic started.");

        DisableGameplayInputAndCamera();
        PrepareMeteorite();
        ActivateFollowCamera();

        Vector3 start = asteroidStartPoint.position;
        Vector3 end = asteroidImpactPoint.position;
        Vector3 travelDirection = (end - start).sqrMagnitude > 0.01f ? (end - start).normalized : Vector3.forward;
        bool switchedCamera = false;
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, travelDuration);

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            micrometeorite.position = Vector3.Lerp(start, end, eased);
            if (Mathf.Abs(xAxisSpinDegreesPerSecond) > 0.01f)
            {
                micrometeorite.Rotate(Vector3.right * xAxisSpinDegreesPerSecond * Time.deltaTime, Space.Self);
            }

            if (!switchedCamera)
            {
                UpdateFollowCamera(travelDirection);
                float distanceToImpact = Vector3.Distance(micrometeorite.position, end);
                if (t >= 0.65f || distanceToImpact <= cameraSwitchDistance)
                {
                    switchedCamera = true;
                    ActivateImpactCamera();
                    Debug.Log("Micrometeorite cinematic camera switched to impact view.");
                }
            }
            else
            {
                UpdateImpactCameraLook();
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        micrometeorite.position = end;
        yield return ImpactRoutine();

        yield return new WaitForSeconds(Mathf.Max(0f, postImpactHoldTime));

        EndCinematic();
        Debug.Log("Opening cinematic complete. Objective: Restore power.");
    }

    private IEnumerator ImpactRoutine()
    {
        if (micrometeorite != null)
        {
            micrometeorite.gameObject.SetActive(false);
        }

        if (impactFX != null)
        {
            impactFX.transform.position = asteroidImpactPoint.position;
            impactFX.SetActive(true);
            ParticleSystem particles = impactFX.GetComponentInChildren<ParticleSystem>();
            if (particles != null)
            {
                particles.Play(true);
            }
        }

        if (impactAudio != null)
        {
            impactAudio.Play();
        }

        if (habitatDamageVisualState != null)
        {
            habitatDamageVisualState.ShowDamaged();
        }
        else
        {
            GameObject crack = GameObject.Find("Crack_Impact_01");
            GameObject patch = GameObject.Find("ConcretePatch_01");
            if (crack != null)
            {
                crack.SetActive(true);
            }

            if (patch != null)
            {
                patch.SetActive(false);
            }
        }

        Debug.Log("Micrometeorite impact: habitat damaged.");

        yield return ShakeImpactCamera();
    }

    private IEnumerator ShakeImpactCamera()
    {
        if (impactCamera == null || impactShakeDuration <= 0f || impactShakeMagnitude <= 0f)
        {
            yield break;
        }

        Transform cameraTransform = impactCamera.transform;
        Vector3 basePosition = cameraTransform.position;
        float elapsed = 0f;

        while (elapsed < impactShakeDuration)
        {
            float fade = 1f - Mathf.Clamp01(elapsed / impactShakeDuration);
            Vector3 shake = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-0.35f, 0.35f)) * impactShakeMagnitude * fade;
            cameraTransform.position = basePosition + shake;
            UpdateImpactCameraLook();
            elapsed += Time.deltaTime;
            yield return null;
        }

        cameraTransform.position = basePosition;
        UpdateImpactCameraLook();
    }

    private void SetInitialState()
    {
        if (habitatDamageVisualState != null)
        {
            habitatDamageVisualState.ShowNormal();
        }

        if (impactFX != null)
        {
            impactFX.SetActive(false);
        }

        if (micrometeorite != null)
        {
            micrometeorite.gameObject.SetActive(false);
        }

        SetCameraEnabled(asteroidFollowCamera, false);
        SetCameraEnabled(impactCamera, false);
        if (mainGameplayCamera != null)
        {
            mainGameplayCamera.enabled = true;
        }
    }

    private void PrepareMeteorite()
    {
        Vector3 travelDirection = (asteroidImpactPoint.position - asteroidStartPoint.position).normalized;
        micrometeorite.position = asteroidStartPoint.position;
        micrometeorite.rotation = travelDirection.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(travelDirection, Vector3.up)
            : asteroidStartPoint.rotation;
        micrometeorite.gameObject.SetActive(true);
    }

    private void ActivateFollowCamera()
    {
        SetCameraEnabled(asteroidFollowCamera, true);
        SetCameraEnabled(impactCamera, false);
        SetCameraEnabled(mainGameplayCamera, false);

        Vector3 direction = (asteroidImpactPoint.position - asteroidStartPoint.position).normalized;
        UpdateFollowCamera(direction);
    }

    private void ActivateImpactCamera()
    {
        SetCameraEnabled(asteroidFollowCamera, false);
        SetCameraEnabled(impactCamera, true);
        SetCameraEnabled(mainGameplayCamera, false);
        UpdateImpactCameraLook();
    }

    private void UpdateFollowCamera(Vector3 travelDirection)
    {
        if (asteroidFollowCamera == null || micrometeorite == null)
        {
            return;
        }

        Vector3 forward = travelDirection.sqrMagnitude > 0.01f ? travelDirection.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        if (right.sqrMagnitude < 0.01f)
        {
            right = Vector3.right;
        }

        Vector3 offset =
            right * followCameraLocalOffset.x +
            Vector3.up * followCameraLocalOffset.y -
            forward * Mathf.Abs(followCameraLocalOffset.z);

        Vector3 visualCenter = GetMeteoriteVisualCenter();
        asteroidFollowCamera.transform.position = visualCenter + offset;
        asteroidFollowCamera.transform.LookAt(visualCenter + forward * 0.2f);
    }

    private void UpdateImpactCameraLook()
    {
        if (impactCamera == null || asteroidImpactPoint == null)
        {
            return;
        }

        impactCamera.transform.LookAt(asteroidImpactPoint.position);
    }

    private Vector3 GetMeteoriteVisualCenter()
    {
        if (micrometeorite == null)
        {
            return Vector3.zero;
        }

        Renderer renderer = micrometeorite.GetComponentInChildren<Renderer>(true);
        return renderer != null ? renderer.bounds.center : micrometeorite.position;
    }

    private void DisableGameplayInputAndCamera()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerControllerToDisable = player.GetComponent<ThirdPersonPlayerController>();
            if (playerControllerToDisable != null)
            {
                playerControllerWasEnabled = playerControllerToDisable.enabled;
                playerControllerToDisable.enabled = false;
            }
        }

        if (mainGameplayCamera != null)
        {
            mainCameraWasEnabled = mainGameplayCamera.enabled;
            gameplayCameraControllerToDisable = mainGameplayCamera.GetComponent<ThirdPersonCameraFollow>();
            if (gameplayCameraControllerToDisable != null)
            {
                cameraControllerWasEnabled = gameplayCameraControllerToDisable.enabled;
                gameplayCameraControllerToDisable.enabled = false;
            }
        }
    }

    private void EndCinematic()
    {
        SetCameraEnabled(asteroidFollowCamera, false);
        SetCameraEnabled(impactCamera, false);

        if (mainGameplayCamera != null)
        {
            mainGameplayCamera.enabled = mainCameraWasEnabled || !mainGameplayCamera.enabled;
        }

        if (playerControllerToDisable != null)
        {
            playerControllerToDisable.enabled = playerControllerWasEnabled;
        }

        if (gameplayCameraControllerToDisable != null)
        {
            gameplayCameraControllerToDisable.enabled = cameraControllerWasEnabled;
        }

        isRunning = false;
    }

    private bool CanPlay()
    {
        return micrometeorite != null
            && asteroidStartPoint != null
            && asteroidImpactPoint != null
            && asteroidFollowCamera != null
            && impactCamera != null;
    }

    private void ResolveRuntimeReferences()
    {
        if (mainGameplayCamera == null)
        {
            mainGameplayCamera = Camera.main;
        }

        if (habitatDamageVisualState == null)
        {
            habitatDamageVisualState = FindAnyObjectByType<HabitatDamageVisualState>();
        }
    }

    private static void SetCameraEnabled(Camera camera, bool enabled)
    {
        if (camera != null)
        {
            camera.enabled = enabled;
        }
    }
}
