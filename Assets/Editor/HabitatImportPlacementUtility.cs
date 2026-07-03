using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HabitatImportPlacementUtility
{
    private const string HabitatFolder = "Assets/_External/moon/Habitat";
    private const string MaterialFolder = HabitatFolder + "/Materials";
    private const string PrefabFolder = "Assets/_Prefabs/Environment";

    private const string FbxName = "Meshy_AI_Habitat_01_0702093135_texture.fbx";
    private const string BaseTextureName = "Meshy_AI_Habitat_01_0702093135_texture.png";
    private const string EmissionTextureName = "Meshy_AI_Habitat_01_0702093135_texture_emission.png";
    private const string MetallicTextureName = "Meshy_AI_Habitat_01_0702093135_texture_metallic.png";
    private const string NormalTextureName = "Meshy_AI_Habitat_01_0702093135_texture_normal.png";
    private const string RoughnessTextureName = "Meshy_AI_Habitat_01_0702093135_texture_roughness.png";

    private const string FbxAssetPath = HabitatFolder + "/" + FbxName;
    private const string BaseTexturePath = HabitatFolder + "/" + BaseTextureName;
    private const string EmissionTexturePath = HabitatFolder + "/" + EmissionTextureName;
    private const string MetallicTexturePath = HabitatFolder + "/" + MetallicTextureName;
    private const string NormalTexturePath = HabitatFolder + "/" + NormalTextureName;
    private const string RoughnessTexturePath = HabitatFolder + "/" + RoughnessTextureName;
    private const string MaterialAssetPath = MaterialFolder + "/Habitat_01_Mat.mat";
    private const string PrefabAssetPath = PrefabFolder + "/Habitat_01.prefab";
    private const string TargetScenePath = "Assets/saved.unity";

    [MenuItem("Tools/Regolith/Import Habitat Assets Only")]
    public static void ImportHabitatAssetsOnly()
    {
        EnsureFolders();
        CopyFromDownloads();

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        ConfigureTextureImporters();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxAssetPath);
        if (fbxAsset == null)
        {
            Debug.LogError("Habitat import: FBX not found at " + FbxAssetPath + ". Check C:/Users/khali/Downloads/hapitat.");
            return;
        }

        Material material = CreateOrUpdateMaterial();
        CreateOrUpdatePrefabAsset(fbxAsset, material);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Object prefab = AssetDatabase.LoadAssetAtPath<Object>(PrefabAssetPath);
        Selection.activeObject = prefab != null ? prefab : fbxAsset;
        if (Selection.activeObject != null)
        {
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        Debug.Log(
            "Habitat import complete. Assets only; no scene objects were created, moved, deleted, or saved.\n" +
            "- Imported folder: " + HabitatFolder + "\n" +
            "- Material: " + MaterialAssetPath + "\n" +
            "- Prefab asset: " + PrefabAssetPath);
    }

    [MenuItem("Tools/Regolith/Import And Place Habitat")]
    public static void ImportAndPlaceHabitat()
    {
        Debug.LogWarning("Habitat placement is disabled. Running asset-only import instead; drag Habitat_01.prefab into the scene manually when ready.");
        ImportHabitatAssetsOnly();
    }

    [MenuItem("Tools/Regolith/Import And Place Habitat In Build Scene")]
    public static void ImportAndPlaceHabitatInBuildScene()
    {
        Debug.LogWarning("Habitat import-and-place now uses the safe placer. It only instantiates the existing prefab and saves the scene.");
        PlaceImportedHabitatNearAirlockInBuildScene();
    }

    [MenuItem("Tools/Regolith/Place Imported Habitat Near Airlock")]
    public static void PlaceImportedHabitatNearAirlockInBuildScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Habitat placement cannot run during Play Mode. Exit Play Mode and run the menu item again.");
            return;
        }

        if (!File.Exists(AssetPathToAbsolutePath(PrefabAssetPath)))
        {
            Debug.LogWarning("Habitat placement: prefab was not found. Running asset-only import first.");
            ImportHabitatAssetsOnly();
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
        if (prefab == null)
        {
            Debug.LogError("Habitat placement: prefab still missing at " + PrefabAssetPath + ". No scene changes made.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        GameObject habitat = GameObject.Find("Habitat_01");
        if (habitat == null)
        {
            habitat = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (habitat == null)
            {
                habitat = Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(habitat, scene);
            }

            habitat.name = "Habitat_01";
        }

        PlaceHabitatObject(habitat);
        Selection.activeGameObject = habitat;
        EditorGUIUtility.PingObject(habitat);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("Habitat placement complete. Habitat_01 was placed near Airlock in " + TargetScenePath + ". No other scene objects were modified.");
    }

    private static void EnsureFolders()
    {
        EnsureAssetFolder(HabitatFolder);
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
        string downloadsSource = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads",
            "hapitat",
            "Meshy_AI_Habitat_01_0702093135_texture_fbx");

        if (!Directory.Exists(downloadsSource))
        {
            Debug.LogWarning("Habitat import: source folder not found at " + downloadsSource + ". Reusing any files already imported under " + HabitatFolder + ".");
            return;
        }

        string destinationFolder = AssetPathToAbsolutePath(HabitatFolder);
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
        if (!File.Exists(source))
        {
            Debug.LogWarning("Habitat import: missing source file " + source);
            return;
        }

        File.Copy(source, Path.Combine(destinationFolder, fileName), true);
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
            material.SetFloat("_Metallic", 0.45f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.36f);
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

    private static void CreateOrUpdatePrefabAsset(GameObject fbxAsset, Material material)
    {
        GameObject prefabRoot = new GameObject("Habitat_01")
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        GameObject modelInstance = Object.Instantiate(fbxAsset);
        modelInstance.name = fbxAsset.name;
        modelInstance.hideFlags = HideFlags.HideAndDontSave;
        modelInstance.transform.SetParent(prefabRoot.transform, false);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        foreach (Renderer renderer in prefabRoot.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        AddPrefabCollider(prefabRoot);
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabAssetPath);
        Object.DestroyImmediate(prefabRoot);
    }

    private static void PlaceHabitatObject(GameObject habitat)
    {
        Transform airlock = FindAirlockTransform();
        Vector3 targetPosition = airlock != null
            ? airlock.position - airlock.right * 9f + airlock.forward * 6f
            : new Vector3(7f, 4.3f, 8f);

        Quaternion targetRotation = habitat.transform.rotation;
        if (airlock != null)
        {
            Vector3 lookDirection = airlock.position - targetPosition;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        habitat.transform.SetPositionAndRotation(targetPosition, targetRotation);
        SnapBoundsToTerrain(habitat);
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

    private static void AddPrefabCollider(GameObject prefabRoot)
    {
        Bounds bounds = CalculateLocalRendererBounds(prefabRoot);
        BoxCollider collider = prefabRoot.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = bounds.size;
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
            Vector3 localSize = rendererBounds.size;
            Bounds localBounds = new Bounds(localCenter, localSize);

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

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
