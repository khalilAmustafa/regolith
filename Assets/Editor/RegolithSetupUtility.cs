using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RegolithSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Resource Collection";
    private const string RegolithLayerName = "Regolith";
    private const string GatherTriggerName = "Gather";
    private const string GatherStateName = "Gathering Objects";
    private const float GatherFallbackDuration = 1.2f;

    private static readonly string[] NameTerms =
    {
        "regolith",
        "rock",
        "ore",
        "resource"
    };

    [MenuItem(MenuPath)]
    public static void SetupRegolithCollection()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Regolith setup cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        int regolithLayer = EnsureLayer(RegolithLayerName);
        List<GameObject> targets = FindTargets();
        int configuredResources = 0;

        foreach (GameObject target in targets)
        {
            if (ConfigureResource(target, regolithLayer))
            {
                configuredResources++;
            }
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            PlayerSurvivalResources resources = UnityEngine.Object.FindAnyObjectByType<PlayerSurvivalResources>();
            player = resources != null ? resources.gameObject : null;
        }

        bool configuredPlayer = ConfigurePlayer(player, regolithLayer);
        DisableOldManagerIfPresent();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Regolith resource collection setup complete:\n" +
            "- Regolith layer: " + (regolithLayer >= 0 ? regolithLayer.ToString() : "not available") + "\n" +
            "- Resource objects configured: " + configuredResources + "\n" +
            "- Player configured: " + (configuredPlayer ? "yes" : "no") + "\n" +
            "- Terrain modified: no");
    }

    private static List<GameObject> FindTargets()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected != null && selected.Length > 0)
        {
            return selected
                .Where(IsRegolithCandidate)
                .Distinct()
                .ToList();
        }

        return UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
            .Where(IsRegolithCandidate)
            .Distinct()
            .ToList();
    }

    private static bool IsRegolithCandidate(GameObject target)
    {
        if (target == null || target.GetComponent<Terrain>() != null || target.GetComponent<TerrainCollider>() != null)
        {
            return false;
        }

        if (target.GetComponentInParent<Terrain>() != null)
        {
            return false;
        }

        if (target.GetComponentsInChildren<Renderer>(true).Length == 0)
        {
            return false;
        }

        string lowerName = target.name.ToLowerInvariant();
        return NameTerms.Any(term => lowerName.Contains(term));
    }

    private static bool ConfigureResource(GameObject target, int regolithLayer)
    {
        Undo.RecordObject(target, "Configure Regolith resource object");

        RegolithResource resource = target.GetComponent<RegolithResource>();
        if (resource == null)
        {
            resource = Undo.AddComponent<RegolithResource>(target);
        }
        else
        {
            Undo.RecordObject(resource, "Configure Regolith resource");
        }

        Renderer[] renderers = GetResourceRenderers(target);
        resource.AssignRenderers(renderers);
        SetSerializedInt(resource, "amount", Mathf.Max(1, GetSerializedInt(resource, "amount", 1)));
        SetSerializedColor(resource, "highlightColor", Color.blue);

        Collider collider = target.GetComponent<Collider>();
        if (collider == null)
        {
            MeshFilter meshFilter = target.GetComponentInChildren<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(target);
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = true;
                collider = meshCollider;
            }
            else
            {
                BoxCollider boxCollider = Undo.AddComponent<BoxCollider>(target);
                Bounds bounds = CalculateRendererBounds(target);
                boxCollider.center = target.transform.InverseTransformPoint(bounds.center);
                Vector3 localSize = target.transform.InverseTransformVector(bounds.size);
                localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
                boxCollider.size = Vector3.Max(Vector3.one * 0.25f, localSize);
                collider = boxCollider;
            }
        }
        else
        {
            Undo.RecordObject(collider, "Configure Regolith resource collider");
        }

        collider.isTrigger = false;

        if (regolithLayer >= 0)
        {
            SetLayerRecursively(target, regolithLayer);
        }

        EditorUtility.SetDirty(resource);
        EditorUtility.SetDirty(collider);
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool ConfigurePlayer(GameObject player, int regolithLayer)
    {
        if (player == null)
        {
            Debug.LogWarning("Regolith setup: no Player object found. Add PlayerInventory and RegolithCollector to the player manually.");
            return false;
        }

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            inventory = Undo.AddComponent<PlayerInventory>(player);
        }

        RegolithCollector collector = player.GetComponent<RegolithCollector>();
        if (collector == null)
        {
            collector = Undo.AddComponent<RegolithCollector>(player);
        }
        else
        {
            Undo.RecordObject(collector, "Configure Regolith collector");
        }

        float currentRadius = GetSerializedFloat(collector, "collectRadius", 3f);
        collector.Configure(Mathf.Max(3f, currentRadius), KeyCode.E, regolithLayer >= 0 ? 1 << regolithLayer : ~0);

        PlayerGatherAnimationController gatherController = EnsureGatherController(player);
        Animator animator = player.GetComponentInChildren<Animator>(true);
        collector.ConfigureGatherAnimation(
            gatherController,
            animator,
            GatherTriggerName,
            GatherStateName,
            GatherFallbackDuration,
            true,
            true);

        EditorUtility.SetDirty(inventory);
        EditorUtility.SetDirty(collector);
        EditorUtility.SetDirty(player);
        return true;
    }

    private static PlayerGatherAnimationController EnsureGatherController(GameObject player)
    {
        PlayerGatherAnimationController gatherController = player.GetComponent<PlayerGatherAnimationController>();
        if (gatherController == null)
        {
            gatherController = Undo.AddComponent<PlayerGatherAnimationController>(player);
        }
        else
        {
            Undo.RecordObject(gatherController, "Configure player gather animation controller");
        }

        gatherController.animator = player.GetComponentInChildren<Animator>(true);
        gatherController.playerControllerToDisable = player.GetComponent<ThirdPersonPlayerController>();
        gatherController.rootMotionRelayToDisable = player.GetComponentInChildren<RootMotionRelay>(true);
        gatherController.playerRigidbody = player.GetComponent<Rigidbody>();
        gatherController.gatherTriggerName = GatherTriggerName;
        gatherController.gatherStateName = GatherStateName;
        gatherController.fallbackDuration = GatherFallbackDuration;
        gatherController.useImportedGatherAnimation = true;
        gatherController.lockRootTransformDuringGather = true;

        EditorUtility.SetDirty(gatherController);
        return gatherController;
    }

    private static void DisableOldManagerIfPresent()
    {
        RegolithCollectionManager manager = UnityEngine.Object.FindAnyObjectByType<RegolithCollectionManager>(FindObjectsInactive.Include);
        if (manager == null || !manager.enabled)
        {
            return;
        }

        Undo.RecordObject(manager, "Disable old Regolith collection manager");
        manager.enabled = false;
        EditorUtility.SetDirty(manager);
        Debug.Log("Regolith setup: disabled existing RegolithCollectionManager to prevent double E-key collection.");
    }

    private static Renderer[] GetResourceRenderers(GameObject target)
    {
        return target.GetComponentsInChildren<Renderer>(true)
            .Where(renderer =>
            {
                string lowerName = renderer.gameObject.name.ToLowerInvariant();
                return !lowerName.Contains("highlight")
                    && !lowerName.Contains("collect_ring")
                    && !lowerName.Contains("outline")
                    && !lowerName.Contains("selection");
            })
            .ToArray();
    }

    private static Bounds CalculateRendererBounds(GameObject target)
    {
        Renderer[] renderers = GetResourceRenderers(target);
        if (renderers.Length == 0)
        {
            return new Bounds(target.transform.position, Vector3.one);
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

        Debug.LogWarning("Regolith setup: no empty Unity layer slot is available. Collector will scan all layers.");
        return -1;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
        {
            Undo.RecordObject(child.gameObject, "Assign Regolith layer");
            child.gameObject.layer = layer;
            EditorUtility.SetDirty(child.gameObject);
        }
    }

    private static int GetSerializedInt(UnityEngine.Object target, string propertyName, int fallback)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.intValue : fallback;
    }

    private static float GetSerializedFloat(UnityEngine.Object target, string propertyName, float fallback)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.floatValue : fallback;
    }

    private static void SetSerializedInt(UnityEngine.Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedColor(UnityEngine.Object target, string propertyName, Color value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.colorValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
