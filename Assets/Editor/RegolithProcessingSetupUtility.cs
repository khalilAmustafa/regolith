using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RegolithProcessingSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Regolith Processing System";

    [MenuItem(MenuPath)]
    public static void SetupRegolithProcessingSystem()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Regolith processing setup cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerSurvivalResources resources = EnsurePlayerResources(player);
        SolarPowerState solarPowerState = UnityEngine.Object.FindAnyObjectByType<SolarPowerState>();
        SolarPowerResourceSync powerSync = EnsurePowerSync(player, resources, solarPowerState);
        SurvivalHUD hud = EnsureHud(resources);
        Component concreteHudText = EnsureConcreteHudText(hud);

        GameObject room02 = FindSceneObject("Room_02");
        GameObject room03 = FindSceneObject("Room_03");

        RegolithRoomProcessor oxygenProcessor = ConfigureProcessor(
            room02,
            "Room_02_RegolithO2Processor",
            ProcessorType.Oxygen,
            resources,
            solarPowerState,
            3,
            25f,
            1,
            5f);

        RegolithRoomProcessor concreteProcessor = ConfigureProcessor(
            room03,
            "Room_03_RegolithConcreteProcessor",
            ProcessorType.Concrete,
            resources,
            solarPowerState,
            4,
            25f,
            1,
            6f);

        RegolithCollectionManager collectionManager = UnityEngine.Object.FindAnyObjectByType<RegolithCollectionManager>();
        if (collectionManager != null)
        {
            Undo.RecordObject(collectionManager, "Configure regolith collection manager");
            collectionManager.resources = resources;
            collectionManager.collectAfterGatherAnimation = false;
            EditorUtility.SetDirty(collectionManager);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Regolith processing setup complete:\n" +
            "- Player found: " + (player != null ? "yes" : "no") + "\n" +
            "- PlayerSurvivalResources assigned: " + (resources != null ? "yes" : "no") + "\n" +
            "- SolarPowerState found: " + (solarPowerState != null ? "yes" : "no") + "\n" +
            "- SolarPowerResourceSync assigned: " + (powerSync != null ? "yes" : "no") + "\n" +
            "- Concrete HUD line assigned: " + (concreteHudText != null ? "yes" : "no") + "\n" +
            "- Room_02 processor assigned: " + (oxygenProcessor != null ? "yes" : "no") + "\n" +
            "- Room_03 processor assigned: " + (concreteProcessor != null ? "yes" : "no") + "\n" +
            "- Regolith collection uses direct collection/fallback: yes");
    }

    private static PlayerSurvivalResources EnsurePlayerResources(GameObject player)
    {
        PlayerSurvivalResources resources = player != null
            ? player.GetComponent<PlayerSurvivalResources>()
            : UnityEngine.Object.FindAnyObjectByType<PlayerSurvivalResources>();

        if (resources == null && player != null)
        {
            resources = Undo.AddComponent<PlayerSurvivalResources>(player);
        }

        if (resources == null)
        {
            Debug.LogWarning("Regolith processing setup: Player object/resources not found. Assign resources manually.");
            return null;
        }

        Undo.RecordObject(resources, "Configure player survival resources");
        resources.maxRegolith = 10;
        resources.maxRegolithAmount = 10f;
        resources.maxConcrete = 20;
        resources.maxStoredOxygen = Mathf.Max(100f, resources.maxStoredOxygen);
        resources.maxStoredO2 = resources.maxStoredOxygen;
        EditorUtility.SetDirty(resources);
        return resources;
    }

    private static SolarPowerResourceSync EnsurePowerSync(GameObject player, PlayerSurvivalResources resources, SolarPowerState solarPowerState)
    {
        if (player == null)
        {
            return UnityEngine.Object.FindAnyObjectByType<SolarPowerResourceSync>();
        }

        SolarPowerResourceSync sync = player.GetComponent<SolarPowerResourceSync>();
        if (sync == null)
        {
            sync = Undo.AddComponent<SolarPowerResourceSync>(player);
        }
        else
        {
            Undo.RecordObject(sync, "Configure solar power resource sync");
        }

        sync.resources = resources;
        sync.solarPowerState = solarPowerState;
        EditorUtility.SetDirty(sync);
        return sync;
    }

    private static SurvivalHUD EnsureHud(PlayerSurvivalResources resources)
    {
        SurvivalHUD hud = UnityEngine.Object.FindAnyObjectByType<SurvivalHUD>();
        if (hud == null)
        {
            hud = SurvivalHUDBuilder.CreateSurvivalHUD();
            Undo.RegisterCreatedObjectUndo(hud.gameObject, "Create survival HUD");
        }

        Undo.RecordObject(hud, "Configure survival HUD");
        hud.player = resources;
        EditorUtility.SetDirty(hud);
        return hud;
    }

    private static Component EnsureConcreteHudText(SurvivalHUD hud)
    {
        if (hud == null)
        {
            return null;
        }

        Transform resourcePanel = hud.transform.Find("ResourcePanel");
        if (resourcePanel == null)
        {
            resourcePanel = FindChildByName(hud.transform, "ResourcePanel");
        }

        if (resourcePanel == null)
        {
            Debug.LogWarning("Regolith processing setup: SurvivalHUD ResourcePanel not found. Concrete HUD text needs manual assignment.");
            return null;
        }

        Transform concreteExisting = resourcePanel.Find("ConcreteText");
        Component concreteText = concreteExisting != null
            ? concreteExisting.GetComponent<TMP_Text>() as Component ?? concreteExisting.GetComponent<Text>() as Component
            : null;
        if (concreteExisting == null)
        {
            GameObject textObject = new GameObject("ConcreteText", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textObject, "Create concrete HUD text");
            textObject.transform.SetParent(resourcePanel, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -108f);
            rect.sizeDelta = new Vector2(900f, 34f);

            concreteText = AddTextComponent(textObject, "Concrete: 0 / 20", 24, TextAlignmentOptions.TopLeft);

            Transform power = resourcePanel.Find("PowerText");
            if (power != null)
            {
                RectTransform powerRect = power.GetComponent<RectTransform>();
                if (powerRect != null && powerRect.anchoredPosition.y > -130f)
                {
                    Undo.RecordObject(powerRect, "Move power HUD text");
                    powerRect.anchoredPosition = new Vector2(powerRect.anchoredPosition.x, -144f);
                    EditorUtility.SetDirty(powerRect);
                }
            }
        }
        else
        {
            concreteText = concreteExisting.GetComponent<TMP_Text>() as Component
                ?? concreteExisting.GetComponent<Text>() as Component;
        }

        hud.BindConcreteText(concreteText);
        EditorUtility.SetDirty(hud);
        return concreteText;
    }

    private static RegolithRoomProcessor ConfigureProcessor(
        GameObject room,
        string triggerName,
        ProcessorType processorType,
        PlayerSurvivalResources resources,
        SolarPowerState solarPowerState,
        int requiredRegolith,
        float outputO2Amount,
        int outputConcreteAmount,
        float processingDuration)
    {
        if (room == null)
        {
            Debug.LogWarning("Regolith processing setup: " + triggerName + " parent room was not found.");
            return null;
        }

        Transform existing = room.transform.Find(triggerName);
        GameObject triggerObject;
        if (existing != null)
        {
            triggerObject = existing.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(triggerObject, "Update regolith processor trigger");
        }
        else
        {
            triggerObject = new GameObject(triggerName);
            Undo.RegisterCreatedObjectUndo(triggerObject, "Create regolith processor trigger");
            triggerObject.transform.SetParent(room.transform, false);
        }

        RegolithRoomProcessor processor = triggerObject.GetComponent<RegolithRoomProcessor>();
        if (processor == null)
        {
            processor = Undo.AddComponent<RegolithRoomProcessor>(triggerObject);
        }
        else
        {
            Undo.RecordObject(processor, "Configure regolith room processor");
        }

        BoxCollider trigger = triggerObject.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BoxCollider>(triggerObject);
        }
        else
        {
            Undo.RecordObject(trigger, "Configure regolith processor trigger collider");
        }

        Bounds bounds = CalculateRendererBounds(room);
        Vector3 center = bounds.size == Vector3.zero ? room.transform.position : bounds.center;
        triggerObject.transform.position = center;
        triggerObject.transform.rotation = room.transform.rotation;
        triggerObject.transform.localScale = Vector3.one;
        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
        trigger.size = new Vector3(
            Mathf.Max(4f, bounds.size.x + 2f),
            Mathf.Max(3f, bounds.size.y + 1.5f),
            Mathf.Max(4f, bounds.size.z + 2f));

        processor.processorType = processorType;
        processor.resources = resources;
        processor.solarPowerState = solarPowerState;
        processor.requiredRegolith = requiredRegolith;
        processor.outputStoredO2 = outputO2Amount;
        processor.outputConcrete = outputConcreteAmount;
        processor.processingDuration = processingDuration;
        processor.requirePower = true;
        processor.interactKey = KeyCode.E;
        processor.playerTag = "Player";

        EnsureProcessorUi(processor, triggerObject.transform);

        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(processor);
        EditorUtility.SetDirty(triggerObject);
        return processor;
    }

    private static void EnsureProcessorUi(RegolithRoomProcessor processor, Transform parent)
    {
        string prefix = processor.processorType == ProcessorType.Oxygen ? "O2" : "Concrete";
        processor.promptUI = EnsureWorldText(
            parent,
            prefix + "_ProcessorPromptUI",
            processor.processorType == ProcessorType.Oxygen ? "Press E\nProcess regolith into O2" : "Press E\nProcess regolith into concrete",
            new Vector3(0f, 2.4f, 0f),
            out TMP_Text promptText);
        processor.promptText = promptText;

        processor.statusUI = EnsureWorldText(
            parent,
            prefix + "_ProcessorStatusUI",
            string.Empty,
            new Vector3(0f, 2.9f, 0f),
            out TMP_Text statusText);
        processor.statusText = statusText;

        if (processor.promptUI != null)
        {
            processor.promptUI.SetActive(false);
        }

        if (processor.statusUI != null)
        {
            processor.statusUI.SetActive(false);
        }
    }

    private static GameObject EnsureWorldText(Transform parent, string name, string text, Vector3 localPosition, out TMP_Text tmpText)
    {
        Transform existing = parent.Find(name);
        GameObject root;
        if (existing != null)
        {
            root = existing.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(root, "Update processor UI");
        }
        else
        {
            root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(root, "Create processor UI");
            root.transform.SetParent(parent, false);
        }

        root.transform.localPosition = localPosition;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(360f, 90f);

        Transform textChild = root.transform.Find("Text");
        GameObject textObject;
        if (textChild != null)
        {
            textObject = textChild.gameObject;
        }
        else
        {
            textObject = new GameObject("Text", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textObject, "Create processor UI text");
            textObject.transform.SetParent(root.transform, false);
        }

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        tmpText = textObject.GetComponent<TMP_Text>();
        if (tmpText == null)
        {
            tmpText = Undo.AddComponent<TextMeshProUGUI>(textObject);
        }

        tmpText.text = text;
        tmpText.fontSize = 28f;
        tmpText.color = Color.white;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.raycastTarget = false;

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(tmpText);
        return root;
    }

    private static Component AddTextComponent(GameObject target, string text, int fontSize, TextAlignmentOptions alignment)
    {
        TMP_Text tmpText = target.GetComponent<TMP_Text>();
        if (tmpText == null)
        {
            tmpText = Undo.AddComponent<TextMeshProUGUI>(target);
        }

        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.color = Color.white;
        tmpText.alignment = alignment;
        tmpText.raycastTarget = false;
        return tmpText;
    }

    private static GameObject FindSceneObject(string exactName)
    {
        GameObject exact = GameObject.Find(exactName);
        if (exact != null)
        {
            return exact;
        }

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name.IndexOf(exactName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }
}
