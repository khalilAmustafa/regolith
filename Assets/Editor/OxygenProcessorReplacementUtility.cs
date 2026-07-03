using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OxygenProcessorReplacementUtility
{
    private const string MenuPath = "Tools/Regolith/Replace O2 Processor Model";
    private const string SourceFolderName = "o2";
    private const string SourceInnerFolderName = "Meshy_AI_Oxygen_Processor_02_0703085234_texture_fbx";
    private const string SourceBaseName = "Meshy_AI_Oxygen_Processor_02_0703085234_texture";
    private const string AssetFolder = "Assets/_External/Oxygen_Processor_02";
    private const string MaterialFolder = "Assets/_Materials/OxygenProcessor";
    private const string PrefabFolder = "Assets/_Prefabs/Environment";
    private const string MaterialPath = MaterialFolder + "/Oxygen_Processor_02_Mat.mat";
    private const string PrefabPath = PrefabFolder + "/Oxygen_Processor_02.prefab";
    private const string ReplacementChildName = "OxygenProcessor_02_Model";

    [MenuItem(MenuPath)]
    public static void ReplaceO2ProcessorModel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("O2 processor replacement cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        EnsureFolders();
        CopyAssetFilesFromDownloads();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        if (!ImportAndCreatePrefab())
        {
            Debug.LogError("O2 processor replacement stopped: prefab could not be created from imported FBX.");
            return;
        }

        GameObject target = FindCurrentO2Processor();
        if (target == null)
        {
            Debug.LogError("O2 processor replacement stopped: no current Oxygen/O2 processor object was found in the active scene.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("O2 processor replacement stopped: prefab missing at " + PrefabPath);
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(target, "Replace O2 processor model");

        GameObject replacement = GetOrCreateReplacementModel(target, prefab);
        DisableOldVisualRenderers(target, replacement.transform);
        EnsureUsefulCollider(target);

        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "O2 processor replacement complete:\n" +
            "- Imported asset folder: " + AssetFolder + "\n" +
            "- Prefab: " + PrefabPath + "\n" +
            "- Target scene object: " + target.name + "\n" +
            "- New model child: " + replacement.name + "\n" +
            "- Existing scripts/triggers/colliders preserved");
    }

    private static void CopyAssetFilesFromDownloads()
    {
        string sourceFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            SourceFolderName,
            SourceInnerFolderName);

        if (!Directory.Exists(sourceFolder))
        {
            Debug.LogWarning("O2 processor import: source folder not found at " + sourceFolder + ". Reusing files already in " + AssetFolder + ".");
            return;
        }

        string destination = AssetPathToAbsolutePath(AssetFolder);
        Directory.CreateDirectory(destination);

        CopyIfExists(sourceFolder, SourceBaseName + ".fbx", destination);
        CopyIfExists(sourceFolder, SourceBaseName + ".png", destination);
        CopyIfExists(sourceFolder, SourceBaseName + "_emission.png", destination);
        CopyIfExists(sourceFolder, SourceBaseName + "_metallic.png", destination);
        CopyIfExists(sourceFolder, SourceBaseName + "_normal.png", destination);
        CopyIfExists(sourceFolder, SourceBaseName + "_roughness.png", destination);
    }

    private static bool ImportAndCreatePrefab()
    {
        string fbxPath = AssetFolder + "/" + SourceBaseName + ".fbx";
        if (!File.Exists(AssetPathToAbsolutePath(fbxPath)))
        {
            Debug.LogError("O2 processor import: FBX missing at " + fbxPath);
            return false;
        }

        AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        ConfigureTexture(BaseTexturePath, TextureImporterType.Default, true);
        ConfigureTexture(EmissionTexturePath, TextureImporterType.Default, true);
        ConfigureTexture(MetallicTexturePath, TextureImporterType.Default, false);
        ConfigureTexture(RoughnessTexturePath, TextureImporterType.Default, false);
        ConfigureTexture(NormalTexturePath, TextureImporterType.NormalMap, false);

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath)
            ?? AssetDatabase.LoadMainAssetAtPath(fbxPath) as GameObject;
        if (fbxAsset == null)
        {
            Debug.LogError("O2 processor import: Unity could not load FBX asset at " + fbxPath);
            return false;
        }

        Material material = CreateOrUpdateMaterial();
        GameObject prefabRoot = null;
        try
        {
            prefabRoot = new GameObject("Oxygen_Processor_02");
            GameObject modelInstance = PrefabUtility.InstantiatePrefab(fbxAsset) as GameObject;
            if (modelInstance == null)
            {
                modelInstance = UnityEngine.Object.Instantiate(fbxAsset);
            }

            modelInstance.name = SourceBaseName;
            modelInstance.transform.SetParent(prefabRoot.transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            AssignMaterial(prefabRoot, material);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath, out bool success);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return success && savedPrefab != null;
        }
        finally
        {
            if (prefabRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
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
        material.EnableKeyword("_EMISSION");

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.35f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.42f);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Color.white * 0.45f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject FindCurrentO2Processor()
    {
        List<GameObject> candidates = new List<GameObject>();

        foreach (O2ExtractorInteractable extractor in UnityEngine.Object.FindObjectsByType<O2ExtractorInteractable>(FindObjectsInactive.Include))
        {
            GameObject candidate = extractor.transform.parent != null ? extractor.transform.parent.gameObject : extractor.gameObject;
            if (!IsGeneratedTrigger(candidate))
            {
                candidates.Add(candidate);
            }
        }

        foreach (OxygenProcessor processor in UnityEngine.Object.FindObjectsByType<OxygenProcessor>(FindObjectsInactive.Include))
        {
            if (!IsGeneratedTrigger(processor.gameObject))
            {
                candidates.Add(processor.gameObject);
            }
        }

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                string lowerName = transform.name.ToLowerInvariant();
                if ((lowerName.Contains("oxygen") || lowerName.Contains("o2") || lowerName.Contains("extractor") || lowerName.Contains("processor"))
                    && !IsGeneratedTrigger(transform.gameObject)
                    && transform.GetComponentInChildren<Renderer>(true) != null)
                {
                    candidates.Add(transform.gameObject);
                }
            }
        }

        GameObject best = null;
        int bestScore = int.MinValue;
        foreach (GameObject candidate in candidates)
        {
            if (candidate == null)
            {
                continue;
            }

            int score = ScoreCandidate(candidate);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static int ScoreCandidate(GameObject candidate)
    {
        string lowerName = candidate.name.ToLowerInvariant();
        int score = 0;

        if (candidate.GetComponent<OxygenProcessor>() != null)
        {
            score += 100;
        }

        if (candidate.GetComponentInChildren<O2ExtractorInteractable>(true) != null)
        {
            score += 80;
        }

        if (lowerName.Contains("oxygen"))
        {
            score += 30;
        }

        if (lowerName.Contains("o2"))
        {
            score += 30;
        }

        if (lowerName.Contains("extractor") || lowerName.Contains("processor"))
        {
            score += 25;
        }

        if (lowerName.Contains("trigger"))
        {
            score -= 100;
        }

        score += Mathf.Min(20, candidate.GetComponentsInChildren<Renderer>(true).Length);
        return score;
    }

    private static GameObject GetOrCreateReplacementModel(GameObject target, GameObject prefab)
    {
        Transform existing = target.transform.Find(ReplacementChildName);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return existing.gameObject;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(prefab);
        }

        instance.name = ReplacementChildName;
        instance.transform.SetParent(target.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static void DisableOldVisualRenderers(GameObject target, Transform replacement)
    {
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer.transform.IsChildOf(replacement))
            {
                continue;
            }

            Undo.RecordObject(renderer, "Hide old O2 processor visual");
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void EnsureUsefulCollider(GameObject target)
    {
        if (target.GetComponentInChildren<Collider>(true) != null)
        {
            return;
        }

        Bounds bounds = CalculateRendererBounds(target);
        BoxCollider collider = Undo.AddComponent<BoxCollider>(target);
        collider.center = target.transform.InverseTransformPoint(bounds.center);
        collider.size = bounds.size;
        collider.isTrigger = false;
        EditorUtility.SetDirty(collider);
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(root.transform.position, Vector3.one);
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled)
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

    private static void AssignMaterial(GameObject root, Material material)
    {
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
    }

    private static bool IsGeneratedTrigger(GameObject obj)
    {
        if (obj == null)
        {
            return false;
        }

        string lowerName = obj.name.ToLowerInvariant();
        return lowerName.Contains("interacttrigger") || lowerName.Contains("_interacttrigger") || lowerName.EndsWith("trigger");
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
        EnsureAssetFolder(AssetFolder);
        EnsureAssetFolder("Assets/_Materials");
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

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string BaseTexturePath => AssetFolder + "/" + SourceBaseName + ".png";
    private static string EmissionTexturePath => AssetFolder + "/" + SourceBaseName + "_emission.png";
    private static string MetallicTexturePath => AssetFolder + "/" + SourceBaseName + "_metallic.png";
    private static string NormalTexturePath => AssetFolder + "/" + SourceBaseName + "_normal.png";
    private static string RoughnessTexturePath => AssetFolder + "/" + SourceBaseName + "_roughness.png";
}
