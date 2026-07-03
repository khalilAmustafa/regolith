using UnityEngine;

public class HabitatDamageVisualState : MonoBehaviour
{
    public GameObject crackObject;
    public GameObject concretePatchObject;

    public void ShowNormal()
    {
        SetActive(crackObject, false);
        SetActive(concretePatchObject, false);
    }

    public void ShowDamaged()
    {
        SetActive(crackObject, true);
        SetActive(concretePatchObject, false);
    }

    public void ShowPatched()
    {
        SetActive(crackObject, false);
        SetActive(concretePatchObject, true);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}
