using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private BasePowerSystem powerSystem;

    [Header("Slots")]
    [SerializeField] private InventorySlotUI regolithSlot;
    [SerializeField] private InventorySlotUI storedOxygenSlot;
    [SerializeField] private InventorySlotUI regolithPanelsSlot;

    [Header("Icons")]
    [SerializeField] private Sprite regolithIcon;
    [SerializeField] private Sprite storedOxygenIcon;
    [SerializeField] private Sprite regolithPanelsIcon;

    private PlayerSurvivalResources survivalResources;
    private SolarPowerState solarPowerState;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInventoryUIExists()
    {
        InventoryUI[] existing = FindObjectsByType<InventoryUI>(FindObjectsInactive.Include);
        if (existing != null && existing.Length > 0)
        {
            return;
        }

        GameObject inventoryUiObject = new GameObject("InventoryUI");
        inventoryUiObject.AddComponent<InventoryUI>();
    }

    private void Start()
    {
        ResolveReferences();
        LoadDefaultIconsIfMissing();

        if (inventoryPanel == null)
        {
            inventoryPanel = FindExistingInventoryPanel();
        }

        if (inventoryPanel == null)
        {
            CreateDefaultPanel();
        }
        else
        {
            ConfigurePanelVisuals();
            ReplacePlainTextPanelWithSlots();
            ResolveSlotReferences();
            CreateMissingSlots();
        }

        UpdateSlots();

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) && inventoryPanel != null)
        {
            bool shouldShow = !inventoryPanel.activeSelf;
            inventoryPanel.SetActive(shouldShow);

            if (shouldShow)
            {
                UpdateSlots();
            }
        }

        if (inventoryPanel != null && inventoryPanel.activeSelf)
        {
            UpdateSlots();
        }
    }

    private void UpdateSlots()
    {
        ResolveReferences();

        if (regolithSlot != null)
        {
            regolithSlot.SetSlot(regolithIcon, "Regolith", $"{GetRegolith()} / {GetMaxRegolith()}");
        }

        if (storedOxygenSlot != null)
        {
            storedOxygenSlot.SetSlot(storedOxygenIcon, "Stored O2", $"{GetStoredOxygen()} / {GetMaxStoredOxygen()}");
        }

        if (regolithPanelsSlot != null)
        {
            regolithPanelsSlot.SetSlot(regolithPanelsIcon, "Regolith Panels", GetConcrete().ToString());
        }
    }

    private void ResolveReferences()
    {
        if (inventory == null)
        {
            inventory = FindAnyObjectByType<PlayerInventory>();
        }

        if (survivalResources == null)
        {
            survivalResources = inventory != null
                ? inventory.GetComponent<PlayerSurvivalResources>()
                : FindAnyObjectByType<PlayerSurvivalResources>();
        }

        if (powerSystem == null)
        {
            powerSystem = FindAnyObjectByType<BasePowerSystem>();
        }

        if (solarPowerState == null)
        {
            solarPowerState = FindAnyObjectByType<SolarPowerState>();
        }
    }

    private int GetRegolith()
    {
        if (survivalResources != null)
        {
            return survivalResources.GetRegolith();
        }

        return inventory != null ? inventory.Regolith : 0;
    }

    private int GetMaxRegolith()
    {
        if (survivalResources != null)
        {
            return survivalResources.GetMaxRegolith();
        }

        return inventory != null ? inventory.MaxRegolith : 0;
    }

    private int GetStoredOxygen()
    {
        if (survivalResources != null)
        {
            return Mathf.RoundToInt(survivalResources.GetStoredO2());
        }

        return inventory != null ? inventory.StoredOxygen : 0;
    }

    private int GetMaxStoredOxygen()
    {
        if (survivalResources != null)
        {
            return Mathf.RoundToInt(survivalResources.GetMaxStoredO2());
        }

        return inventory != null ? inventory.MaxStoredOxygen : 0;
    }

    private int GetConcrete()
    {
        if (survivalResources != null)
        {
            return survivalResources.GetConcrete();
        }

        return inventory != null ? inventory.Concrete : 0;
    }

    private GameObject FindExistingInventoryPanel()
    {
        GameObject existingPanel = GameObject.Find("InventoryPanel");
        if (existingPanel != null)
        {
            return existingPanel;
        }

        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform candidate in allTransforms)
        {
            if (candidate != null && candidate.name == "InventoryPanel" && candidate.gameObject.scene.IsValid())
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private void ReplacePlainTextPanelWithSlots()
    {
        if (inventoryPanel == null)
        {
            return;
        }

        RemoveChildIfExists("RegolithText");
        RemoveChildIfExists("StoredOxygenText");
        RemoveChildIfExists("RegolithPanelsText");
        RemoveChildIfExists("PowerText");
    }

    private void RemoveChildIfExists(string childName)
    {
        Transform child = FindChildRecursive(inventoryPanel.transform, childName);
        if (child != null)
        {
            Destroy(child.gameObject);
        }
    }

    private void ResolveSlotReferences()
    {
        if (inventoryPanel == null)
        {
            return;
        }

        if (regolithSlot == null)
        {
            regolithSlot = FindSlot("RegolithSlot");
        }

        if (storedOxygenSlot == null)
        {
            storedOxygenSlot = FindSlot("StoredO2Slot");
        }

        if (regolithPanelsSlot == null)
        {
            regolithPanelsSlot = FindSlot("RegolithPanelsSlot");
        }
    }

    private InventorySlotUI FindSlot(string childName)
    {
        Transform child = FindChildRecursive(inventoryPanel.transform, childName);
        return child != null ? child.GetComponent<InventorySlotUI>() : null;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void CreateDefaultPanel()
    {
        Canvas targetCanvas = FindOrCreateCanvas();
        inventoryPanel = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
        inventoryPanel.transform.SetParent(targetCanvas.transform, false);
        ConfigurePanelVisuals();

        CreateTitleIfMissing();
        CreateSlotsContainer();
        CreateMissingSlots();
    }

    private void ConfigurePanelVisuals()
    {
        RectTransform panelRect = inventoryPanel.GetComponent<RectTransform>();
        if (panelRect == null)
        {
            panelRect = inventoryPanel.AddComponent<RectTransform>();
        }

        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-36f, 0f);
        panelRect.sizeDelta = new Vector2(454f, 210f);

        Image background = inventoryPanel.GetComponent<Image>();
        if (background == null)
        {
            background = inventoryPanel.AddComponent<Image>();
        }

        background.color = new Color(0.02f, 0.025f, 0.03f, 0.88f);
        background.raycastTarget = false;
    }

    private void CreateTitleIfMissing()
    {
        if (inventoryPanel == null || FindChildRecursive(inventoryPanel.transform, "InventoryTitle") != null)
        {
            return;
        }

        CreateText("InventoryTitle", inventoryPanel.transform, "Inventory", new Vector2(18f, -14f), new Vector2(418f, 34f), 28f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
    }

    private Transform CreateSlotsContainer()
    {
        Transform existingContainer = FindChildRecursive(inventoryPanel.transform, "InventorySlots");
        if (existingContainer != null)
        {
            return existingContainer;
        }

        GameObject containerObject = new GameObject("InventorySlots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        containerObject.transform.SetParent(inventoryPanel.transform, false);

        RectTransform rect = containerObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(18f, 18f);
        rect.offsetMax = new Vector2(-18f, -58f);

        HorizontalLayoutGroup layout = containerObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return containerObject.transform;
    }

    private void CreateMissingSlots()
    {
        if (inventoryPanel == null)
        {
            return;
        }

        CreateTitleIfMissing();
        Transform slotsContainer = CreateSlotsContainer();

        if (regolithSlot == null)
        {
            regolithSlot = CreateSlot("RegolithSlot", slotsContainer);
        }

        if (storedOxygenSlot == null)
        {
            storedOxygenSlot = CreateSlot("StoredO2Slot", slotsContainer);
        }

        if (regolithPanelsSlot == null)
        {
            regolithPanelsSlot = CreateSlot("RegolithPanelsSlot", slotsContainer);
        }
    }

    private InventorySlotUI CreateSlot(string name, Transform parent)
    {
        GameObject slotObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InventorySlotUI));
        slotObject.transform.SetParent(parent, false);

        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(120f, 120f);

        Image background = slotObject.GetComponent<Image>();
        background.color = new Color(0.09f, 0.105f, 0.12f, 0.96f);
        background.raycastTarget = false;

        Image iconImage = CreateSlotImage(slotObject.transform);
        TextMeshProUGUI nameText = CreateText("NameText", slotObject.transform, string.Empty, new Vector2(8f, -8f), new Vector2(104f, 28f), 15f, FontStyles.Bold, TextAlignmentOptions.Top);
        TextMeshProUGUI amountText = CreateText("AmountText", slotObject.transform, string.Empty, new Vector2(8f, 8f), new Vector2(104f, 26f), 17f, FontStyles.Bold, TextAlignmentOptions.BottomRight);

        RectTransform nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);

        RectTransform amountRect = amountText.GetComponent<RectTransform>();
        amountRect.anchorMin = new Vector2(0f, 0f);
        amountRect.anchorMax = new Vector2(1f, 0f);
        amountRect.pivot = new Vector2(0.5f, 0f);

        InventorySlotUI slot = slotObject.GetComponent<InventorySlotUI>();
        slot.Bind(iconImage, nameText, amountText, slotObject);
        return slot;
    }

    private Image CreateSlotImage(Transform parent)
    {
        GameObject iconObject = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(parent, false);

        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -2f);
        rect.sizeDelta = new Vector2(62f, 62f);

        Image image = iconObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(0.18f, 0.2f, 0.22f, 0.95f);
        return image;
    }

    private Canvas FindOrCreateCanvas()
    {
        SurvivalHUD survivalHud = SurvivalHUD.Instance != null ? SurvivalHUD.Instance : FindAnyObjectByType<SurvivalHUD>();
        if (survivalHud != null)
        {
            Canvas hudCanvas = survivalHud.GetComponentInParent<Canvas>();
            if (hudCanvas != null)
            {
                return hudCanvas;
            }
        }

        SimpleHUD simpleHud = FindAnyObjectByType<SimpleHUD>();
        if (simpleHud != null)
        {
            Canvas simpleHudCanvas = simpleHud.GetComponentInParent<Canvas>();
            if (simpleHudCanvas != null)
            {
                return simpleHudCanvas;
            }
        }

        GameObject namedCanvasObject = GameObject.Find("SurvivalHUD_Canvas");
        if (namedCanvasObject != null && namedCanvasObject.TryGetComponent(out Canvas namedCanvas))
        {
            return namedCanvas;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.name.Contains("HUD"))
            {
                return canvas;
            }
        }

        GameObject canvasObject = new GameObject("InventoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas createdCanvas = canvasObject.GetComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return createdCanvas;
    }

    private TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string text,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.color = Color.white;
        textComponent.alignment = alignment;
        textComponent.textWrappingMode = TextWrappingModes.NoWrap;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.raycastTarget = false;

        return textComponent;
    }

    private void LoadDefaultIconsIfMissing()
    {
        regolithIcon ??= Resources.Load<Sprite>("InventoryIcons/raw_regolith");
        storedOxygenIcon ??= Resources.Load<Sprite>("InventoryIcons/o2");
        regolithPanelsIcon ??= Resources.Load<Sprite>("InventoryIcons/Regolith_Panels");
    }
}
