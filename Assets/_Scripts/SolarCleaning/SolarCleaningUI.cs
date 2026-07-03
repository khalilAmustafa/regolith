using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SolarCleaningUI : MonoBehaviour
{
    public GameObject root;
    public TMP_Text cleaningPercentLabel;
    public Text cleaningPercentFallbackLabel;
    public Image progressFill;
    public TMP_Text instructionLabel;
    public Text instructionFallbackLabel;
    public TMP_Text exitLabel;
    public Text exitFallbackLabel;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }
    }

    public void Show()
    {
        if (root != null)
        {
            root.SetActive(true);
        }

        SetStaticText();
    }

    public void Hide()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    public void SetProgress(float normalized)
    {
        float clamped = Mathf.Clamp01(normalized);
        string label = "Cleaning " + Mathf.RoundToInt(clamped * 100f) + "%";

        SetText(cleaningPercentLabel, cleaningPercentFallbackLabel, label);

        if (progressFill != null)
        {
            progressFill.fillAmount = clamped;
        }
    }

    private void SetStaticText()
    {
        SetText(instructionLabel, instructionFallbackLabel, "Hold click to clean");
        SetText(exitLabel, exitFallbackLabel, "ESC exit");
    }

    private static void SetText(TMP_Text tmp, Text fallback, string value)
    {
        if (tmp != null)
        {
            tmp.text = value;
        }

        if (fallback != null)
        {
            fallback.text = value;
        }
    }
}
