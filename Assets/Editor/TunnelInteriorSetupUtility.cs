using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TunnelInteriorSetupUtility
{
    private static readonly string[] TunnelNameHints = { "Tunnel", "tunnel", "corridor" };
    private static readonly string[] BlockerNameHints = { "blocker", "door", "doorway", "airlock", "collider" };
    private static readonly string[] LightSourceNameHints = { "object_9", "blue", "light", "emission", "emissive", "lamp" };
    private const string GlowMaterialPath = "Assets/_Materials/Tunnel_White_Glow.mat";

    [MenuItem("Tools/Regolith/Add Tunnel Interior Lights And Fix Entry")]
    public static void AddTunnelInteriorLightsAndFixEntry()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("Tunnel interior setup: no valid active scene.");
            return;
        }

        GameObject tunnel = FindTunnel();
        AirlockController airlock = Object.FindAnyObjectByType<AirlockController>();

        List<string> manualChecks = new List<string>();
        List<string> colliderChanges = new List<string>();
        int lightsReady = 0;
        bool tunnelRootColliderChanged = false;
        bool blockerAssigned = false;

        LogNearbyColliders(tunnel, airlock);

        if (tunnel == null)
        {
            manualChecks.Add("Tunnel_01 was not found in the active scene.");
        }
        else
        {
            lightsReady = EnsureTunnelLights(tunnel);
            tunnelRootColliderChanged = FixTunnelRootCollider(tunnel, colliderChanges);
        }

        if (airlock == null)
        {
            manualChecks.Add("AirlockController was not found in the active scene.");
        }
        else
        {
            Collider blocker = EnsureAirlockBlocker(airlock, tunnel, colliderChanges);
            blockerAssigned = blocker != null && airlock.blockerCollider == blocker;
            if (!blockerAssigned)
            {
                manualChecks.Add("AirlockController.blockerCollider is still unassigned.");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);

        StringBuilder summary = new StringBuilder();
        summary.AppendLine("Tunnel interior setup complete:");
        summary.AppendLine("- Tunnel found: " + ToYesNo(tunnel != null));
        summary.AppendLine("- Lights created/reused: " + lightsReady);
        summary.AppendLine("- Tunnel root collider changed: " + ToYesNo(tunnelRootColliderChanged));
        summary.AppendLine("- AirlockController found: " + ToYesNo(airlock != null));
        summary.AppendLine("- blockerCollider assigned: " + ToYesNo(blockerAssigned));
        summary.AppendLine("- Collider changes:");
        if (colliderChanges.Count == 0)
        {
            summary.AppendLine("  none");
        }
        else
        {
            foreach (string change in colliderChanges)
            {
                summary.AppendLine("  " + change);
            }
        }

        summary.AppendLine("- Manual checks needed:");
        if (manualChecks.Count == 0)
        {
            summary.AppendLine("  none");
        }
        else
        {
            foreach (string check in manualChecks)
            {
                summary.AppendLine("  " + check);
            }
        }

        Debug.Log(summary.ToString());
    }

    private static GameObject FindTunnel()
    {
        GameObject exact = GameObject.Find("Tunnel_01");
        if (exact != null)
        {
            return exact;
        }

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject candidate in allObjects)
        {
            foreach (string hint in TunnelNameHints)
            {
                if (candidate.name.Contains(hint))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static int EnsureTunnelLights(GameObject tunnel)
    {
        Bounds bounds = CalculateRendererBounds(tunnel);
        Color whiteLight = Color.white;
        Material glowMaterial = CreateOrUpdateGlowMaterial(whiteLight);
        List<Transform> modelLightSources = FindModelLightSources(tunnel);

        if (modelLightSources.Count > 0)
        {
            DisableOldGeneratedCenterLights(tunnel.transform);
            for (int i = 0; i < modelLightSources.Count; i++)
            {
                EnsureModelAnchoredLight(modelLightSources[i], i + 1, whiteLight, glowMaterial);
            }

            Debug.Log("Tunnel lighting: using model light mesh anchors, including Object_9.002 if present. Anchors found: " + modelLightSources.Count);
            return modelLightSources.Count;
        }

        float length = Mathf.Max(bounds.size.x, bounds.size.z);
        int lightCount = Mathf.Clamp(Mathf.RoundToInt(length / 4f), 3, 5);

        for (int i = 0; i < lightCount; i++)
        {
            string lightName = "Tunnel_WarmBlueLight_" + (i + 1).ToString("00");
            Transform existing = tunnel.transform.Find(lightName);
            GameObject lightObject = existing != null ? existing.gameObject : new GameObject(lightName);

            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(lightObject, "Create tunnel guide light");
                lightObject.transform.SetParent(tunnel.transform, false);
            }
            else
            {
                Undo.RecordObject(lightObject.transform, "Update tunnel guide light");
            }

            Light light = lightObject.GetComponent<Light>();
            if (light == null)
            {
                light = Undo.AddComponent<Light>(lightObject);
            }
            else
            {
                Undo.RecordObject(light, "Update tunnel guide light");
            }

            float t = lightCount == 1 ? 0.5f : i / (float)(lightCount - 1);
            Vector3 localPosition = GetTunnelLightLocalPosition(t, bounds, tunnel.transform);
            lightObject.transform.localPosition = localPosition;
            lightObject.transform.localRotation = Quaternion.identity;
            lightObject.transform.localScale = Vector3.one;

            light.enabled = true;
            light.type = LightType.Point;
            light.color = whiteLight;
            light.intensity = 4.5f;
            light.range = 8f;
            light.bounceIntensity = 1f;
            light.cullingMask = ~0;
            light.renderMode = LightRenderMode.ForcePixel;
            light.shadows = LightShadows.None;

            EnsureVisibleLightFixture(tunnel.transform, lightName, localPosition, glowMaterial);

            EditorUtility.SetDirty(lightObject);
            EditorUtility.SetDirty(light);
        }

        return lightCount;
    }

    private static List<Transform> FindModelLightSources(GameObject tunnel)
    {
        List<Transform> exact = new List<Transform>();
        List<Transform> hinted = new List<Transform>();
        Transform[] children = tunnel.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child == tunnel.transform)
            {
                continue;
            }

            string lowerName = child.name.ToLowerInvariant();
            if (lowerName == "object_9.002")
            {
                exact.Add(child);
                continue;
            }

            if (lowerName.StartsWith("object_9") || ContainsAny(lowerName, LightSourceNameHints))
            {
                if (child.GetComponent<Renderer>() != null || child.childCount == 0)
                {
                    hinted.Add(child);
                }
            }
        }

        exact.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        hinted.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        List<Transform> result = new List<Transform>();
        foreach (Transform item in exact)
        {
            AddUnique(result, item);
        }

        foreach (Transform item in hinted)
        {
            AddUnique(result, item);
        }

        return result;
    }

    private static void AddUnique(List<Transform> list, Transform item)
    {
        if (item != null && !list.Contains(item))
        {
            list.Add(item);
        }
    }

    private static void EnsureModelAnchoredLight(Transform source, int index, Color warmBlue, Material glowMaterial)
    {
        Renderer sourceRenderer = source.GetComponent<Renderer>();
        Vector3 localLightPosition = Vector3.zero;
        if (sourceRenderer != null)
        {
            localLightPosition = source.InverseTransformPoint(sourceRenderer.bounds.center);
            AssignGlowMaterial(sourceRenderer, glowMaterial);
        }

        string lightName = "Tunnel_SourcePointLight_" + index.ToString("00");
        Transform existing = source.Find(lightName);
        GameObject lightObject = existing != null ? existing.gameObject : new GameObject(lightName);
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(lightObject, "Create source anchored tunnel light");
            lightObject.transform.SetParent(source, false);
        }
        else
        {
            Undo.RecordObject(lightObject.transform, "Update source anchored tunnel light");
        }

        lightObject.transform.localPosition = localLightPosition;
        lightObject.transform.localRotation = Quaternion.identity;
        lightObject.transform.localScale = Vector3.one;

        Light light = lightObject.GetComponent<Light>();
        if (light == null)
        {
            light = Undo.AddComponent<Light>(lightObject);
        }
        else
        {
            Undo.RecordObject(light, "Update source anchored tunnel light");
        }

        light.enabled = true;
        light.type = LightType.Point;
        light.color = warmBlue;
        light.intensity = source.name == "Object_9.002" ? 7f : 5.5f;
        light.range = source.name == "Object_9.002" ? 10f : 8f;
        light.bounceIntensity = 1f;
        light.cullingMask = ~0;
        light.renderMode = LightRenderMode.ForcePixel;
        light.shadows = LightShadows.None;

        EditorUtility.SetDirty(lightObject);
        EditorUtility.SetDirty(light);
    }

    private static void AssignGlowMaterial(Renderer renderer, Material glowMaterial)
    {
        if (renderer == null || glowMaterial == null)
        {
            return;
        }

        Undo.RecordObject(renderer, "Assign source glow material");
        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            renderer.sharedMaterial = glowMaterial;
        }
        else
        {
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = glowMaterial;
            }

            renderer.sharedMaterials = materials;
        }

        EditorUtility.SetDirty(renderer);
    }

    private static void DisableOldGeneratedCenterLights(Transform tunnelRoot)
    {
        for (int i = 1; i <= 8; i++)
        {
            string lightName = "Tunnel_WarmBlueLight_" + i.ToString("00");
            Transform light = tunnelRoot.Find(lightName);
            if (light != null)
            {
                Undo.RecordObject(light.gameObject, "Disable old center tunnel light");
                light.gameObject.SetActive(false);
                EditorUtility.SetDirty(light.gameObject);
            }

            string emitterName = "Tunnel_WarmBlueEmitter_" + i.ToString("00");
            Transform emitter = tunnelRoot.Find(emitterName);
            if (emitter != null)
            {
                Undo.RecordObject(emitter.gameObject, "Disable old center tunnel emitter");
                emitter.gameObject.SetActive(false);
                EditorUtility.SetDirty(emitter.gameObject);
            }
        }
    }

    private static void EnsureVisibleLightFixture(Transform tunnelRoot, string lightName, Vector3 localPosition, Material glowMaterial)
    {
        string fixtureName = lightName.Replace("Light", "Emitter");
        Transform existing = tunnelRoot.Find(fixtureName);
        GameObject fixture = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);

        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(fixture, "Create tunnel emissive fixture");
            fixture.name = fixtureName;
            fixture.transform.SetParent(tunnelRoot, false);

            Collider fixtureCollider = fixture.GetComponent<Collider>();
            if (fixtureCollider != null)
            {
                Object.DestroyImmediate(fixtureCollider);
            }
        }
        else
        {
            Undo.RecordObject(fixture.transform, "Update tunnel emissive fixture");
        }

        fixture.transform.localPosition = localPosition;
        fixture.transform.localRotation = Quaternion.identity;
        fixture.transform.localScale = Vector3.one * 0.18f;

        Renderer renderer = fixture.GetComponent<Renderer>();
        if (renderer != null && glowMaterial != null)
        {
            Undo.RecordObject(renderer, "Assign tunnel glow material");
            renderer.sharedMaterial = glowMaterial;
            EditorUtility.SetDirty(renderer);
        }

        EditorUtility.SetDirty(fixture);
    }

    private static Material CreateOrUpdateGlowMaterial(Color glowColor)
    {
        EnsureAssetFolder("Assets/_Materials");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, GlowMaterialPath);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        Color emission = glowColor * 3.5f;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", glowColor);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", glowColor);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", emission);
        }

        material.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
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

    private static Vector3 GetTunnelLightLocalPosition(float t, Bounds worldBounds, Transform tunnelTransform)
    {
        Vector3 localCenter = tunnelTransform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = tunnelTransform.InverseTransformVector(worldBounds.size);
        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

        bool useLocalZ = localSize.z >= localSize.x;
        float length = Mathf.Max(useLocalZ ? localSize.z : localSize.x, 0.01f);
        float start = -length * 0.32f;
        float end = length * 0.32f;
        float along = Mathf.Lerp(start, end, t);

        Vector3 result = localCenter;
        result.y += Mathf.Max(localSize.y * 0.22f, 0.35f);
        if (useLocalZ)
        {
            result.z += along;
        }
        else
        {
            result.x += along;
        }

        return result;
    }

    private static bool FixTunnelRootCollider(GameObject tunnel, List<string> colliderChanges)
    {
        bool changed = false;
        Collider[] rootColliders = tunnel.GetComponents<Collider>();
        foreach (Collider collider in rootColliders)
        {
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            if (collider is BoxCollider || collider is MeshCollider)
            {
                Undo.RecordObject(collider, "Make tunnel root collider trigger");
                collider.isTrigger = true;
                EditorUtility.SetDirty(collider);
                colliderChanges.Add("- Set " + GetPath(collider.transform) + " " + collider.GetType().Name + " to isTrigger = true so the tunnel root does not block entry.");
                changed = true;
            }
        }

        return changed;
    }

    private static Collider EnsureAirlockBlocker(AirlockController airlock, GameObject tunnel, List<string> colliderChanges)
    {
        if (airlock.blockerCollider != null)
        {
            return airlock.blockerCollider;
        }

        Collider existing = FindExistingDoorBlocker(airlock, tunnel);
        if (existing != null)
        {
            Undo.RecordObject(airlock, "Assign airlock blocker collider");
            airlock.blockerCollider = existing;
            EditorUtility.SetDirty(airlock);
            colliderChanges.Add("- Assigned existing blocker collider: " + GetPath(existing.transform));
            return existing;
        }

        Transform existingBlockerTransform = airlock.transform.Find("Airlock_DoorBlocker");
        GameObject blockerObject = existingBlockerTransform != null
            ? existingBlockerTransform.gameObject
            : new GameObject("Airlock_DoorBlocker");

        if (existingBlockerTransform == null)
        {
            Undo.RegisterCreatedObjectUndo(blockerObject, "Create airlock door blocker");
            blockerObject.transform.SetParent(airlock.transform, false);
        }
        else
        {
            Undo.RecordObject(blockerObject.transform, "Update airlock door blocker");
        }

        blockerObject.transform.localPosition = ResolveDoorBlockerLocalPosition(airlock);
        blockerObject.transform.localRotation = Quaternion.identity;
        blockerObject.transform.localScale = Vector3.one;

        BoxCollider blocker = blockerObject.GetComponent<BoxCollider>();
        if (blocker == null)
        {
            blocker = Undo.AddComponent<BoxCollider>(blockerObject);
        }
        else
        {
            Undo.RecordObject(blocker, "Update airlock door blocker");
        }

        blocker.isTrigger = false;
        blocker.enabled = !airlock.isOpen;
        blocker.center = Vector3.zero;
        blocker.size = new Vector3(2.6f, 2.4f, 0.35f);

        Undo.RecordObject(airlock, "Assign airlock blocker collider");
        airlock.blockerCollider = blocker;

        EditorUtility.SetDirty(blockerObject);
        EditorUtility.SetDirty(blocker);
        EditorUtility.SetDirty(airlock);

        colliderChanges.Add("- Created/reused Airlock_DoorBlocker and assigned it to AirlockController.blockerCollider.");
        return blocker;
    }

    private static Vector3 ResolveDoorBlockerLocalPosition(AirlockController airlock)
    {
        if (airlock.enterPoint != null && airlock.enterPoint.IsChildOf(airlock.transform))
        {
            Vector3 enterLocal = airlock.enterPoint.localPosition;
            return new Vector3(enterLocal.x, Mathf.Max(enterLocal.y, 1.2f), enterLocal.z * 0.45f);
        }

        return new Vector3(0f, 1.2f, 0f);
    }

    private static Collider FindExistingDoorBlocker(AirlockController airlock, GameObject tunnel)
    {
        Collider best = null;
        int bestScore = int.MinValue;
        Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include);

        foreach (Collider collider in colliders)
        {
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            if (tunnel != null && collider.transform.IsChildOf(tunnel.transform))
            {
                continue;
            }

            float distance = Vector3.Distance(collider.bounds.center, airlock.transform.position);
            if (distance > 8f)
            {
                continue;
            }

            string lowerPath = GetPath(collider.transform).ToLowerInvariant();
            int score = Mathf.RoundToInt((8f - distance) * 10f);
            foreach (string hint in BlockerNameHints)
            {
                if (lowerPath.Contains(hint))
                {
                    score += 20;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = collider;
            }
        }

        return best;
    }

    private static void LogNearbyColliders(GameObject tunnel, AirlockController airlock)
    {
        StringBuilder log = new StringBuilder();
        log.AppendLine("Tunnel entry collider inspection:");
        AppendColliderList(log, "Tunnel_01", tunnel != null ? tunnel.transform : null, tunnel != null ? tunnel.transform.position : Vector3.zero, 0f, true);
        AppendColliderList(log, "Airlock/Door/Doorway", airlock != null ? airlock.transform : null, airlock != null ? airlock.transform.position : Vector3.zero, 8f, false);
        Debug.Log(log.ToString());
    }

    private static void AppendColliderList(StringBuilder log, string label, Transform root, Vector3 center, float radius, bool includeChildrenOnly)
    {
        log.AppendLine("- " + label + ":");
        if (root == null)
        {
            log.AppendLine("  not found");
            return;
        }

        Collider[] colliders = includeChildrenOnly
            ? root.GetComponentsInChildren<Collider>(true)
            : Object.FindObjectsByType<Collider>(FindObjectsInactive.Include);

        int count = 0;
        foreach (Collider collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            if (!includeChildrenOnly && Vector3.Distance(collider.bounds.center, center) > radius)
            {
                continue;
            }

            string lowerPath = GetPath(collider.transform).ToLowerInvariant();
            if (!includeChildrenOnly && !ContainsAny(lowerPath, "airlock", "door", "doorway", "blocker", "collider", "tunnel"))
            {
                continue;
            }

            count++;
            log.AppendLine("  " + GetPath(collider.transform)
                + " | " + collider.GetType().Name
                + " | trigger=" + collider.isTrigger
                + " | enabled=" + collider.enabled
                + " | size=" + collider.bounds.size);
        }

        if (count == 0)
        {
            log.AppendLine("  none");
        }
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(root.transform.position, Vector3.one);
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

    private static bool ContainsAny(string value, params string[] options)
    {
        foreach (string option in options)
        {
            if (value.Contains(option))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
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

    private static string ToYesNo(bool value)
    {
        return value ? "yes" : "no";
    }
}
