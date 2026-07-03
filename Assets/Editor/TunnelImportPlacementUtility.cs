using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TunnelImportPlacementUtility
{
    public static bool LastPlacementSucceeded { get; private set; }

    private const string TargetScenePath = "Assets/saved.unity";
    private const string TunnelFolder = "Assets/_External/moon/Tunnel";
    private const string MaterialFolder = TunnelFolder + "/Materials";
    private const string PrefabFolder = "Assets/_Prefabs/Environment";

    private const string FbxName = "Meshy_AI_Habitat_01_0702104213_texture.fbx";
    private const string BaseTextureName = "Meshy_AI_Habitat_01_0702104213_texture.png";
    private const string EmissionTextureName = "Meshy_AI_Habitat_01_0702104213_texture_emission.png";
    private const string MetallicTextureName = "Meshy_AI_Habitat_01_0702104213_texture_metallic.png";
    private const string NormalTextureName = "Meshy_AI_Habitat_01_0702104213_texture_normal.png";
    private const string RoughnessTextureName = "Meshy_AI_Habitat_01_0702104213_texture_roughness.png";

    private const string FbxAssetPath = TunnelFolder + "/" + FbxName;
    private const string BaseTexturePath = TunnelFolder + "/" + BaseTextureName;
    private const string EmissionTexturePath = TunnelFolder + "/" + EmissionTextureName;
    private const string MetallicTexturePath = TunnelFolder + "/" + MetallicTextureName;
    private const string NormalTexturePath = TunnelFolder + "/" + NormalTextureName;
    private const string RoughnessTexturePath = TunnelFolder + "/" + RoughnessTextureName;
    private const string MaterialAssetPath = MaterialFolder + "/Tunnel_01_Mat.mat";
    private const string PrefabAssetPath = PrefabFolder + "/Tunnel_01.prefab";

    [MenuItem("Tools/Regolith/Import Tunnel Assets Only")]
    public static void ImportTunnelAssetsOnly()
    {
        EnsureTunnelPrefabAsset();
    }

    private static bool EnsureTunnelPrefabAsset()
    {
        EnsureFolders();
        CopyFromDownloads();

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        ConfigureTextureImporters();
        AssetDatabase.ImportAsset(FbxAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxAssetPath)
            ?? AssetDatabase.LoadMainAssetAtPath(FbxAssetPath) as GameObject;
        if (fbxAsset == null)
        {
            Debug.LogError("Tunnel import: FBX not found at " + FbxAssetPath + ". Check C:/Users/khali/Downloads/tunle.");
            return false;
        }

        Material material = CreateOrUpdateMaterial();
        if (!CreateOrUpdatePrefabAsset(fbxAsset, material))
        {
            Debug.LogError("Tunnel import: failed to create prefab at " + PrefabAssetPath + ". No scene changes made.");
            return false;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Object prefab = AssetDatabase.LoadAssetAtPath<Object>(PrefabAssetPath);
        Selection.activeObject = prefab != null ? prefab : fbxAsset;
        if (Selection.activeObject != null)
        {
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        Debug.Log("Tunnel import complete. Prefab asset: " + PrefabAssetPath);
        return prefab != null;
    }

    [MenuItem("Tools/Regolith/Import And Place Tunnel Near Airlock")]
    public static void ImportAndPlaceTunnelNearAirlock()
    {
        LastPlacementSucceeded = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Tunnel placement cannot run during Play Mode. Exit Play Mode and run the menu item again.");
            return;
        }

        if (!EnsureTunnelPrefabAsset())
        {
            Debug.LogError("Tunnel placement stopped: tunnel prefab could not be created. No scene changes made.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
        if (prefab == null)
        {
            Debug.LogError("Tunnel placement: prefab missing at " + PrefabAssetPath + ". No scene changes made.");
            return;
        }

        BackupSceneFile();

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path.Replace("\\", "/") != TargetScenePath)
        {
            if (scene.isDirty)
            {
                Debug.LogWarning("Tunnel placement stopped: the active scene has unsaved changes and is not " + TargetScenePath + ". Save your current scene or open " + TargetScenePath + ", then rerun Tools/Regolith/Import And Place Tunnel Near Airlock.");
                return;
            }

            scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        }

        GameObject tunnel = GameObject.Find("Tunnel_01");
        if (tunnel == null)
        {
            tunnel = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (tunnel == null)
            {
                tunnel = Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(tunnel, scene);
            }

            tunnel.name = "Tunnel_01";
        }

        PlaceTunnelNearAirlock(tunnel);
        Selection.activeGameObject = tunnel;
        EditorGUIUtility.PingObject(tunnel);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        LastPlacementSucceeded = true;
        Debug.Log("Tunnel placement complete. Tunnel_01 was placed near Airlock in " + TargetScenePath + ".");
    }

    private static void BackupSceneFile()
    {
        string sourcePath = AssetPathToAbsolutePath(TargetScenePath);
        if (!File.Exists(sourcePath))
        {
            return;
        }

        string backupName = "saved_before_tunnel_place_auto_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        string backupPath = Path.Combine(Path.GetDirectoryName(sourcePath), backupName);
        File.Copy(sourcePath, backupPath, false);
        AssetDatabase.ImportAsset("Assets/" + backupName);
        Debug.Log("Tunnel placement backup created: Assets/" + backupName);
    }

    private static void EnsureFolders()
    {
        EnsureAssetFolder(TunnelFolder);
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

    private static void CopyFromDownloads()
    {
        string downloadsRoot = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads",
            "tunle");

        string downloadsSource = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads",
            "tunle",
            "Meshy_AI_Habitat_01_0702104213_texture_fbx");

        if (!Directory.Exists(downloadsSource) && Directory.Exists(downloadsRoot))
        {
            downloadsSource = downloadsRoot;
        }

        if (!Directory.Exists(downloadsSource))
        {
            Debug.LogWarning("Tunnel import: source folder not found at " + downloadsSource + ". Reusing files already under " + TunnelFolder + ".");
            return;
        }

        string destinationFolder = AssetPathToAbsolutePath(TunnelFolder);
        Directory.CreateDirectory(destinationFolder);

        CopyIfExists(downloadsSource, FbxName, destinationFolder);
        CopyIfExists(downloadsSource, BaseTextureName, destinationFolder);
        CopyIfExists(downloadsSource, EmissionTextureName, destinationFolder);
        CopyIfExists(downloadsSource, MetallicTextureName, destinationFolder);
        CopyIfExists(downloadsSource, NormalTextureName, destinationFolder);
        CopyIfExists(downloadsSource, RoughnessTextureName, destinationFolder);
    }

    private static void CopyIfExists(string sourceFolder, string fileName, string destinationFolder)
    {
        string source = Path.Combine(sourceFolder, fileName);
        if (File.Exists(source))
        {
            File.Copy(source, Path.Combine(destinationFolder, fileName), true);
        }
    }

    private static void ConfigureTextureImporters()
    {
        ConfigureTexture(BaseTexturePath, TextureImporterType.Default, true);
        ConfigureTexture(EmissionTexturePath, TextureImporterType.Default, true);
        ConfigureTexture(MetallicTexturePath, TextureImporterType.Default, false);
        ConfigureTexture(RoughnessTexturePath, TextureImporterType.Default, false);
        ConfigureTexture(NormalTexturePath, TextureImporterType.NormalMap, false);
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

    private static Material CreateOrUpdateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialAssetPath);
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
        material.EnableKeyword("_EMISSION");

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.5f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.34f);
        }

        EditorUtility.SetDirty(material);
        return material;
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

    private static bool CreateOrUpdatePrefabAsset(GameObject fbxAsset, Material material)
    {
        GameObject prefabRoot = null;
        try
        {
            prefabRoot = new GameObject("Tunnel_01");

            GameObject modelInstance = PrefabUtility.InstantiatePrefab(fbxAsset) as GameObject;
            if (modelInstance == null)
            {
                modelInstance = Object.Instantiate(fbxAsset);
            }

            modelInstance.name = fbxAsset.name;
            modelInstance.transform.SetParent(prefabRoot.transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            foreach (Renderer renderer in prefabRoot.GetComponentsInChildren<Renderer>(true))
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

            AddPrefabCollider(prefabRoot);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabAssetPath, out bool success);
            AssetDatabase.ImportAsset(PrefabAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return success && savedPrefab != null && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath) != null;
        }
        finally
        {
            if (prefabRoot != null)
            {
                Object.DestroyImmediate(prefabRoot);
            }
        }
    }

    private static void AddPrefabCollider(GameObject prefabRoot)
    {
        Bounds bounds = CalculateLocalRendererBounds(prefabRoot);
        BoxCollider collider = prefabRoot.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = bounds.size;
    }

    private static void PlaceTunnelNearAirlock(GameObject tunnel)
    {
        Transform airlock = FindAirlockTransform();
        Vector3 targetPosition = airlock != null
            ? airlock.position + airlock.right * 8.5f + airlock.forward * 4.5f
            : new Vector3(15f, 4.3f, 8f);

        Quaternion targetRotation = tunnel.transform.rotation;
        if (airlock != null)
        {
            Vector3 direction = airlock.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        tunnel.transform.SetPositionAndRotation(targetPosition, targetRotation);
        SnapBoundsToTerrain(tunnel);
    }

    private static Transform FindAirlockTransform()
    {
        GameObject airlock = GameObject.Find("Airlock")
            ?? GameObject.Find("REF_AirlockPlacement")
            ?? GameObject.Find("Base Entrance");

        return airlock != null ? airlock.transform : null;
    }

    private static void SnapBoundsToTerrain(GameObject root)
    {
        Terrain terrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
        {
            return;
        }

        Bounds bounds = CalculateWorldRendererBounds(root);
        float terrainY = terrain.SampleHeight(root.transform.position) + terrain.transform.position.y + 0.04f;
        if (bounds.size == Vector3.zero)
        {
            root.transform.position = new Vector3(root.transform.position.x, terrainY, root.transform.position.z);
            return;
        }

        root.transform.position += Vector3.up * (terrainY - bounds.min.y);
    }

    private static Bounds CalculateLocalRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            Bounds rendererBounds = renderer.bounds;
            Vector3 localCenter = root.transform.InverseTransformPoint(rendererBounds.center);
            Bounds localBounds = new Bounds(localCenter, rendererBounds.size);
            if (!hasBounds)
            {
                bounds = localBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(localBounds);
            }
        }

        return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one);
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

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
