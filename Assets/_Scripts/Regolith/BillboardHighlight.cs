using UnityEngine;

public class BillboardHighlight : MonoBehaviour
{
    public Transform targetCamera;
    public bool yawOnly = false;
    public bool faceAwayFromCamera = false;
    public Vector3 localOffset = new Vector3(0f, 0.05f, 0f);
    public Vector3 baseLocalPosition = Vector3.zero;
    public Vector3 baseLocalScale = Vector3.one;
    public bool usePulse = true;
    public float pulseAmount = 0.025f;
    public float pulseSpeed = 2.4f;

    private void LateUpdate()
    {
        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }

        transform.localPosition = baseLocalPosition + localOffset;

        if (targetCamera != null)
        {
            Vector3 direction = targetCamera.position - transform.position;
            if (faceAwayFromCamera)
            {
                direction = -direction;
            }

            if (yawOnly)
            {
                direction.y = 0f;
            }

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        float pulse = usePulse ? 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount : 1f;
        transform.localScale = baseLocalScale * pulse;
    }
}
