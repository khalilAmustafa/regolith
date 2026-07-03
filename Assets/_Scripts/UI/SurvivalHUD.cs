using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class SurvivalHUD : MonoBehaviour
{
    public static SurvivalHUD Instance { get; private set; }

    public PlayerSurvivalResources player;

    [SerializeField] private Component oxygenText;
    [SerializeField] private Component storedOxygenText;
    [SerializeField] private Component regolithText;
    [SerializeField] private Component concreteText;
    [SerializeField] private Component powerText;
    [SerializeField] private Component promptText;
    [SerializeField] private Component messageText;

    private Coroutine messageCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple SurvivalHUD instances found. Using the latest instance.", this);
        }

        Instance = this;

        if (player == null)
        {
            player = FindAnyObjectByType<PlayerSurvivalResources>();
        }

        SetPrompt(string.Empty);
        SetText(messageText, string.Empty);
        SetVisible(messageText, false);
    }

    private void Update()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerSurvivalResources>();
        }

        if (player == null)
        {
            return;
        }

        SetText(oxygenText, "Suit O2: " + player.currentSuitOxygen.ToString("0") + " / " + player.maxSuitOxygen.ToString("0"));
        SetText(storedOxygenText, "Stored O2: " + player.storedOxygen.ToString("0") + " / " + player.maxStoredOxygen.ToString("0"));
        SetText(regolithText, "Regolith: " + player.GetRegolith() + " / " + player.GetMaxRegolith());
        SetText(concreteText, "Concrete: " + player.GetConcrete() + " / " + player.GetMaxConcrete());
        SetText(powerText, "Power: " + (player.powerOnline ? "ONLINE" : "OFFLINE"));
    }

    public void SetPrompt(string text)
    {
        SetText(promptText, text);
        SetVisible(promptText, !string.IsNullOrWhiteSpace(text));
    }

    public void ShowMessage(string text, float duration = 2f)
    {
        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
        }

        messageCoroutine = StartCoroutine(ShowMessageRoutine(text, duration));
    }

    public void Bind(
        Component newOxygenText,
        Component newStoredOxygenText,
        Component newRegolithText,
        Component newPowerText,
        Component newPromptText,
        Component newMessageText)
    {
        oxygenText = newOxygenText;
        storedOxygenText = newStoredOxygenText;
        regolithText = newRegolithText;
        powerText = newPowerText;
        promptText = newPromptText;
        messageText = newMessageText;
    }

    public void Bind(
        Component newOxygenText,
        Component newStoredOxygenText,
        Component newRegolithText,
        Component newConcreteText,
        Component newPowerText,
        Component newPromptText,
        Component newMessageText)
    {
        oxygenText = newOxygenText;
        storedOxygenText = newStoredOxygenText;
        regolithText = newRegolithText;
        concreteText = newConcreteText;
        powerText = newPowerText;
        promptText = newPromptText;
        messageText = newMessageText;
    }

    public void BindConcreteText(Component newConcreteText)
    {
        concreteText = newConcreteText;
    }

    private IEnumerator ShowMessageRoutine(string text, float duration)
    {
        SetText(messageText, text);
        SetVisible(messageText, !string.IsNullOrWhiteSpace(text));

        yield return new WaitForSeconds(Mathf.Max(0f, duration));

        SetText(messageText, string.Empty);
        SetVisible(messageText, false);
        messageCoroutine = null;
    }

    private void SetText(Component target, string value)
    {
        if (target == null)
        {
            return;
        }

        Text uiText = target as Text;
        if (uiText != null)
        {
            uiText.text = value;
            return;
        }

        PropertyInfo textProperty = target.GetType().GetProperty("text");
        if (textProperty != null && textProperty.CanWrite)
        {
            textProperty.SetValue(target, value);
        }
    }

    private void SetVisible(Component target, bool visible)
    {
        if (target == null)
        {
            return;
        }

        target.gameObject.SetActive(visible);
    }
}
