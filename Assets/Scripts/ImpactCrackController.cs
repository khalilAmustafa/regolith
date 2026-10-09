using UnityEngine;

public class ImpactCrackController : MonoBehaviour
{
    // Raised after the crack becomes visible so the repair state can flip to Damaged
    // without the cinematic knowing about the repair system.
    public event System.Action CrackShown;

    public Transform impactPoint;
    public GameObject crackPrefab;
    public GameObject crackInstance;
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffsetEuler = Vector3.zero;
    public Vector3 crackScale = new Vector3(30f, 30f, 1f);
    public bool parentToImpactPoint = true;

    private void Start()
    {
        HideCrack();
    }

    public void HideCrack()
    {
        if (crackInstance != null)
        {
            crackInstance.SetActive(false);
        }
    }

    public void ShowCrack()
    {
        Transform targetPoint = impactPoint != null ? impactPoint : transform;

        if (crackInstance == null)
        {
            if (crackPrefab == null)
            {
                Debug.LogWarning("Impact crack cannot show because no crack prefab is assigned.", this);
                return;
            }

            Transform parent = parentToImpactPoint ? targetPoint : null;
            crackInstance = Instantiate(crackPrefab, parent);
            crackInstance.name = "ImpactCrack_01";
        }
        else if (parentToImpactPoint && crackInstance.transform.parent != targetPoint)
        {
            crackInstance.transform.SetParent(targetPoint, true);
        }
        else if (!parentToImpactPoint && crackInstance.transform.parent != null)
        {
            crackInstance.transform.SetParent(null, true);
        }

        Quaternion rotation = targetPoint.rotation * Quaternion.Euler(rotationOffsetEuler);
        // Offset is applied in the impact point's local space so a small Z value
        // lifts the visual off the panel surface regardless of panel orientation.
        crackInstance.transform.SetPositionAndRotation(targetPoint.position + rotation * positionOffset, rotation);
        crackInstance.transform.localScale = crackScale;
        crackInstance.SetActive(true);
        CrackShown?.Invoke();
    }
}
