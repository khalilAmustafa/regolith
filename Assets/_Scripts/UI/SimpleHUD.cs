using UnityEngine;
using UnityEngine.UI;

public class SimpleHUD : MonoBehaviour
{
    [SerializeField] private Text regolithText;
    [SerializeField] private Text oxygenText;
    [SerializeField] private Text concreteText;
    [SerializeField] private Text promptText;
    [SerializeField] private Text objectiveText;
    [SerializeField] private Text winText;

    private void Awake()
    {
        SetInventory(0, 0);
        SetPrompt(string.Empty);

        if (winText != null)
        {
            winText.gameObject.SetActive(false);
        }
    }

    public void SetInventory(int regolith, int oxygen)
    {
        SetInventory(regolith, oxygen, 0);
    }

    public void SetInventory(int regolith, int oxygen, int concrete)
    {
        if (regolithText != null)
        {
            regolithText.text = $"Regolith: {regolith}";
        }

        if (oxygenText != null)
        {
            oxygenText.text = $"Oxygen: {oxygen}";
        }

        if (concreteText != null)
        {
            concreteText.text = $"Concrete: {concrete}";
        }
    }

    public void SetPrompt(string text)
    {
        if (promptText != null)
        {
            promptText.text = text;
            promptText.enabled = !string.IsNullOrWhiteSpace(text);
        }
    }

    public void SetObjective(string text)
    {
        if (objectiveText != null)
        {
            objectiveText.text = text;
        }
    }

    public void ShowWin(string text)
    {
        if (winText != null)
        {
            winText.text = text;
            winText.gameObject.SetActive(true);
        }
    }

    public void Bind(Text regolith, Text oxygen, Text prompt, Text objective, Text win)
    {
        regolithText = regolith;
        oxygenText = oxygen;
        promptText = prompt;
        objectiveText = objective;
        winText = win;
    }

    public void Bind(Text regolith, Text oxygen, Text concrete, Text prompt, Text objective, Text win)
    {
        regolithText = regolith;
        oxygenText = oxygen;
        concreteText = concrete;
        promptText = prompt;
        objectiveText = objective;
        winText = win;
    }
}
