using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomTradeStationSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Room Trade Stations";
    private const string TradeStationLayerName = "TradeStation";
    private const string AutoSetupTriggerPath = "Assets/Editor/RoomTradeStationSetupTrigger.txt";

    [InitializeOnLoadMethod]
    private static void RunTriggeredSetup()
    {
        if (AssetDatabase.LoadAssetAtPath<TextAsset>(AutoSetupTriggerPath) == null)
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<TextAsset>(AutoSetupTriggerPath) == null)
            {
                return;
            }

            AssetDatabase.DeleteAsset(AutoSetupTriggerPath);
            SetupRoomTradeStations();
        };
    }

    [MenuItem(MenuPath)]
    public static void SetupRoomTradeStations()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Room trade station setup cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        int stationLayer = EnsureLayer(TradeStationLayerName);
        BasePowerSystem powerSystem = FindOrCreatePowerSystem();
        SolarPowerState solarPowerState = UnityEngine.Object.FindAnyObjectByType<SolarPowerState>(FindObjectsInactive.Include);
        SolarPanelCleaningMinigame cleaningMinigame = UnityEngine.Object.FindAnyObjectByType<SolarPanelCleaningMinigame>(FindObjectsInactive.Include);
        if (solarPowerState != null)
        {
            Undo.RecordObject(solarPowerState, "Configure solar cleaning power gate");
            solarPowerState.requiredCleanPercent = 0.5f;
            solarPowerState.autoPowerOnlineWhenClean = false;
            EditorUtility.SetDirty(solarPowerState);
        }

        List<string> configured = new List<string>();
        GameObject room02 = FindSceneObject("Room_02", "02 Oxygen Processor", "Oxygen Processor", "O2");
        if (room02 != null)
        {
            RoomTradeStation station = ConfigureStationTrigger(
                room02,
                "O2Processor_TradeStation",
                "O2 Processor",
                StationType.OxygenProcessor,
                3,
                10,
                false,
                stationLayer);
            configured.Add(station != null ? GetPath(station.transform) : "O2 Processor failed");
        }
        else
        {
            Debug.LogWarning("Room trade station setup: visible O2 room was not found. Expected Room_02 or Oxygen/O2 object.");
        }

        GameObject room03 = FindSceneObject("Room_03", "03 Power Station", "Power Station", "Power");
        if (room03 != null)
        {
            RoomTradeStation station = ConfigureStationTrigger(
                room03,
                "RegolithPanel_TradeStation",
                "Regolith Panel",
                StationType.RegolithPanel,
                5,
                1,
                false,
                stationLayer);
            configured.Add(station != null ? GetPath(station.transform) : "Regolith Panel failed");
        }
        else
        {
            Debug.LogWarning("Room trade station setup: visible Regolith Panel room was not found. Expected Room_03 or old Power Station object.");
        }

        bool playerConfigured = ConfigurePlayerInteractor(stationLayer, powerSystem);
        SolarGeneratorPowerRestorer generatorRestorer = ConfigureSolarGeneratorPowerRestore(powerSystem, solarPowerState, cleaningMinigame);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Room trade station setup complete:\n" +
            "- TradeStation layer: " + (stationLayer >= 0 ? stationLayer.ToString() : "not available") + "\n" +
            "- Configured station objects:\n  " + (configured.Count > 0 ? string.Join("\n  ", configured) : "none") + "\n" +
            "- PlayerStationInteractor configured: " + (playerConfigured ? "yes" : "no") + "\n" +
            "- BasePowerSystem configured: " + (powerSystem != null ? GetPath(powerSystem.transform) : "no") + "\n" +
            "- Solar generator power restore: " + (generatorRestorer != null ? GetPath(generatorRestorer.transform) : "no") + "\n" +
            "- Terrain modified: no\n" +
            "- Recipes: O2 Processor = 3 Regolith -> 10 O2, Regolith Panel = 5 Regolith -> 1 Concrete, Generator = restore power after 50% solar cleaning");
    }

    private static RoomTradeStation ConfigureStationTrigger(
        GameObject visibleRoom,
        string triggerName,
        string stationName,
        StationType stationType,
        int requiredRegolith,
        int outputAmount,
        bool requiresPower,
        int stationLayer)
    {
        GameObject triggerObject = EnsureTriggerChild(visibleRoom, triggerName);
        Undo.RecordObject(triggerObject, "Configure room trade station trigger");

        RoomTradeStation station = triggerObject.GetComponent<RoomTradeStation>();
        if (station == null)
        {
            station = Undo.AddComponent<RoomTradeStation>(triggerObject);
        }
        else
        {
            Undo.RecordObject(station, "Configure room trade station");
        }

        station.Configure(stationName, stationType, requiredRegolith, outputAmount, requiresPower);
        station.AssignHighlightRenderers(SelectHighlightRenderers(visibleRoom));
        station.SetPromptPoint(triggerObject.transform);

        BoxCollider trigger = triggerObject.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BoxCollider>(triggerObject);
        }
        else
        {
            Undo.RecordObject(trigger, "Configure room trade station collider");
        }

        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
        trigger.size = new Vector3(1.75f, 2f, 1.75f);

        if (stationLayer >= 0)
        {
            SetLayerRecursively(triggerObject, stationLayer);
        }

        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(station);
        EditorUtility.SetDirty(triggerObject);
        return station;
    }

    private static GameObject EnsureTriggerChild(GameObject visibleRoom, string triggerName)
    {
        Transform existing = visibleRoom.transform.Find(triggerName);
        if (existing == null && triggerName == "RegolithPanel_TradeStation")
        {
            existing = visibleRoom.transform.Find("PowerStation_TradeStation");
            if (existing != null)
            {
                Undo.RecordObject(existing.gameObject, "Rename old power station trade trigger");
                existing.name = triggerName;
                EditorUtility.SetDirty(existing.gameObject);
            }
        }

        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return existing.gameObject;
        }

        GameObject triggerObject = new GameObject(triggerName);
        Undo.RegisterCreatedObjectUndo(triggerObject, "Create room trade station trigger");
        triggerObject.transform.SetParent(visibleRoom.transform, false);
        triggerObject.transform.position = EstimateEntrancePosition(visibleRoom);
        triggerObject.transform.rotation = visibleRoom.transform.rotation;
        triggerObject.transform.localScale = Vector3.one;
        return triggerObject;
    }

    private static Vector3 EstimateEntrancePosition(GameObject visibleRoom)
    {
        Bounds bounds = CalculateRendererBounds(visibleRoom);
        Vector3 center = bounds.size == Vector3.zero ? visibleRoom.transform.position : bounds.center;
        Vector3 forward = visibleRoom.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }

        float radius = Mathf.Max(2f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.65f);
        return center + forward.normalized * radius + Vector3.up * 1.2f;
    }

    private static bool ConfigurePlayerInteractor(int stationLayer, BasePowerSystem powerSystem)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            PlayerInventory inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
            player = inventory != null ? inventory.gameObject : null;
        }

        if (player == null)
        {
            Debug.LogWarning("Room trade station setup: no Player object found. Add PlayerStationInteractor to the player manually.");
            return false;
        }

        PlayerInventory playerInventory = player.GetComponent<PlayerInventory>();
        if (playerInventory == null)
        {
            playerInventory = Undo.AddComponent<PlayerInventory>(player);
        }

        PlayerStationInteractor interactor = player.GetComponent<PlayerStationInteractor>();
        if (interactor == null)
        {
            interactor = Undo.AddComponent<PlayerStationInteractor>(player);
        }
        else
        {
            Undo.RecordObject(interactor, "Configure player station interactor");
        }

        PlayerRoomTrader oldTrader = player.GetComponent<PlayerRoomTrader>();
        if (oldTrader != null && oldTrader.enabled)
        {
            Undo.RecordObject(oldTrader, "Disable old player room trader");
            oldTrader.enabled = false;
            EditorUtility.SetDirty(oldTrader);
        }

        interactor.Configure(1.75f, KeyCode.E, stationLayer >= 0 ? 1 << stationLayer : ~0, null, powerSystem);

        EditorUtility.SetDirty(playerInventory);
        EditorUtility.SetDirty(interactor);
        EditorUtility.SetDirty(player);
        return true;
    }

    private static SolarGeneratorPowerRestorer ConfigureSolarGeneratorPowerRestore(
        BasePowerSystem powerSystem,
        SolarPowerState solarPowerState,
        SolarPanelCleaningMinigame cleaningMinigame)
    {
        GameObject generator = FindSceneObject("Generator_Module_01", "SolarGenerator_PowerRestore", "Generator");
        if (generator == null)
        {
            GameObject solar = FindSceneObject("SolarArray_01", "SolarPanelCleaningRoot", "Solar Array");
            generator = new GameObject("SolarGenerator_PowerRestore");
            Undo.RegisterCreatedObjectUndo(generator, "Create solar generator power restore object");
            generator.transform.position = solar != null ? solar.transform.position + solar.transform.right * 2f + Vector3.up * 1f : Vector3.up;
            generator.transform.rotation = solar != null ? solar.transform.rotation : Quaternion.identity;
        }

        SolarGeneratorPowerRestorer restorer = generator.GetComponent<SolarGeneratorPowerRestorer>();
        if (restorer == null)
        {
            restorer = Undo.AddComponent<SolarGeneratorPowerRestorer>(generator);
        }
        else
        {
            Undo.RecordObject(restorer, "Configure solar generator power restorer");
        }

        Transform interactPoint = EnsureGeneratorInteractPoint(generator);
        restorer.Configure(50f, KeyCode.E, 1.75f, interactPoint, powerSystem, solarPowerState, cleaningMinigame);
        restorer.AssignHighlightRenderers(SelectHighlightRenderers(generator));

        BoxCollider trigger = generator.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BoxCollider>(generator);
        }
        else
        {
            Undo.RecordObject(trigger, "Configure solar generator interaction trigger");
        }

        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
        trigger.size = new Vector3(1.75f, 2f, 1.75f);

        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(restorer);
        EditorUtility.SetDirty(generator);
        return restorer;
    }

    private static Transform EnsureGeneratorInteractPoint(GameObject generator)
    {
        Transform existing = generator.transform.Find("PowerRestoreInteractPoint");
        if (existing != null)
        {
            return existing;
        }

        GameObject interactPoint = new GameObject("PowerRestoreInteractPoint");
        Undo.RegisterCreatedObjectUndo(interactPoint, "Create generator power restore interact point");
        interactPoint.transform.SetParent(generator.transform, false);
        interactPoint.transform.localPosition = Vector3.up * 1f;
        interactPoint.transform.localRotation = Quaternion.identity;
        interactPoint.transform.localScale = Vector3.one;
        return interactPoint.transform;
    }

    private static BasePowerSystem FindOrCreatePowerSystem()
    {
        BasePowerSystem existing = UnityEngine.Object.FindAnyObjectByType<BasePowerSystem>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return existing;
        }

        SolarPowerState solarPowerState = UnityEngine.Object.FindAnyObjectByType<SolarPowerState>(FindObjectsInactive.Include);
        GameObject host = solarPowerState != null ? solarPowerState.gameObject : new GameObject("BasePowerSystem");
        if (solarPowerState == null)
        {
            Undo.RegisterCreatedObjectUndo(host, "Create BasePowerSystem");
        }

        return Undo.AddComponent<BasePowerSystem>(host);
    }

    private static GameObject FindConcreteStationObject(GameObject room02, GameObject room03)
    {
        foreach (GameObject candidate in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (candidate == null || candidate == room02 || candidate == room03 || candidate.GetComponent<Terrain>() != null || candidate.GetComponentInParent<Terrain>() != null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            bool isConcrete = lowerName.Contains("concrete")
                || lowerName.Contains("patch")
                || lowerName.Contains("patching")
                || lowerName.Contains("repair");
            bool isClearStation = lowerName.Contains("room")
                || lowerName.Contains("station")
                || lowerName.Contains("processor");
            if (isConcrete && isClearStation && candidate.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                return candidate;
            }
        }

        return null;
    }

    private static GameObject FindSceneObject(params string[] nameTerms)
    {
        foreach (string exactName in nameTerms)
        {
            GameObject exact = GameObject.Find(exactName);
            if (exact != null)
            {
                return exact;
            }
        }

        foreach (GameObject candidate in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (candidate == null || candidate.GetComponent<Terrain>() != null || candidate.GetComponentInParent<Terrain>() != null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            if (nameTerms.Any(term => lowerName.Contains(term.ToLowerInvariant())) && candidate.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                return candidate;
            }
        }

        return null;
    }

    private static Renderer[] SelectHighlightRenderers(GameObject visibleRoom)
    {
        Renderer[] renderers = visibleRoom.GetComponentsInChildren<Renderer>(true)
            .Where(renderer =>
            {
                string lowerName = renderer.gameObject.name.ToLowerInvariant();
                return !lowerName.Contains("trade")
                    && !lowerName.Contains("trigger")
                    && !lowerName.Contains("highlight")
                    && !lowerName.Contains("outline")
                    && !lowerName.Contains("selection");
            })
            .ToArray();

        Renderer[] preferred = renderers
            .Where(renderer =>
            {
                string lowerName = renderer.gameObject.name.ToLowerInvariant();
                return lowerName.Contains("door")
                    || lowerName.Contains("sign")
                    || lowerName.Contains("panel")
                    || lowerName.Contains("processor")
                    || lowerName.Contains("frame");
            })
            .Take(4)
            .ToArray();

        if (preferred.Length > 0)
        {
            return preferred;
        }

        return renderers
            .OrderBy(renderer => renderer.bounds.size.sqrMagnitude)
            .Take(3)
            .ToArray();
    }

    private static Bounds CalculateRendererBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(target.transform.position, Vector3.one * 3f);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0)
        {
            return existing;
        }

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 7; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue))
            {
                continue;
            }

            layer.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return i;
        }

        Debug.LogWarning("Room trade station setup: no empty Unity layer slot is available. Station interactor will scan all layers.");
        return -1;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
        {
            Undo.RecordObject(child.gameObject, "Assign TradeStation layer");
            child.gameObject.layer = layer;
            EditorUtility.SetDirty(child.gameObject);
        }
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
