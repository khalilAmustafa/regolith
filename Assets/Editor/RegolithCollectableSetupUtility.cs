using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RegolithCollectableSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Regolith Collectibles";
    [MenuItem(MenuPath)]
    public static void SetupRegolithCollectibles()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Regolith collectible setup cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        GameObject regolithRoad = GameObject.Find("Regolith_Road");
        if (regolithRoad == null)
        {
            Debug.LogError("Regolith collectible setup: no scene object named Regolith_Road was found.");
            return;
        }

        List<Transform> collectibleRoots = FindCollectibleRoots(regolithRoad.transform);
        int configured = 0;

        foreach (Transform root in collectibleRoots)
        {
            if (root == null || root == regolithRoad.transform)
            {
                continue;
            }

            ConfigureCollectible(root.gameObject);
            configured++;
        }

        RegolithCollectionManager manager = FindOrCreateManager();
        ConfigureManager(manager, regolithRoad.transform);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(
            "Regolith collectible setup complete:\n" +
            "- Regolith_Road found: yes\n" +
            "- Collectibles configured/reused: " + configured + "\n" +
            "- Manager assigned: " + (manager != null ? "yes" : "no") + "\n" +
            "- Prompt UI assigned: " + (manager != null && manager.promptUI != null ? "yes" : "no"));
    }

    private static List<Transform> FindCollectibleRoots(Transform regolithParent)
    {
        HashSet<Transform> roots = new HashSet<Transform>();

        foreach (Transform child in regolithParent)
        {
            if (child.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                roots.Add(child);
            }
        }

        Transform[] allChildren = regolithParent.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child == regolithParent || !child.name.StartsWith("Regolith_Rubble"))
            {
                continue;
            }

            if (child.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                roots.Add(child);
            }
        }

        if (roots.Count == 0)
        {
            Renderer[] renderers = regolithParent.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                roots.Add(renderer.transform);
            }
        }

        return new List<Transform>(roots);
    }

    private static void ConfigureCollectible(GameObject target)
    {
        Undo.RecordObject(target, "Configure regolith collectible");

        RegolithCollectable collectable = target.GetComponent<RegolithCollectable>();
        if (collectable == null)
        {
            collectable = Undo.AddComponent<RegolithCollectable>(target);
        }
        else
        {
            Undo.RecordObject(collectable, "Configure regolith collectible");
        }

        collectable.visualRoot = target;
        DisableUglyHighlightRenderers(target);
        collectable.renderers = GetCollectibleRenderers(target);
        collectable.cachedRenderers = collectable.renderers;
        collectable.amount = Mathf.Max(1, collectable.amount);
        collectable.deactivateOnCollect = true;
        collectable.normalTint = Color.white;
        collectable.highlightedTint = new Color(0.49f, 0.73f, 1f, 1f);
        collectable.useBlueTintHighlight = true;
        collectable.highlightEmissionIntensity = 0.65f;

        Collider collider = target.GetComponent<Collider>();
        if (collider == null)
        {
            SphereCollider sphere = Undo.AddComponent<SphereCollider>(target);
            Bounds bounds = CalculateRendererBounds(target);
            sphere.center = target.transform.InverseTransformPoint(bounds.center);
            sphere.radius = Mathf.Max(0.75f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            collider = sphere;
        }
        else
        {
            Undo.RecordObject(collider, "Configure regolith trigger collider");
        }

        collider.isTrigger = true;
        collectable.triggerCollider = collider;
        collectable.highlightObject = null;
        collectable.meshOutlineObject = null;
        collectable.Initialize();
        collectable.SetHighlighted(false);

        EditorUtility.SetDirty(collider);
        EditorUtility.SetDirty(collectable);
        EditorUtility.SetDirty(target);
    }

    private static void DisableUglyHighlightRenderers(GameObject target)
    {
        Renderer[] childRenderers = target.GetComponentsInChildren<Renderer>(true);
        Bounds rubbleBounds = CalculateRendererBounds(target);
        float rubbleMaxSize = Mathf.Max(0.01f, Mathf.Max(rubbleBounds.size.x, rubbleBounds.size.y, rubbleBounds.size.z));

        foreach (Renderer childRenderer in childRenderers)
        {
            if (childRenderer == null || childRenderer.gameObject == target)
            {
                continue;
            }

            string lowerName = childRenderer.gameObject.name.ToLowerInvariant();
            bool suspiciousName =
                lowerName.Contains("highlight")
                || lowerName.Contains("selection")
                || lowerName.Contains("glow")
                || lowerName.Contains("ring")
                || lowerName.Contains("outline")
                || lowerName.Contains("billboard");

            bool oversized = Mathf.Max(childRenderer.bounds.size.x, childRenderer.bounds.size.y, childRenderer.bounds.size.z) > rubbleMaxSize * 1.2f;

            if (suspiciousName || oversized)
            {
                Undo.RecordObject(childRenderer, "Disable ugly regolith highlight renderer");
                childRenderer.enabled = false;
                EditorUtility.SetDirty(childRenderer);
                if (suspiciousName)
                {
                    Undo.RecordObject(childRenderer.gameObject, "Disable old regolith highlight object");
                    childRenderer.gameObject.SetActive(false);
                    EditorUtility.SetDirty(childRenderer.gameObject);
                }
            }
        }
    }

    private static Renderer[] GetCollectibleRenderers(GameObject target)
    {
        List<Renderer> result = new List<Renderer>();
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            string lowerName = renderer.gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("highlight") || lowerName.Contains("collect_ring") || lowerName.Contains("outline") || lowerName.Contains("selection"))
            {
                continue;
            }

            result.Add(renderer);
        }

        return result.ToArray();
    }

    private static RegolithCollectionManager FindOrCreateManager()
    {
        RegolithCollectionManager manager = Object.FindAnyObjectByType<RegolithCollectionManager>();
        if (manager != null)
        {
            return manager;
        }

        GameObject managerObject = new GameObject("RegolithCollectionManager");
        Undo.RegisterCreatedObjectUndo(managerObject, "Create regolith collection manager");
        return Undo.AddComponent<RegolithCollectionManager>(managerObject);
    }

    private static void ConfigureManager(RegolithCollectionManager manager, Transform regolithParent)
    {
        if (manager == null)
        {
            return;
        }

        Undo.RecordObject(manager, "Configure regolith collection manager");

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        manager.player = playerObject != null ? playerObject.transform : null;
        manager.resources = manager.player != null
            ? manager.player.GetComponent<PlayerSurvivalResources>()
            : Object.FindAnyObjectByType<PlayerSurvivalResources>();
        manager.regolithParent = regolithParent;
        manager.detectionRadius = Mathf.Max(manager.detectionRadius, 2.5f);
        manager.interactKey = KeyCode.E;

        PromptRefs promptRefs = EnsurePromptUI();
        manager.promptUI = promptRefs.Root;
        manager.promptText = promptRefs.Text;
        manager.promptMessage = "Press E\nCollect regolith";

        if (manager.promptUI != null)
        {
            manager.promptUI.SetActive(false);
        }

        EditorUtility.SetDirty(manager);
    }

    private static PromptRefs EnsurePromptUI()
    {
        GameObject existingRoot = GameObject.Find("RegolithCollectPromptUI");
        if (existingRoot != null)
        {
            return new PromptRefs
            {
                Root = existingRoot,
                Text = existingRoot.GetComponentInChildren<TMP_Text>(true)
            };
        }

        GameObject canvasObject = new GameObject("RegolithCollectPromptUI");
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create regolith prompt UI");

        Canvas canvas = Undo.AddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Undo.AddComponent<CanvasScaler>(canvasObject);
        Undo.AddComponent<GraphicRaycaster>(canvasObject);

        GameObject panelObject = new GameObject("PromptPanel");
        Undo.RegisterCreatedObjectUndo(panelObject, "Create regolith prompt panel");
        panelObject.transform.SetParent(canvasObject.transform, false);

        Image panel = Undo.AddComponent<Image>(panelObject);
        panel.color = new Color(0f, 0f, 0f, 0.55f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 90f);
        panelRect.sizeDelta = new Vector2(260f, 74f);

        GameObject textObject = new GameObject("PromptText");
        Undo.RegisterCreatedObjectUndo(textObject, "Create regolith prompt text");
        textObject.transform.SetParent(panelObject.transform, false);

        TextMeshProUGUI text = Undo.AddComponent<TextMeshProUGUI>(textObject);
        text.text = "Press E\nCollect regolith";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24f;
        text.color = Color.white;
        text.raycastTarget = false;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        canvasObject.SetActive(false);
        return new PromptRefs { Root = canvasObject, Text = text };
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(root.transform.position, Vector3.one);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            string lowerName = renderer.gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("highlight") || lowerName.Contains("collect_ring") || lowerName.Contains("outline") || lowerName.Contains("selection"))
            {
                continue;
            }

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

    private static void EnsureAssetFolder(string folderPath)
    {
        string normalized = folderPath.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private struct PromptRefs
    {
        public GameObject Root;
        public TMP_Text Text;
    }
}
