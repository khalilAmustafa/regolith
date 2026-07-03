using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RegolithGeneratorImportUtility
{
    private const string GeneratorFolder = "Assets/_External/moon/Generator";
    private const string TextureFolder = GeneratorFolder + "/Textures";
    private const string MaterialFolder = GeneratorFolder + "/Materials";
    private const string PrefabFolder = "Assets/_Prefabs/Environment";
    private const string FbxAssetPath = GeneratorFolder + "/Meshy_Generator_Module.fbx";
    private const string MaterialAssetPath = MaterialFolder + "/Generator_Module_Mat.mat";
    private const string PrefabAssetPath = PrefabFolder + "/Generator_Module_01.prefab";

    private const string SourceFolderName = "Meshy_AI_Fusion_Core_Module_0701164122_texture_fbx";
    private const string SourceFbxName = "Meshy_AI_Fusion_Core_Module_0701164122_texture.fbx";
    private const string BaseTextureName = "Meshy_AI_Fusion_Core_Module_0701164122_texture.png";
    private const string EmissionTextureName = "Meshy_AI_Fusion_Core_Module_0701164122_texture_emission.png";
    private const string MetallicTextureName = "Meshy_AI_Fusion_Core_Module_0701164122_texture_metallic.png";
    private const string NormalTextureName = "Meshy_AI_Fusion_Core_Module_0701164122_texture_normal.png";
    private const string RoughnessTextureName = "Meshy_AI_Fusion_Core_Module_0701164122_texture_roughness.png";
    private const string RequestedSourceFolder = @"C:\Users\khal\Downloads\Meshy_AI_Fusion_Core_Module_0701164122_texture_fbx\Meshy_AI_Fusion_Core_Module_0701164122_texture_fbx";

    private static readonly string[] TextureNames =
    {
        BaseTextureName,
        EmissionTextureName,
        MetallicTextureName,
        NormalTextureName,
        RoughnessTextureName
    };

    [MenuItem("Tools/Regolith/Import And Place Generator Module")]
    public static void ImportAndPlaceGeneratorModule()
    {
        EnsureFolders();

        string sourceFolder = ResolveSourceFolder();
        if (string.IsNullOrEmpty(sourceFolder))
        {
            Debug.LogError("Generator import: source folder not found at " + RequestedSourceFolder + " or current-user Downloads fallback.");
            return;
        }

        if (!CopyFbx(sourceFolder) || !CopyTextures(sourceFolder))
        {
            return;
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        ConfigureTextureImportSettings();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        Material material = CreateOrUpdateGeneratorMaterial();
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxAssetPath);
        if (fbxAsset == null)
        {
            Debug.LogError("Generator import: copied FBX could not be loaded at " + FbxAssetPath);
            return;
        }

        Transform solarTransform = FindSolarTransform();
        GameObject root = CreateSceneInstance(fbxAsset);
        ApplyMaterialOverrides(root, material);
        AdjustScaleIfNeeded(root);
        PlaceGenerator(root, solarTransform);
        AddConnectionPoints(root);
        AddSimpleRootCollider(root);
        SavePrefab(root);

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
    }

    [MenuItem("Tools/Regolith/Apply Generator Texture To Existing Module")]
    public static void ApplyGeneratorTextureToExistingModule()
    {
        EnsureFolders();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        ConfigureTextureImportSettings();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        Material material = CreateOrUpdateGeneratorMaterial();
        GameObject target = ResolveExistingGeneratorRoot();
        if (target == null)
        {
            Debug.LogError("Generator texture apply: select the generator object, or add a scene object named Generator_Module_01.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(target, "Apply Generator Texture");
        ApplyMaterialOverrides(target, material);
        EditorSceneManager.MarkSceneDirty(target.scene);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = target;
        EditorGUIUtility.PingObject(target);
        Debug.Log("Generator texture apply: texture material applied to " + target.name + ".");
    }

    private static void EnsureFolders()
    {
        EnsureAssetFolder(GeneratorFolder);
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
        if (Directory.Exists(RequestedSourceFolder))
        {
            return RequestedSourceFolder;
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile))
        {
            return null;
        }

        string fallback = Path.Combine(userProfile, "Downloads", SourceFolderName, SourceFolderName);
        return Directory.Exists(fallback) ? fallback : null;
    }

    private static bool CopyFbx(string sourceFolder)
    {
        string sourcePath = Path.Combine(sourceFolder, SourceFbxName);
        if (!File.Exists(sourcePath))
        {
            Debug.LogError("Generator import: source FBX not found at " + sourcePath);
            return false;
        }

        File.Copy(sourcePath, AssetPathToAbsolutePath(FbxAssetPath), true);
        Debug.Log("Generator import: copied FBX to " + FbxAssetPath);
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
                Debug.LogError("Generator import: source texture not found at " + sourcePath);
                return false;
            }

            string destinationPath = AssetPathToAbsolutePath(TextureFolder + "/" + textureName);
            File.Copy(sourcePath, destinationPath, true);
            copied.Add(textureName);
        }

        Debug.Log("Generator import: copied textures to " + TextureFolder + ": " + string.Join(", ", copied));
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
            Debug.LogWarning("Generator import: texture importer not found for " + assetPath);
            return;
        }

        if (importer.textureType != textureType)
        {
            importer.textureType = textureType;
            importer.SaveAndReimport();
        }
    }

    private static Material CreateOrUpdateGeneratorMaterial()
    {
        Texture2D baseTexture = LoadTexture(BaseTextureName);
        Texture2D emissionTexture = LoadTexture(EmissionTextureName);
        Texture2D metallicTexture = LoadTexture(MetallicTextureName);
        Texture2D normalTexture = LoadTexture(NormalTextureName);

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

        SetMaterialTexture(material, "_BaseMap", "_MainTex", baseTexture);
        SetMaterialTexture(material, "_BumpMap", null, normalTexture);
        SetMaterialTexture(material, "_EmissionMap", null, emissionTexture);
        SetMaterialTexture(material, "_MetallicGlossMap", null, metallicTexture);
        SetMaterialColor(material, "_BaseColor", Color.white);
        SetMaterialColor(material, "_Color", Color.white);
        SetMaterialFloat(material, "_Metallic", 0.4f);
        SetMaterialFloat(material, "_Smoothness", 0.35f);
        SetMaterialFloat(material, "_Glossiness", 0.35f);
        SetMaterialFloat(material, "_BumpScale", 1f);

        if (normalTexture != null)
        {
            material.EnableKeyword("_NORMALMAP");
        }

        if (metallicTexture != null)
        {
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (emissionTexture != null)
        {
            material.EnableKeyword("_EMISSION");
            SetMaterialColor(material, "_EmissionColor", new Color(0.38f, 0.72f, 0.85f) * 0.45f);
        }

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        Debug.Log("Generator import: created material at " + MaterialAssetPath);
        Debug.Log("Generator import: assigned textures to Generator_Module_Mat. Roughness map is copied but not connected directly; Smoothness is set to 0.35.");
        return material;
    }

    private static GameObject ResolveExistingGeneratorRoot()
    {
        if (Selection.activeGameObject != null)
        {
            return Selection.activeGameObject.transform.root.gameObject;
        }

        GameObject named = GameObject.Find("Generator_Module_01");
        if (named != null)
        {
            return named;
        }

        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
        return objects
            .Where(sceneObject => sceneObject != null)
            .Where(sceneObject =>
                sceneObject.name.IndexOf("Generator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneObject.name.IndexOf("Meshy", StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(sceneObject => sceneObject.transform.parent == null ? 0 : 1)
            .ThenBy(sceneObject => sceneObject.name, StringComparer.Ordinal)
            .FirstOrDefault();
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
        GameObject existing = GameObject.Find("Generator_Module_01");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject root = new GameObject("Generator_Module_01");
        Undo.RegisterCreatedObjectUndo(root, "Create Generator Module");

        GameObject instance = PrefabUtility.InstantiatePrefab(fbxAsset, activeScene) as GameObject;
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(fbxAsset);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Instantiate Generator Module FBX");
        instance.name = fbxAsset.name;
        instance.transform.SetParent(root.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        Debug.Log("Generator import: instantiated generator from " + FbxAssetPath);
        return root;
    }

    private static void ApplyMaterialOverrides(GameObject root, Material material)
    {
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer renderer in renderers)
        {
            Undo.RecordObject(renderer, "Override Generator Material");
            renderer.sharedMaterials = FillMaterialSlots(renderer.sharedMaterials, material);
            EditorUtility.SetDirty(renderer);
        }

        Debug.Log("Generator import: applied material overrides to " + renderers.Length + " MeshRenderer(s).");
    }

    private static void AdjustScaleIfNeeded(GameObject root)
    {
        Bounds bounds = CalculateRendererBounds(root);
        float largestDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

        Undo.RecordObject(root.transform, "Scale Generator Module");
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

    private static void PlaceGenerator(GameObject root, Transform solarTransform)
    {
        Vector3 targetPosition = ResolvePlacementPosition(solarTransform);
        Quaternion targetRotation = ResolvePlacementRotation(targetPosition, solarTransform);

        Undo.RecordObject(root.transform, "Place Generator Module");
        root.transform.position = targetPosition;
        root.transform.rotation = targetRotation;
        SnapBoundsToTerrain(root);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Generator import: placed near solar array at " + root.transform.position + " with rotation " + root.transform.eulerAngles);
    }

    private static Vector3 ResolvePlacementPosition(Transform solarTransform)
    {
        if (solarTransform != null)
        {
            return solarTransform.position + new Vector3(3.5f, 0f, 1.5f);
        }

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            return sceneView.pivot;
        }

        return new Vector3(45.5f, 4.3f, 13.5f);
    }

    private static Quaternion ResolvePlacementRotation(Vector3 generatorPosition, Transform solarTransform)
    {
        if (solarTransform == null)
        {
            return Quaternion.identity;
        }

        Vector3 toSolar = solarTransform.position - generatorPosition;
        toSolar.y = 0f;
        if (toSolar.sqrMagnitude < 0.001f)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(toSolar.normalized, Vector3.up);
    }

    private static Transform FindSolarTransform()
    {
        GameObject solarArray = GameObject.Find("SolarArray_01");
        if (solarArray != null)
        {
            return solarArray.transform;
        }

        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
        GameObject candidate = objects
            .Where(sceneObject => sceneObject != null && sceneObject.name.IndexOf("Solar", StringComparison.OrdinalIgnoreCase) >= 0)
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

    private static void AddConnectionPoints(GameObject root)
    {
        RemoveExistingChild(root.transform, "PipeConnectionPoint");
        RemoveExistingChild(root.transform, "InteractionPoint");

        Bounds bounds = CalculateRendererBounds(root);
        Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
        Vector3 localExtents = Abs(root.transform.InverseTransformVector(bounds.extents));

        GameObject pipePoint = new GameObject("PipeConnectionPoint");
        Undo.RegisterCreatedObjectUndo(pipePoint, "Create Pipe Connection Point");
        pipePoint.transform.SetParent(root.transform, false);
        pipePoint.transform.localPosition = localCenter + new Vector3(localExtents.x + 0.35f, 0f, 0f);
        pipePoint.transform.localRotation = Quaternion.identity;
        pipePoint.transform.localScale = Vector3.one;

        GameObject interactionPoint = new GameObject("InteractionPoint");
        Undo.RegisterCreatedObjectUndo(interactionPoint, "Create Interaction Point");
        interactionPoint.transform.SetParent(root.transform, false);
        interactionPoint.transform.localPosition = localCenter + new Vector3(0f, 0f, localExtents.z + 1.2f);
        interactionPoint.transform.localRotation = Quaternion.identity;
        interactionPoint.transform.localScale = Vector3.one;

        Debug.Log("Generator import: added PipeConnectionPoint and InteractionPoint.");
    }

    private static void RemoveExistingChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }
    }

    private static void AddSimpleRootCollider(GameObject root)
    {
        Collider[] existingColliders = root.GetComponentsInChildren<Collider>(true);
        if (existingColliders.Length > 0)
        {
            Debug.Log("Generator import: existing collider(s) found; no root BoxCollider added.");
            return;
        }

        Bounds bounds = CalculateRendererBounds(root);
        if (bounds.size == Vector3.zero)
        {
            Debug.LogWarning("Generator import: could not calculate renderer bounds, so no collider was added.");
            return;
        }

        BoxCollider collider = Undo.AddComponent<BoxCollider>(root);
        collider.center = root.transform.InverseTransformPoint(bounds.center);
        collider.size = Abs(root.transform.InverseTransformVector(bounds.size));
        Debug.Log("Generator import: added simple root BoxCollider.");
    }

    private static void SavePrefab(GameObject root)
    {
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabAssetPath, InteractionMode.UserAction);
        if (savedPrefab == null)
        {
            Debug.LogError("Generator import: prefab could not be saved at " + PrefabAssetPath);
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Generator import: saved prefab at " + PrefabAssetPath);
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
