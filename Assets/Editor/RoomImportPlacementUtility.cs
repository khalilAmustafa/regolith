using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomImportPlacementUtility
{
    public static bool LastPlacementSucceeded { get; private set; }

    private const string TargetScenePath = "Assets/saved.unity";
    private const string PrefabFolder = "Assets/_Prefabs/Environment";
    private const string Room2Folder = "Assets/_External/moon/Room2";
    private const string Room3Folder = "Assets/_External/moon/Room3";
    private const string MaterialFolder = "Assets/_External/moon/Rooms/Materials";

    private static readonly RoomAssetConfig Room2 = new RoomAssetConfig(
        "Room_02",
        Room2Folder,
        "Meshy_AI_Oxygen_Processor_02_0702135147_texture",
        "Room_02_Mat",
        "Room_02.prefab");

    private static readonly RoomAssetConfig Room3 = new RoomAssetConfig(
        "Room_03",
        Room3Folder,
        "Meshy_AI_Oxygen_Processor_02_0702140210_texture",
        "Room_03_Mat",
        "Room_03.prefab");

    [MenuItem("Tools/Regolith/Import And Place Room 2 And Room 3")]
    public static void ImportAndPlaceRooms()
    {
        LastPlacementSucceeded = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Room placement cannot run during Play Mode. Exit Play Mode and run the menu item again.");
            return;
        }

        EnsureFolders();
        CopyRoomFromDownloads("ROOM2", Room2);
        CopyRoomFromDownloads("ROOM3", Room3);

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        bool room2Ready = ImportAndCreatePrefab(Room2);
        bool room3Ready = ImportAndCreatePrefab(Room3);
        if (!room2Ready || !room3Ready)
        {
            Debug.LogError("Room placement stopped: one or both room prefabs could not be created. No scene placement was made.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path.Replace("\\", "/") != TargetScenePath)
        {
            if (scene.isDirty)
            {
                Debug.LogWarning("Room placement stopped: active scene has unsaved changes and is not " + TargetScenePath + ". Open Assets/saved.unity or save current scene, then rerun.");
                return;
            }

            scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        }

        BackupSceneFile();

        GameObject room2Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Room2.PrefabPath);
        GameObject room3Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Room3.PrefabPath);
        GameObject room2 = GetOrCreateScenePrefab(Room2.SceneName, room2Prefab, scene);
        GameObject room3 = GetOrCreateScenePrefab(Room3.SceneName, room3Prefab, scene);

        PlaceRooms(room2, room3);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        LastPlacementSucceeded = true;
        Debug.Log("Room import/placement complete. Room_02 and Room_03 were imported with textures and placed in " + TargetScenePath + ".");
    }

    private static bool ImportAndCreatePrefab(RoomAssetConfig config)
    {
        string fbxPath = config.FbxPath;
        AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        ConfigureTexture(config.BaseTexturePath, TextureImporterType.Default, true);
        ConfigureTexture(config.EmissionTexturePath, TextureImporterType.Default, true);
        ConfigureTexture(config.MetallicTexturePath, TextureImporterType.Default, false);
        ConfigureTexture(config.RoughnessTexturePath, TextureImporterType.Default, false);
        ConfigureTexture(config.NormalTexturePath, TextureImporterType.NormalMap, false);

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath)
            ?? AssetDatabase.LoadMainAssetAtPath(fbxPath) as GameObject;
        if (fbxAsset == null)
        {
            Debug.LogError("Room import: FBX missing at " + fbxPath);
            return false;
        }

        Material material = CreateOrUpdateMaterial(config);
        GameObject prefabRoot = null;
        try
        {
            prefabRoot = new GameObject(config.SceneName);
            GameObject modelInstance = PrefabUtility.InstantiatePrefab(fbxAsset) as GameObject;
            if (modelInstance == null)
            {
                modelInstance = Object.Instantiate(fbxAsset);
            }

            modelInstance.name = config.SourceBaseName;
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

            AddRootTriggerCollider(prefabRoot);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, config.PrefabPath, out bool success);
            AssetDatabase.ImportAsset(config.PrefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return success && savedPrefab != null && AssetDatabase.LoadAssetAtPath<GameObject>(config.PrefabPath) != null;
        }
        finally
        {
            if (prefabRoot != null)
            {
                Object.DestroyImmediate(prefabRoot);
            }
        }
    }

    private static Material CreateOrUpdateMaterial(RoomAssetConfig config)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(config.MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, config.MaterialPath);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        SetTexture(material, "_BaseMap", "_MainTex", config.BaseTexturePath);
        SetTexture(material, "_BumpMap", "_BumpMap", config.NormalTexturePath);
        SetTexture(material, "_MetallicGlossMap", "_MetallicGlossMap", config.MetallicTexturePath);
        SetTexture(material, "_EmissionMap", "_EmissionMap", config.EmissionTexturePath);

        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_EMISSION");

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.35f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.38f);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Color.white * 0.35f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject GetOrCreateScenePrefab(string sceneName, GameObject prefab, Scene scene)
    {
        GameObject existing = GameObject.Find(sceneName);
        if (existing != null)
        {
            return existing;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            instance = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
        }

        instance.name = sceneName;
        return instance;
    }

    private static void PlaceRooms(GameObject room2, GameObject room3)
    {
        Transform anchor = FindSceneTransform("Tunnel_01")
            ?? FindSceneTransform("Airlock")
            ?? FindSceneTransform("Habitat_01");

        Vector3 basePosition = anchor != null ? anchor.position : new Vector3(6f, 6.2f, 20f);
        Quaternion baseRotation = anchor != null ? anchor.rotation : Quaternion.identity;

        Vector3 right = anchor != null ? anchor.right : Vector3.right;
        Vector3 forward = anchor != null ? anchor.forward : Vector3.forward;

        SetRoomTransform(room2, basePosition + right * 5f + forward * 5.5f, baseRotation);
        SetRoomTransform(room3, basePosition - right * 5f + forward * 5.5f, baseRotation);
    }

    private static void SetRoomTransform(GameObject room, Vector3 position, Quaternion rotation)
    {
        Undo.RecordObject(room.transform, "Place imported room");
        room.transform.SetPositionAndRotation(position, rotation);
        room.transform.localScale = Vector3.one;
        SnapBoundsToTerrain(room);
        EditorUtility.SetDirty(room);
    }

    private static void AddRootTriggerCollider(GameObject root)
    {
        Bounds bounds = CalculateWorldRendererBounds(root);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = root.transform.InverseTransformPoint(bounds.center);
        collider.size = bounds.size;
        collider.isTrigger = true;
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

    private static Transform FindSceneTransform(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        return obj != null ? obj.transform : null;
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

    private static void CopyRoomFromDownloads(string downloadsFolderName, RoomAssetConfig config)
    {
        string sourceFolder = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads",
            downloadsFolderName,
            config.SourceBaseName + "_fbx");

        if (!Directory.Exists(sourceFolder))
        {
            Debug.LogWarning("Room import: source folder not found at " + sourceFolder + ". Reusing files already under " + config.AssetFolder + ".");
            return;
        }

        string destination = AssetPathToAbsolutePath(config.AssetFolder);
        Directory.CreateDirectory(destination);

        CopyIfExists(sourceFolder, config.SourceBaseName + ".fbx", destination);
        CopyIfExists(sourceFolder, config.SourceBaseName + ".png", destination);
        CopyIfExists(sourceFolder, config.SourceBaseName + "_emission.png", destination);
        CopyIfExists(sourceFolder, config.SourceBaseName + "_metallic.png", destination);
        CopyIfExists(sourceFolder, config.SourceBaseName + "_normal.png", destination);
        CopyIfExists(sourceFolder, config.SourceBaseName + "_roughness.png", destination);
    }

    private static void CopyIfExists(string sourceFolder, string fileName, string destinationFolder)
    {
        string source = Path.Combine(sourceFolder, fileName);
        if (File.Exists(source))
        {
            File.Copy(source, Path.Combine(destinationFolder, fileName), true);
        }
    }

    private static void EnsureFolders()
    {
        EnsureAssetFolder("Assets/_External");
        EnsureAssetFolder("Assets/_External/moon");
        EnsureAssetFolder(Room2Folder);
        EnsureAssetFolder(Room3Folder);
        EnsureAssetFolder("Assets/_External/moon/Rooms");
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

    private static void BackupSceneFile()
    {
        string sourcePath = AssetPathToAbsolutePath(TargetScenePath);
        if (!File.Exists(sourcePath))
        {
            return;
        }

        string backupName = "saved_before_room2_room3_place_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        string backupPath = Path.Combine(Path.GetDirectoryName(sourcePath), backupName);
        File.Copy(sourcePath, backupPath, false);
        AssetDatabase.ImportAsset("Assets/" + backupName);
        Debug.Log("Room placement backup created: Assets/" + backupName);
    }

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private sealed class RoomAssetConfig
    {
        public readonly string SceneName;
        public readonly string AssetFolder;
        public readonly string SourceBaseName;
        public readonly string MaterialName;
        public readonly string PrefabName;

        public RoomAssetConfig(string sceneName, string assetFolder, string sourceBaseName, string materialName, string prefabName)
        {
            SceneName = sceneName;
            AssetFolder = assetFolder;
            SourceBaseName = sourceBaseName;
            MaterialName = materialName;
            PrefabName = prefabName;
        }

        public string FbxPath => AssetFolder + "/" + SourceBaseName + ".fbx";
        public string BaseTexturePath => AssetFolder + "/" + SourceBaseName + ".png";
        public string EmissionTexturePath => AssetFolder + "/" + SourceBaseName + "_emission.png";
        public string MetallicTexturePath => AssetFolder + "/" + SourceBaseName + "_metallic.png";
        public string NormalTexturePath => AssetFolder + "/" + SourceBaseName + "_normal.png";
        public string RoughnessTexturePath => AssetFolder + "/" + SourceBaseName + "_roughness.png";
        public string MaterialPath => MaterialFolder + "/" + MaterialName + ".mat";
        public string PrefabPath => PrefabFolder + "/" + PrefabName;
    }
}
