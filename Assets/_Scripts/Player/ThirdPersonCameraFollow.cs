using UnityEngine;

[DisallowMultipleComponent]
public class ThirdPersonCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool autoFindPlayer = true;

    [Header("Framing")]
    [SerializeField] private float distance = 8f;
    [SerializeField] private float minDistance = 4.5f;
    [SerializeField] private float maxDistance = 12f;
    [SerializeField] private float height = 2.2f;
    [SerializeField] private float lookAtHeight = 1.35f;
    [SerializeField] private float shoulderOffset = 0.35f;
    [SerializeField] private bool enforceComfortDistanceOnStart = true;
    [SerializeField] private float comfortStartDistance = 8f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2.4f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -25f;
    [SerializeField] private float maxPitch = 65f;

    [Header("Zoom")]
    [SerializeField] private bool allowZoom = true;
    [SerializeField] private float zoomSpeed = 1.5f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.07f;
    [SerializeField] private float rotationSmoothSpeed = 18f;

    [Header("Collision")]
    [SerializeField] private bool useCameraCollision = true;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionBuffer = 0.2f;
    [SerializeField] private LayerMask collisionMask = ~0;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = true;
    [SerializeField] private KeyCode unlockCursorKey = KeyCode.Escape;

    private float yaw;
    private float pitch = 18f;
    private Vector3 positionVelocity;
    private readonly RaycastHit[] hits = new RaycastHit[12];

    private void Start()
    {
        if (target == null && autoFindPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player == null)
            {
                player = GameObject.Find("Player");
            }

            if (player != null)
            {
                target = player.transform;
            }
        }

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = NormalizeAngle(angles.x);

        minDistance = Mathf.Max(0.5f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);

        if (enforceComfortDistanceOnStart && distance < comfortStartDistance)
        {
            distance = comfortStartDistance;
        }

        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        if (lockCursorOnStart)
        {
            LockCursor();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        HandleCursor();
        HandleInput();
        UpdateCamera();
    }

    private void HandleCursor()
    {
        if (Input.GetKeyDown(unlockCursorKey))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }
    }

    private void HandleInput()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;

            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch += invertY ? mouseY : -mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        if (allowZoom)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            if (Mathf.Abs(scroll) > 0.001f)
            {
                distance -= scroll * zoomSpeed;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }
        }
    }

    private void UpdateCamera()
    {
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 lookPoint = target.position + Vector3.up * lookAtHeight;
        Vector3 shoulder = orbitRotation * Vector3.right * shoulderOffset;

        Vector3 desiredPosition =
            target.position +
            Vector3.up * height +
            shoulder -
            orbitRotation * Vector3.forward * distance;

        if (useCameraCollision)
        {
            desiredPosition = ResolveCameraCollision(lookPoint, desiredPosition);
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime
        );

        Vector3 lookDirection = lookPoint - transform.position;

        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationSmoothSpeed * Time.deltaTime
            );
        }
    }

    private Vector3 ResolveCameraCollision(Vector3 lookPoint, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - lookPoint;
        float desiredDistance = direction.magnitude;

        if (desiredDistance <= 0.01f)
        {
            return desiredPosition;
        }

        direction /= desiredDistance;

        int hitCount = Physics.SphereCastNonAlloc(
            lookPoint,
            collisionRadius,
            direction,
            hits,
            desiredDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore
        );

        float closestDistance = desiredDistance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
            {
                continue;
            }

            if (target != null && hit.transform.root == target.root)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
            }
        }

        if (closestDistance < desiredDistance)
        {
            float safeDistance = Mathf.Max(closestDistance - collisionBuffer, minDistance * 0.35f);
            return lookPoint + direction * safeDistance;
        }

        return desiredPosition;
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
    }
}
