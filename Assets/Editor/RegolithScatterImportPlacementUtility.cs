using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RegolithScatterImportPlacementUtility
{
    private const string TargetScenePath = "Assets/saved.unity";
    private const string SourceFolderName = "regolith";
    private const string SourceSubfolderName = "Meshy_AI_Rubble_Mountain_0702151354_texture_fbx";
    private const string AssetFolder = "Assets/_External/moon/RegolithScatter";
    private const string MaterialFolder = AssetFolder + "/Materials";
    private const string PrefabFolder = "Assets/_Prefabs/Environment";
    private const string SourceBaseName = "Meshy_AI_Rubble_Mountain_0702151354_texture";
    private const string MaterialPath = MaterialFolder + "/Regolith_Rubble_01_Mat.mat";
    private const string PrefabPath = PrefabFolder + "/Regolith_Rubble_01.prefab";

    [MenuItem("Tools/Regolith/Import And Scatter Regolith Along Road")]
    public static void ImportAndScatterRegolithAlongRoad()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Regolith scatter cannot run during Play Mode. Exit Play Mode and run the menu item again.");
            return;
        }

        EnsureFolders();
        CopyFromDownloads();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        if (!CreateOrUpdatePrefab())
        {
            Debug.LogError("Regolith scatter stopped: prefab could not be created. No scene placement was made.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path.Replace("\\", "/") != TargetScenePath)
        {
            if (scene.isDirty)
            {
                Debug.LogWarning("Regolith scatter stopped: active scene has unsaved changes and is not " + TargetScenePath + ". Open Assets/saved.unity or save current scene, then rerun.");
                return;
            }

            scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        }

        BackupSceneFile();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Regolith scatter stopped: prefab missing at " + PrefabPath + ".");
            return;
        }

        Transform parent = GetOrCreateScatterParent(scene);
        List<Vector3> route = BuildRoadRoute();
        ScatterAlongRoute(prefab, parent, route, scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("Regolith scatter complete. Multiple Regolith_Rubble instances were placed along the road/route and snapped to ground height.");
    }

    private static bool CreateOrUpdatePrefab()
    {
        ConfigureTexture(BaseTexturePath, TextureImporterType.Default, true);
        ConfigureTexture(EmissionTexturePath, TextureImporterType.Default, true);
        ConfigureTexture(MetallicTexturePath, TextureImporterType.Default, false);
        ConfigureTexture(RoughnessTexturePath, TextureImporterType.Default, false);
        ConfigureTexture(NormalTexturePath, TextureImporterType.NormalMap, false);
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath)
            ?? AssetDatabase.LoadMainAssetAtPath(FbxPath) as GameObject;
        if (fbxAsset == null)
        {
            Debug.LogError("Regolith import: FBX missing at " + FbxPath);
            return false;
        }

        Material material = CreateOrUpdateMaterial();
        GameObject root = null;
        try
        {
            root = new GameObject("Regolith_Rubble_01");
            GameObject model = PrefabUtility.InstantiatePrefab(fbxAsset) as GameObject;
            if (model == null)
            {
                model = Object.Instantiate(fbxAsset);
            }

            model.name = SourceBaseName;
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    renderer.sharedMaterial = material;
                    continue;
                }

                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
            }

            AddNonBlockingCollider(root);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return success && savedPrefab != null && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        }
        finally
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static Material CreateOrUpdateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        SetTexture(material, "_BaseMap", "_MainTex", BaseTexturePath);
        SetTexture(material, "_BumpMap", "_BumpMap", NormalTexturePath);
        SetTexture(material, "_MetallicGlossMap", "_MetallicGlossMap", MetallicTexturePath);
        SetTexture(material, "_EmissionMap", "_EmissionMap", EmissionTexturePath);
        material.EnableKeyword("_NORMALMAP");

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.22f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AddNonBlockingCollider(GameObject root)
    {
        Bounds bounds = CalculateWorldRendererBounds(root);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = root.transform.InverseTransformPoint(bounds.center);
        collider.size = bounds.size;
        collider.isTrigger = true;
    }

    private static Transform GetOrCreateScatterParent(Scene scene)
    {
        GameObject existing = GameObject.Find("Regolith_Road_Scatter");
        if (existing != null)
        {
            return existing.transform;
        }

        GameObject parent = new GameObject("Regolith_Road_Scatter");
        SceneManager.MoveGameObjectToScene(parent, scene);
        return parent.transform;
    }

    private static List<Vector3> BuildRoadRoute()
    {
        Bounds? roadBounds = FindRoadBounds();
        if (roadBounds.HasValue)
        {
            Bounds bounds = roadBounds.Value;
            bool useZ = bounds.size.z >= bounds.size.x;
            Vector3 start = bounds.center;
            Vector3 end = bounds.center;
            if (useZ)
            {
                start.z = bounds.min.z;
                end.z = bounds.max.z;
            }
            else
            {
                start.x = bounds.min.x;
                end.x = bounds.max.x;
            }

            return new List<Vector3> { start, end };
        }

        Transform player = FindSceneTransform("Player");
        Transform airlock = FindSceneTransform("Airlock");
        Transform tunnel = FindSceneTransform("Tunnel_01");

        Vector3 a = player != null ? player.position : new Vector3(12f, 0f, 2f);
        Vector3 b = tunnel != null ? tunnel.position : (airlock != null ? airlock.position : new Vector3(8f, 0f, 16f));
        return new List<Vector3> { a, b };
    }

    private static Bounds? FindRoadBounds()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        Bounds bounds = default;
        bool found = false;

        foreach (Renderer renderer in renderers)
        {
            string path = GetPath(renderer.transform).ToLowerInvariant();
            if (!ContainsAny(path, "road", "path", "trail", "track", "route"))
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found ? bounds : null;
    }

    private static void ScatterAlongRoute(GameObject prefab, Transform parent, List<Vector3> route, Scene scene)
    {
        const int count = 12;
        Vector3 start = route[0];
        Vector3 end = route[route.Count - 1];
        Vector3 direction = end - start;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        if (side.sqrMagnitude < 0.001f)
        {
            side = Vector3.right;
        }

        for (int i = 0; i < count; i++)
        {
            string name = "Regolith_Rubble_" + (i + 1).ToString("00");
            GameObject instance = GameObject.Find(name);
            if (instance == null)
            {
                instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null)
                {
                    instance = Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                }

                instance.name = name;
            }

            Undo.RecordObject(instance.transform, "Scatter regolith along road");
            instance.transform.SetParent(parent, true);

            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float sideOffset = ((i % 2 == 0) ? -1f : 1f) * Mathf.Lerp(1.2f, 2.8f, ((i * 37) % 100) / 100f);
            float alongJitter = Mathf.Lerp(-0.8f, 0.8f, ((i * 53) % 100) / 100f);
            Vector3 position = Vector3.Lerp(start, end, t) + side * sideOffset + direction * alongJitter;
            position = SnapToGround(position);

            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, (i * 47) % 360, 0f);
            float scale = Mathf.Lerp(0.75f, 1.35f, ((i * 29) % 100) / 100f);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * scale;
            EditorUtility.SetDirty(instance);
        }
    }

    private static Vector3 SnapToGround(Vector3 position)
    {
        Terrain terrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (terrain != null)
        {
            float y = terrain.SampleHeight(position) + terrain.transform.position.y;
            position.y = y;
            return position;
        }

        Ray ray = new Ray(position + Vector3.up * 100f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 250f, ~0, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y;
        }

        return position;
    }

    private static void CopyFromDownloads()
    {
        string sourceFolder = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads",
            SourceFolderName,
            SourceSubfolderName);

        if (!Directory.Exists(sourceFolder))
        {
            Debug.LogWarning("Regolith import: source folder not found at " + sourceFolder + ". Reusing files already under " + AssetFolder + ".");
            return;
        }

        string destination = AssetPathToAbsolutePath(AssetFolder);
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(sourceFolder))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
    }

    private static void ConfigureTexture(string assetPath, TextureImporterType textureType, bool srgb)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = textureType;
        importer.sRGBTexture = srgb;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    private static void SetTexture(Material material, string primaryProperty, string fallbackProperty, string assetPath)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture == null)
        {
            return;
        }

        if (material.HasProperty(primaryProperty))
        {
            material.SetTexture(primaryProperty, texture);
        }
        else if (material.HasProperty(fallbackProperty))
        {
            material.SetTexture(fallbackProperty, texture);
        }
    }

    private static void EnsureFolders()
    {
        EnsureAssetFolder("Assets/_External");
        EnsureAssetFolder("Assets/_External/moon");
        EnsureAssetFolder(AssetFolder);
        EnsureAssetFolder(MaterialFolder);
        EnsureAssetFolder(PrefabFolder);
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

    private static Bounds CalculateWorldRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = default;
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

        return hasBounds ? bounds : new Bounds(root.transform.position, Vector3.zero);
    }

    private static Transform FindSceneTransform(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        return obj != null ? obj.transform : null;
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
        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static void BackupSceneFile()
    {
        string sourcePath = AssetPathToAbsolutePath(TargetScenePath);
        if (!File.Exists(sourcePath))
        {
            return;
        }

        string backupName = "saved_before_regolith_scatter_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        string backupPath = Path.Combine(Path.GetDirectoryName(sourcePath), backupName);
        File.Copy(sourcePath, backupPath, false);
        AssetDatabase.ImportAsset("Assets/" + backupName);
        Debug.Log("Regolith scatter backup created: Assets/" + backupName);
    }

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string FbxPath => AssetFolder + "/" + SourceBaseName + ".fbx";
    private static string BaseTexturePath => AssetFolder + "/" + SourceBaseName + ".png";
    private static string EmissionTexturePath => AssetFolder + "/" + SourceBaseName + "_emission.png";
    private static string MetallicTexturePath => AssetFolder + "/" + SourceBaseName + "_metallic.png";
    private static string NormalTexturePath => AssetFolder + "/" + SourceBaseName + "_normal.png";
    private static string RoughnessTexturePath => AssetFolder + "/" + SourceBaseName + "_roughness.png";
}
