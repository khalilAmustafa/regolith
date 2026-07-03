using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RoomProcessingSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Room Processing System";

    [MenuItem(MenuPath)]
    public static void SetupRoomProcessingSystem()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Room processing setup cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        PlayerSurvivalResources resources = FindOrCreateResources();
        SolarPowerState solarPowerState = FindSolarPowerState();
        EnsurePowerSync(resources, solarPowerState);
        SurvivalHUD hud = EnsureHud(resources);
        EnsureConcreteHudText(hud);

        GameObject habitat = FindSceneObject("Habitat_01");
        if (habitat != null)
        {
            Debug.Log("Habitat_01 found and intentionally left inactive.");
        }
        else
        {
            Debug.LogWarning("Habitat_01 not found. No habitat gameplay was added.");
        }

        GameObject room02 = FindSceneObject("Room_02");
        GameObject room03 = FindSceneObject("Room_03");

        RegolithRoomProcessor oxygenProcessor = ConfigureRoomProcessor(
            room02,
            habitat,
            "Room_02_ProcessorInteractPoint",
            ProcessorType.Oxygen,
            resources,
            solarPowerState,
            3,
            25f,
            0,
            5f);

        RegolithRoomProcessor concreteProcessor = ConfigureRoomProcessor(
            room03,
            habitat,
            "Room_03_ProcessorInteractPoint",
            ProcessorType.Concrete,
            resources,
            solarPowerState,
            4,
            0f,
            1,
            6f);

        RegolithCollectionManager collectionManager = UnityEngine.Object.FindAnyObjectByType<RegolithCollectionManager>();
        if (collectionManager != null)
        {
            Undo.RecordObject(collectionManager, "Wire regolith resources");
            collectionManager.resources = resources;
            collectionManager.collectAfterGatherAnimation = false;
            EditorUtility.SetDirty(collectionManager);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Room processing setup complete:\n" +
            "- PlayerSurvivalResources assigned: " + (resources != null ? "yes" : "no") + "\n" +
            "- SolarPowerState assigned: " + (solarPowerState != null ? "yes" : "no") + "\n" +
            "- Room_02 O2 processor: " + (oxygenProcessor != null ? "yes" : "no") + "\n" +
            "- Room_03 concrete processor: " + (concreteProcessor != null ? "yes" : "no") + "\n" +
            "- Habitat_01 inactive: yes\n" +
            "- Rooms moved: no\n" +
            "- Room models/textures changed: no");
    }

    private static RegolithRoomProcessor ConfigureRoomProcessor(
        GameObject room,
        GameObject habitat,
        string interactPointName,
        ProcessorType processorType,
        PlayerSurvivalResources resources,
        SolarPowerState solarPowerState,
        int requiredRegolith,
        float outputStoredO2,
        int outputConcrete,
        float processingDuration)
    {
        if (room == null)
        {
            Debug.LogWarning("Room processing setup: " + interactPointName + " parent room was not found.");
            return null;
        }

        DisableLegacyProcessorChildren(room, interactPointName);

        Transform existing = room.transform.Find(interactPointName);
        GameObject interactPoint;
        if (existing != null)
        {
            interactPoint = existing.gameObject;
            interactPoint.SetActive(true);
            Undo.RegisterFullObjectHierarchyUndo(interactPoint, "Update room processor interact point");
        }
        else
        {
            interactPoint = new GameObject(interactPointName);
            Undo.RegisterCreatedObjectUndo(interactPoint, "Create room processor interact point");
            interactPoint.transform.SetParent(room.transform, true);
        }

        PositionInteractPointNearDoor(room, habitat, interactPoint);

        BoxCollider trigger = interactPoint.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BoxCollider>(interactPoint);
        }
        else
        {
            Undo.RecordObject(trigger, "Configure room processor trigger");
        }

        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
        trigger.size = new Vector3(3f, 2.4f, 3f);

        RegolithRoomProcessor processor = interactPoint.GetComponent<RegolithRoomProcessor>();
        if (processor == null)
        {
            processor = Undo.AddComponent<RegolithRoomProcessor>(interactPoint);
        }
        else
        {
            Undo.RecordObject(processor, "Configure room processor");
        }

        processor.processorType = processorType;
        processor.resources = resources;
        processor.solarPowerState = solarPowerState;
        processor.requiredRegolith = requiredRegolith;
        processor.outputStoredO2 = outputStoredO2;
        processor.outputConcrete = outputConcrete;
        processor.processingDuration = processingDuration;
        processor.requirePower = true;
        processor.interactKey = KeyCode.E;
        processor.playerTag = "Player";

        EnsureProcessorUi(processor, interactPoint.transform);

        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(processor);
        EditorUtility.SetDirty(interactPoint);
        return processor;
    }

    private static void PositionInteractPointNearDoor(GameObject room, GameObject habitat, GameObject interactPoint)
    {
        Bounds roomBounds = CalculateRendererBounds(room);
        Vector3 roomCenter = roomBounds.size == Vector3.zero ? room.transform.position : roomBounds.center;
        Vector3 directionToDoor = room.transform.forward;

        if (habitat != null)
        {
            Bounds habitatBounds = CalculateRendererBounds(habitat);
            Vector3 habitatCenter = habitatBounds.size == Vector3.zero ? habitat.transform.position : habitatBounds.center;
            Vector3 toHabitat = habitatCenter - roomCenter;
            toHabitat.y = 0f;
            if (toHabitat.sqrMagnitude > 0.01f)
            {
                directionToDoor = toHabitat.normalized;
            }
        }

        float roomRadius = Mathf.Max(roomBounds.extents.x, roomBounds.extents.z);
        Vector3 doorPosition = roomCenter + directionToDoor * Mathf.Max(1.5f, roomRadius * 0.65f);
        doorPosition.y = roomCenter.y + 1.2f;

        Undo.RecordObject(interactPoint.transform, "Place room processor interact point");
        interactPoint.transform.position = doorPosition;
        interactPoint.transform.rotation = Quaternion.LookRotation(directionToDoor, Vector3.up);
        interactPoint.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(interactPoint.transform);
    }

    private static void DisableLegacyProcessorChildren(GameObject room, string currentInteractPointName)
    {
        foreach (Transform child in room.GetComponentsInChildren<Transform>(true))
        {
            if (child == room.transform || child.name == currentInteractPointName)
            {
                continue;
            }

            bool legacyProcessor =
                child.name.Contains("RegolithO2Processor", StringComparison.OrdinalIgnoreCase)
                || child.name.Contains("RegolithConcreteProcessor", StringComparison.OrdinalIgnoreCase);

            if (legacyProcessor)
            {
                Undo.RecordObject(child.gameObject, "Disable legacy room processor child");
                child.gameObject.SetActive(false);
                EditorUtility.SetDirty(child.gameObject);
            }
        }
    }

    private static void EnsureProcessorUi(RegolithRoomProcessor processor, Transform parent)
    {
        string prefix = processor.processorType == ProcessorType.Oxygen ? "O2" : "Concrete";
        string prompt = processor.processorType == ProcessorType.Oxygen
            ? "Press E\nProcess regolith into O2"
            : "Press E\nProcess regolith into concrete";

        processor.promptUI = EnsureWorldText(parent, prefix + "_ProcessorPromptUI", prompt, new Vector3(0f, 1.8f, 0f), out TMP_Text promptText);
        processor.promptText = promptText;
        processor.statusUI = EnsureWorldText(parent, prefix + "_ProcessorStatusUI", string.Empty, new Vector3(0f, 2.25f, 0f), out TMP_Text statusText);
        processor.statusText = statusText;

        processor.promptUI.SetActive(false);
        processor.statusUI.SetActive(false);
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
        canvasRect.sizeDelta = new Vector2(420f, 90f);

        Transform textChild = root.transform.Find("Text");
        GameObject textObject;
        if (textChild != null)
        {
            textObject = textChild.gameObject;
        }
        else
        {
            textObject = new GameObject("Text", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textObject, "Create processor text");
            textObject.transform.SetParent(root.transform, false);
        }

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

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

    private static PlayerSurvivalResources FindOrCreateResources()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerSurvivalResources resources = player != null
            ? player.GetComponent<PlayerSurvivalResources>()
            : UnityEngine.Object.FindAnyObjectByType<PlayerSurvivalResources>();

        if (resources == null && player != null)
        {
            resources = Undo.AddComponent<PlayerSurvivalResources>(player);
        }

        if (resources != null)
        {
            Undo.RecordObject(resources, "Configure player resources");
            resources.maxRegolith = 10;
            resources.maxRegolithAmount = 10f;
            resources.maxConcrete = 20;
            resources.maxStoredOxygen = Mathf.Max(100f, resources.maxStoredOxygen);
            resources.maxStoredO2 = resources.maxStoredOxygen;
            EditorUtility.SetDirty(resources);
        }

        return resources;
    }

    private static SolarPowerState FindSolarPowerState()
    {
        GameObject root = GameObject.Find("SolarPanelCleaningRoot");
        if (root != null)
        {
            SolarPowerState state = root.GetComponentInChildren<SolarPowerState>(true);
            if (state != null)
            {
                return state;
            }
        }

        return UnityEngine.Object.FindAnyObjectByType<SolarPowerState>();
    }

    private static void EnsurePowerSync(PlayerSurvivalResources resources, SolarPowerState solarPowerState)
    {
        if (resources == null)
        {
            return;
        }

        SolarPowerResourceSync sync = resources.GetComponent<SolarPowerResourceSync>();
        if (sync == null)
        {
            sync = Undo.AddComponent<SolarPowerResourceSync>(resources.gameObject);
        }
        else
        {
            Undo.RecordObject(sync, "Configure solar power resource sync");
        }

        sync.resources = resources;
        sync.solarPowerState = solarPowerState;
        EditorUtility.SetDirty(sync);
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
        EnsureConcreteHudText(hud);
        return hud;
    }

    private static void EnsureConcreteHudText(SurvivalHUD hud)
    {
        if (hud == null)
        {
            return;
        }

        Transform resourcePanel = FindChildByName(hud.transform, "ResourcePanel");
        if (resourcePanel == null)
        {
            return;
        }

        Transform concrete = resourcePanel.Find("ConcreteText");
        Component concreteComponent;
        if (concrete == null)
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

            TMP_Text tmp = Undo.AddComponent<TextMeshProUGUI>(textObject);
            tmp.text = "Concrete: 0 / 20";
            tmp.fontSize = 24f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.raycastTarget = false;
            concreteComponent = tmp;

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
            concreteComponent = concrete.GetComponent<TMP_Text>() as Component;
        }

        hud.BindConcreteText(concreteComponent);
        EditorUtility.SetDirty(hud);
    }

    private static GameObject FindSceneObject(string exactName)
    {
        GameObject found = GameObject.Find(exactName);
        if (found != null)
        {
            return found;
        }

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Equals(exactName, StringComparison.OrdinalIgnoreCase))
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
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
