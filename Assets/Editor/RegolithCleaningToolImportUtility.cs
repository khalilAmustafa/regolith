using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RegolithCleaningToolImportUtility
{
    private const string TargetScenePath = "Assets/saved.unity";
    private const string ToolFolder = "Assets/_External/moon/CleaningTool";
    private const string TextureFolder = ToolFolder + "/Textures";
    private const string MaterialFolder = ToolFolder + "/Materials";
    private const string PrefabFolder = "Assets/_Prefabs/Environment";
    private const string FbxAssetPath = ToolFolder + "/Meshy_Cleaning_Tool.fbx";
    private const string MaterialAssetPath = MaterialFolder + "/Cleaning_Tool_Mat.mat";
    private const string PrefabAssetPath = PrefabFolder + "/Cleaning_Tool_01.prefab";

    private const string SourceFolderName = "Meshy_AI_Orbital_Scrubber_360_0701172800_texture_fbx";
    private const string SourceFbxName = "Meshy_AI_Orbital_Scrubber_360_0701172800_texture.fbx";
    private const string BaseTextureName = "Meshy_AI_Orbital_Scrubber_360_0701172800_texture.png";
    private const string EmissionTextureName = "Meshy_AI_Orbital_Scrubber_360_0701172800_texture_emission.png";
    private const string MetallicTextureName = "Meshy_AI_Orbital_Scrubber_360_0701172800_texture_metallic.png";
    private const string NormalTextureName = "Meshy_AI_Orbital_Scrubber_360_0701172800_texture_normal.png";
    private const string RoughnessTextureName = "Meshy_AI_Orbital_Scrubber_360_0701172800_texture_roughness.png";

    private static readonly string[] TextureNames =
    {
        BaseTextureName,
        EmissionTextureName,
        MetallicTextureName,
        NormalTextureName,
        RoughnessTextureName
    };

    [MenuItem("Tools/Regolith/Import And Place Cleaning Tool")]
    public static void ImportAndPlaceCleaningTool()
    {
        EnsureFolders();

        string sourceFolder = ResolveSourceFolder();
        if (!string.IsNullOrEmpty(sourceFolder))
        {
            if (!CopyFbx(sourceFolder) || !CopyTextures(sourceFolder))
            {
                return;
            }
        }
        else if (!ImportedAssetsExist())
        {
            Debug.LogError("Cleaning tool import: source folder not found under project cleaning_tool, Assets/cleaning_tool, current-user Downloads/cleaning_tool, Downloads/cleaaning, or Downloads/cleaning, and no imported cleaning tool assets exist.");
            return;
        }
        else
        {
            Debug.LogWarning("Cleaning tool import: source folder was not found; reusing existing imported FBX and textures.");
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        ConfigureTextureImportSettings();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        Material material = CreateOrUpdateMaterial();
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxAssetPath);
        if (fbxAsset == null)
        {
            Debug.LogError("Cleaning tool import: copied FBX could not be loaded at " + FbxAssetPath);
            return;
        }

        Transform clusterTransform = FindClusterTransform();
        GameObject root = CreateSceneInstance(fbxAsset);
        ApplyMaterialOverrides(root, material);
        AdjustScaleIfNeeded(root);
        PlaceCleaningTool(root, clusterTransform);
        AddInteractionPoint(root);
        AddSimpleRootCollider(root);
        SavePrefab(root);

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
    }

    [MenuItem("Tools/Regolith/Import And Place Cleaning Tool In Build Scene")]
    public static void ImportAndPlaceCleaningToolInBuildScene()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        if (GameObject.Find("SolarArray_01") == null)
        {
            SolarArrayImportPlacementUtility.ImportAndPlaceSolarArray();
        }

        ImportAndPlaceCleaningTool();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Cleaning tool import: saved placement in " + TargetScenePath + ".");
    }

    private static void EnsureFolders()
    {
        EnsureAssetFolder(ToolFolder);
        EnsureAssetFolder(TextureFolder);
        EnsureAssetFolder(MaterialFolder);
        EnsureAssetFolder("Assets/_Prefabs");
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

    private static string ResolveSourceFolder()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string projectCleaningTool = ResolveSourceFolderCandidate(Path.Combine(projectRoot, "cleaning_tool"));
        if (!string.IsNullOrEmpty(projectCleaningTool))
        {
            return projectCleaningTool;
        }

        string assetsCleaningTool = ResolveSourceFolderCandidate(Path.Combine(Application.dataPath, "cleaning_tool"));
        if (!string.IsNullOrEmpty(assetsCleaningTool))
        {
            return assetsCleaningTool;
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile))
        {
            return null;
        }

        string cleaningToolFolder = ResolveSourceFolderCandidate(Path.Combine(userProfile, "Downloads", "cleaning_tool"));
        if (!string.IsNullOrEmpty(cleaningToolFolder))
        {
            return cleaningToolFolder;
        }

        string cleaaningFolder = ResolveSourceFolderCandidate(Path.Combine(userProfile, "Downloads", "cleaaning"));
        if (!string.IsNullOrEmpty(cleaaningFolder))
        {
            return cleaaningFolder;
        }

        return ResolveSourceFolderCandidate(Path.Combine(userProfile, "Downloads", "cleaning"));
    }

    private static string ResolveSourceFolderCandidate(string rootFolder)
    {
        if (string.IsNullOrEmpty(rootFolder) || !Directory.Exists(rootFolder))
        {
            return null;
        }

        string nestedSourceFolder = Path.Combine(rootFolder, SourceFolderName);
        if (IsValidSourceFolder(nestedSourceFolder))
        {
            return nestedSourceFolder;
        }

        if (IsValidSourceFolder(rootFolder))
        {
            return rootFolder;
        }

        return Directory.EnumerateDirectories(rootFolder, "*", SearchOption.TopDirectoryOnly)
            .Where(IsValidSourceFolder)
            .OrderBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsValidSourceFolder(string folder)
    {
        return !string.IsNullOrEmpty(folder)
            && Directory.Exists(folder)
            && File.Exists(Path.Combine(folder, SourceFbxName))
            && TextureNames.All(textureName => File.Exists(Path.Combine(folder, textureName)));
    }

    private static bool ImportedAssetsExist()
    {
        return File.Exists(AssetPathToAbsolutePath(FbxAssetPath))
            && TextureNames.All(textureName => File.Exists(AssetPathToAbsolutePath(TextureFolder + "/" + textureName)));
    }

    private static bool CopyFbx(string sourceFolder)
    {
        string sourcePath = Path.Combine(sourceFolder, SourceFbxName);
        if (!File.Exists(sourcePath))
        {
            Debug.LogError("Cleaning tool import: source FBX not found at " + sourcePath);
            return false;
        }

        File.Copy(sourcePath, AssetPathToAbsolutePath(FbxAssetPath), true);
        Debug.Log("Cleaning tool import: copied FBX to " + FbxAssetPath);
        return true;
    }

    private static bool CopyTextures(string sourceFolder)
    {
        List<string> copied = new List<string>();
        foreach (string textureName in TextureNames)
        {
            string sourcePath = Path.Combine(sourceFolder, textureName);
            if (!File.Exists(sourcePath))
            {
                Debug.LogError("Cleaning tool import: source texture not found at " + sourcePath);
                return false;
            }

            File.Copy(sourcePath, AssetPathToAbsolutePath(TextureFolder + "/" + textureName), true);
            copied.Add(textureName);
        }

        Debug.Log("Cleaning tool import: copied textures to " + TextureFolder + ": " + string.Join(", ", copied));
        return true;
    }

    private static void ConfigureTextureImportSettings()
    {
        SetTextureImporterType(TextureFolder + "/" + BaseTextureName, TextureImporterType.Default);
        SetTextureImporterType(TextureFolder + "/" + EmissionTextureName, TextureImporterType.Default);
        SetTextureImporterType(TextureFolder + "/" + MetallicTextureName, TextureImporterType.Default);
        SetTextureImporterType(TextureFolder + "/" + RoughnessTextureName, TextureImporterType.Default);
        SetTextureImporterType(TextureFolder + "/" + NormalTextureName, TextureImporterType.NormalMap);
    }

    private static void SetTextureImporterType(string assetPath, TextureImporterType textureType)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("Cleaning tool import: texture importer not found for " + assetPath);
            return;
        }

        if (importer.textureType != textureType)
        {
            importer.textureType = textureType;
            importer.SaveAndReimport();
        }
    }

    private static Material CreateOrUpdateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);
        Shader shader = FindLitShader();

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialAssetPath);
        }
        else if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        SetMaterialTexture(material, "_BaseMap", "_MainTex", LoadTexture(BaseTextureName));
        SetMaterialTexture(material, "_BumpMap", null, LoadTexture(NormalTextureName));
        SetMaterialTexture(material, "_EmissionMap", null, LoadTexture(EmissionTextureName));
        SetMaterialTexture(material, "_MetallicGlossMap", null, LoadTexture(MetallicTextureName));
        SetMaterialColor(material, "_BaseColor", Color.white);
        SetMaterialColor(material, "_Color", Color.white);
        SetMaterialFloat(material, "_Metallic", 0.35f);
        SetMaterialFloat(material, "_Smoothness", 0.32f);
        SetMaterialFloat(material, "_Glossiness", 0.32f);
        SetMaterialFloat(material, "_BumpScale", 1f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.EnableKeyword("_EMISSION");
        SetMaterialColor(material, "_EmissionColor", new Color(0.68f, 0.9f, 1f) * 0.35f);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        Debug.Log("Cleaning tool import: created material and assigned textures at " + MaterialAssetPath);
        return material;
    }

    private static Texture2D LoadTexture(string textureName)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + textureName);
    }

    private static Shader FindLitShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }

    private static GameObject CreateSceneInstance(GameObject fbxAsset)
    {
        GameObject existing = GameObject.Find("Cleaning_Tool_01");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject("Cleaning_Tool_01");
        Undo.RegisterCreatedObjectUndo(root, "Create Cleaning Tool");

        GameObject instance = PrefabUtility.InstantiatePrefab(fbxAsset, SceneManager.GetActiveScene()) as GameObject;
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(fbxAsset);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Instantiate Cleaning Tool FBX");
        instance.name = fbxAsset.name;
        instance.transform.SetParent(root.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        Debug.Log("Cleaning tool import: instantiated cleaning tool from " + FbxAssetPath);
        return root;
    }

    private static void ApplyMaterialOverrides(GameObject root, Material material)
    {
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer renderer in renderers)
        {
            Undo.RecordObject(renderer, "Override Cleaning Tool Material");
            renderer.sharedMaterials = FillMaterialSlots(renderer.sharedMaterials, material);
            EditorUtility.SetDirty(renderer);
        }

        Debug.Log("Cleaning tool import: applied material overrides to " + renderers.Length + " MeshRenderer(s).");
    }

    private static void AdjustScaleIfNeeded(GameObject root)
    {
        Bounds bounds = CalculateRendererBounds(root);
        float largestDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

        Undo.RecordObject(root.transform, "Scale Cleaning Tool");
        root.transform.localScale = Vector3.one;
        if (largestDimension > 12f)
        {
            root.transform.localScale = Vector3.one * 0.4f;
        }
        else if (largestDimension > 0.001f && largestDimension < 1.2f)
        {
            root.transform.localScale = Vector3.one * 2f;
        }
    }

    private static void PlaceCleaningTool(GameObject root, Transform clusterTransform)
    {
        Vector3 targetPosition = clusterTransform != null
            ? clusterTransform.position + new Vector3(-2.2f, 0f, 2.6f)
            : new Vector3(43f, 4.3f, 16f);

        Quaternion targetRotation = Quaternion.identity;
        if (clusterTransform != null)
        {
            Vector3 lookDirection = clusterTransform.position - targetPosition;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        Undo.RecordObject(root.transform, "Place Cleaning Tool");
        root.transform.position = targetPosition;
        root.transform.rotation = targetRotation;
        SnapBoundsToTerrain(root);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Cleaning tool import: placed near solar/generator cluster at " + root.transform.position + ".");
    }

    private static Transform FindClusterTransform()
    {
        GameObject generator = GameObject.Find("Generator_Module_01");
        if (generator != null)
        {
            return generator.transform;
        }

        GameObject solar = GameObject.Find("SolarArray_01");
        if (solar != null)
        {
            return solar.transform;
        }

        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
        GameObject candidate = objects
            .Where(sceneObject => sceneObject != null)
            .Where(sceneObject =>
                sceneObject.name.IndexOf("Solar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneObject.name.IndexOf("Generator", StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(sceneObject => sceneObject.transform.parent == null ? 0 : 1)
            .ThenBy(sceneObject => sceneObject.name, StringComparer.Ordinal)
            .FirstOrDefault();

        return candidate != null ? candidate.transform : null;
    }

    private static void SnapBoundsToTerrain(GameObject root)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        }

        if (terrain == null)
        {
            return;
        }

        float terrainY = terrain.SampleHeight(root.transform.position) + terrain.transform.position.y + 0.04f;
        Bounds bounds = CalculateRendererBounds(root);
        if (bounds.size == Vector3.zero)
        {
            root.transform.position = new Vector3(root.transform.position.x, terrainY, root.transform.position.z);
            return;
        }

        root.transform.position += new Vector3(0f, terrainY - bounds.min.y, 0f);
    }

    private static void AddInteractionPoint(GameObject root)
    {
        Transform existing = root.transform.Find("InteractionPoint");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        Bounds bounds = CalculateRendererBounds(root);
        Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
        Vector3 localExtents = Abs(root.transform.InverseTransformVector(bounds.extents));

        GameObject point = new GameObject("InteractionPoint");
        Undo.RegisterCreatedObjectUndo(point, "Create Cleaning Tool Interaction Point");
        point.transform.SetParent(root.transform, false);
        point.transform.localPosition = localCenter + new Vector3(0f, 0f, localExtents.z + 1f);
        point.transform.localRotation = Quaternion.identity;
        point.transform.localScale = Vector3.one;

        Debug.Log("Cleaning tool import: added InteractionPoint.");
    }

    private static void AddSimpleRootCollider(GameObject root)
    {
        if (root.GetComponentsInChildren<Collider>(true).Length > 0)
        {
            Debug.Log("Cleaning tool import: existing collider(s) found; no root BoxCollider added.");
            return;
        }

        Bounds bounds = CalculateRendererBounds(root);
        if (bounds.size == Vector3.zero)
        {
            Debug.LogWarning("Cleaning tool import: could not calculate renderer bounds, so no collider was added.");
            return;
        }

        BoxCollider collider = Undo.AddComponent<BoxCollider>(root);
        collider.center = root.transform.InverseTransformPoint(bounds.center);
        collider.size = Abs(root.transform.InverseTransformVector(bounds.size));
        Debug.Log("Cleaning tool import: added simple root BoxCollider.");
    }

    private static void SavePrefab(GameObject root)
    {
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabAssetPath, InteractionMode.UserAction);
        if (savedPrefab == null)
        {
            Debug.LogError("Cleaning tool import: prefab could not be saved at " + PrefabAssetPath);
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Cleaning tool import: saved prefab at " + PrefabAssetPath);
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = default;
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
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

        return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.zero);
    }

    private static Material[] FillMaterialSlots(Material[] existingSlots, Material material)
    {
        int slotCount = Mathf.Max(1, existingSlots != null ? existingSlots.Length : 0);
        Material[] slots = new Material[slotCount];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = material;
        }

        return slots;
    }

    private static void SetMaterialTexture(Material material, string property, string fallbackProperty, Texture texture)
    {
        if (material.HasProperty(property))
        {
            material.SetTexture(property, texture);
        }

        if (!string.IsNullOrEmpty(fallbackProperty) && material.HasProperty(fallbackProperty))
        {
            material.SetTexture(fallbackProperty, texture);
        }
    }

    private static void SetMaterialFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void SetMaterialColor(Material material, string property, Color color)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, color);
        }
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
