using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RegolithRoomProcessor : MonoBehaviour
{
    public ProcessorType processorType;
    public PlayerSurvivalResources resources;
    public SolarPowerState solarPowerState;
    public int requiredRegolith = 3;
    public float outputStoredO2 = 25f;
    public int outputConcrete = 1;
    public float processingDuration = 5f;
    public bool requirePower = true;
    public KeyCode interactKey = KeyCode.E;
    public string playerTag = "Player";
    public GameObject promptUI;
    public TMP_Text promptText;
    public GameObject statusUI;
    public TMP_Text statusText;
    public bool playerInside = false;
    public bool isProcessing = false;

    private Coroutine statusRoutine;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        ResolveReferences();
        HidePrompt();
        HideStatus();
    }

    private void OnValidate()
    {
        requiredRegolith = Mathf.Max(0, requiredRegolith);
        outputStoredO2 = Mathf.Max(0f, outputStoredO2);
        outputConcrete = Mathf.Max(0, outputConcrete);
        processingDuration = Mathf.Max(0f, processingDuration);
        EnsureTriggerCollider();
    }

    private void Update()
    {
        ResolveReferences();

        if (playerInside && !isProcessing)
        {
            ShowPrompt();
        }

        if (playerInside && !isProcessing && Input.GetKeyDown(interactKey))
        {
            TryStartProcessing();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        playerInside = true;
        ResolveReferences(other.gameObject);

        if (!isProcessing)
        {
            ShowPrompt();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        playerInside = false;
        HidePrompt();
    }

    public void TryStartProcessing()
    {
        ResolveReferences();

        if (resources == null)
        {
            ShowStatus("Missing player resources");
            return;
        }

        if (!HasPower())
        {
            ShowStatus("Power required");
            return;
        }

        if (!resources.HasRegolith(requiredRegolith))
        {
            ShowStatus("Need regolith");
            return;
        }

        if (processorType == ProcessorType.Oxygen && !resources.CanAddStoredO2(outputStoredO2))
        {
            ShowStatus("Stored O2 full");
            return;
        }

        if (processorType == ProcessorType.Concrete && !resources.CanAddConcrete(outputConcrete))
        {
            ShowStatus("Concrete storage full");
            return;
        }

        if (!resources.ConsumeRegolith(requiredRegolith))
        {
            ShowStatus("Need regolith");
            return;
        }

        StartCoroutine(ProcessRoutine());
    }

    private IEnumerator ProcessRoutine()
    {
        isProcessing = true;
        HidePrompt();

        ShowStatus(processorType == ProcessorType.Oxygen ? "Extracting oxygen..." : "Heating regolith...", 0f);

        yield return new WaitForSeconds(processingDuration);

        if (resources != null)
        {
            if (processorType == ProcessorType.Oxygen)
            {
                resources.AddStoredO2(outputStoredO2);
                ShowStatus("O2 extraction complete", 0f);
            }
            else
            {
                resources.AddConcrete(outputConcrete);
                ShowStatus("Concrete produced", 0f);
            }
        }

        yield return new WaitForSeconds(1.5f);

        HideStatus();
        isProcessing = false;

        if (playerInside)
        {
            ShowPrompt();
        }
    }

    private bool HasPower()
    {
        if (!requirePower)
        {
            return true;
        }

        if (solarPowerState != null)
        {
            bool online = solarPowerState.IsOnline;
            if (resources != null)
            {
                resources.SetPowerOnline(online);
            }

            return online;
        }

        return resources != null && resources.IsPowerOnline();
    }

    private void ShowPrompt()
    {
        string prompt = processorType == ProcessorType.Oxygen
            ? "Press E\nProcess regolith into O2"
            : "Press E\nProcess regolith into concrete";

        if (promptText != null)
        {
            promptText.text = prompt;
        }

        if (promptUI != null)
        {
            promptUI.SetActive(true);
        }

        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.SetPrompt(prompt);
        }
    }

    private void HidePrompt()
    {
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.SetPrompt(string.Empty);
        }
    }

    private void ShowStatus(string message, float duration = 2f)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (statusUI != null)
        {
            statusUI.SetActive(true);
        }

        if (SurvivalHUD.Instance != null)
        {
            SurvivalHUD.Instance.ShowMessage(message, duration > 0f ? duration : 2f);
        }

        if (duration > 0f)
        {
            if (statusRoutine != null)
            {
                StopCoroutine(statusRoutine);
            }

            statusRoutine = StartCoroutine(HideStatusAfter(duration));
        }
    }

    private IEnumerator HideStatusAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        HideStatus();
        statusRoutine = null;
    }

    private void HideStatus()
    {
        if (statusUI != null)
        {
            statusUI.SetActive(false);
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private void ResolveReferences(GameObject playerObject = null)
    {
        if (resources == null)
        {
            if (playerObject != null)
            {
                resources = playerObject.GetComponent<PlayerSurvivalResources>();
            }

            if (resources == null)
            {
                resources = FindAnyObjectByType<PlayerSurvivalResources>();
            }
        }

        if (solarPowerState == null)
        {
            solarPowerState = FindAnyObjectByType<SolarPowerState>();
        }
    }
}
