using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SolarArrayImportPlacementUtility
{
    private const string SolarFolder = "Assets/_External/moon/SOLAR";
    private const string TextureFolder = SolarFolder + "/Textures";
    private const string MaterialFolder = SolarFolder + "/Materials";
    private const string PrefabFolder = "Assets/_Prefabs/Environment";
    private const string TextureFileName = "4494f57800ca7e0f9aa64a3e400ddaa0_medium.jpg";
    private const string TextureAssetPath = TextureFolder + "/" + TextureFileName;
    private const string PrefabAssetPath = PrefabFolder + "/SolarArray_01.prefab";
    private const string RequestedTextureSourcePath = @"C:\Users\khal\Downloads\solar-panel-fbx\textures\4494f57800ca7e0f9aa64a3e400ddaa0_medium.jpg";

    [MenuItem("Tools/Regolith/Import And Place Solar Array")]
    public static void ImportAndPlaceSolarArray()
    {
        EnsureFolders();

        if (!CopyOrResolveTexture())
        {
            return;
        }

        string fbxPath = FindSolarFbx();
        if (string.IsNullOrEmpty(fbxPath))
        {
            Debug.LogError("Solar array import: no FBX found in Assets/_External/moon/SOLAR/.");
            return;
        }

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxAsset == null)
        {
            Debug.LogError("Solar array import: FBX asset could not be loaded at " + fbxPath);
            return;
        }

        SolarMaterials materials = CreateOrUpdateMaterials();
        GameObject root = CreateSceneInstance(fbxAsset);

        ApplyMaterialOverrides(root, materials);
        AddSimpleColliders(root);
        PlaceSolarArray(root);
        SavePrefab(root);

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
    }

    private static void EnsureFolders()
    {
        EnsureAssetFolder(SolarFolder);
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

    private static bool CopyOrResolveTexture()
    {
        string sourcePath = ResolveTextureSourcePath();
        string destinationPath = AssetPathToAbsolutePath(TextureAssetPath);

        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, destinationPath, true);
            AssetDatabase.ImportAsset(TextureAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Solar array import: texture copied to " + TextureAssetPath);
            return true;
        }

        if (File.Exists(destinationPath))
        {
            AssetDatabase.ImportAsset(TextureAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.LogWarning("Solar array import: source texture was not found at " + RequestedTextureSourcePath + "; using existing imported texture at " + TextureAssetPath);
            return true;
        }

        Debug.LogError("Solar array import: texture source not found at " + RequestedTextureSourcePath + " and no imported texture exists at " + TextureAssetPath);
        return false;
    }

    private static string ResolveTextureSourcePath()
    {
        if (File.Exists(RequestedTextureSourcePath))
        {
            return RequestedTextureSourcePath;
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile))
        {
            return RequestedTextureSourcePath;
        }

        string sameUserDownloadsPath = Path.Combine(userProfile, "Downloads", "solar-panel-fbx", "textures", TextureFileName);
        return File.Exists(sameUserDownloadsPath) ? sameUserDownloadsPath : RequestedTextureSourcePath;
    }

    private static string FindSolarFbx()
    {
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { SolarFolder });
        string fbxPath = modelGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(fbxPath))
        {
            Debug.Log("Solar array import: FBX found at " + fbxPath);
        }

        return fbxPath;
    }

    private static SolarMaterials CreateOrUpdateMaterials()
    {
        Texture2D solarTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureAssetPath);
        if (solarTexture == null)
        {
            Debug.LogWarning("Solar array import: texture asset is not loaded yet. The solar cell material will be created without a base map.");
        }

        SolarMaterials materials = new SolarMaterials
        {
            Cell = CreateOrUpdateMaterial("Solar_Cell_Texture_Mat", Color.white, 0.1f, 0.35f, solarTexture),
            Frame = CreateOrUpdateMaterial("Solar_Frame_Mat", new Color(0.86f, 0.87f, 0.84f), 0.2f, 0.25f, null),
            Support = CreateOrUpdateMaterial("Solar_Support_Mat", new Color(0.18f, 0.19f, 0.2f), 0.45f, 0.25f, null)
        };

        AssetDatabase.SaveAssets();
        Debug.Log("Solar array import: materials created or updated in " + MaterialFolder);
        return materials;
    }

    private static Material CreateOrUpdateMaterial(string materialName, Color baseColor, float metallic, float smoothness, Texture2D baseMap)
    {
        string path = MaterialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = FindLitShader();

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        SetMaterialColor(material, baseColor);
        SetMaterialTexture(material, baseMap);
        SetMaterialFloat(material, "_Metallic", metallic);
        SetMaterialFloat(material, "_Smoothness", smoothness);
        SetMaterialFloat(material, "_Glossiness", smoothness);

        EditorUtility.SetDirty(material);
        return material;
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
        GameObject existing = GameObject.Find("SolarArray_01");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject root = new GameObject("SolarArray_01");
        Undo.RegisterCreatedObjectUndo(root, "Create Solar Array");

        GameObject instance = PrefabUtility.InstantiatePrefab(fbxAsset, activeScene) as GameObject;
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(fbxAsset);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Instantiate Solar Array FBX");
        instance.name = fbxAsset.name;
        instance.transform.SetParent(root.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        return root;
    }

    private static void ApplyMaterialOverrides(GameObject root, SolarMaterials materials)
    {
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer renderer in renderers)
        {
            Material material = PickMaterial(renderer, materials);
            Undo.RecordObject(renderer, "Override Solar Array Material");
            renderer.sharedMaterials = FillMaterialSlots(renderer.sharedMaterials, material);
            EditorUtility.SetDirty(renderer);
        }
    }

    private static Material PickMaterial(MeshRenderer renderer, SolarMaterials materials)
    {
        string lowerName = renderer.gameObject.name.ToLowerInvariant();
        bool flat = IsFlatPanel(renderer);
        bool supportLike = IsSupportLike(renderer);

        if (ContainsAny(lowerName, "cylinder", "support", "leg", "brace"))
        {
            return materials.Support;
        }

        if ((ContainsAny(lowerName, "panel", "solar", "cell") || lowerName.Contains("cube")) && flat)
        {
            return materials.Cell;
        }

        if (ContainsAny(lowerName, "frame") || LooksWhite(renderer))
        {
            return materials.Frame;
        }

        if (flat)
        {
            return materials.Cell;
        }

        return supportLike ? materials.Support : materials.Frame;
    }

    private static void AddSimpleColliders(GameObject root)
    {
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer renderer in renderers)
        {
            GameObject target = renderer.gameObject;
            Mesh mesh = GetSharedMesh(target);
            Bounds bounds = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);

            if (target.GetComponent<Collider>() != null)
            {
                continue;
            }

            if (IsSupportLike(renderer))
            {
                CapsuleCollider capsule = Undo.AddComponent<CapsuleCollider>(target);
                ConfigureCapsule(capsule, bounds);
            }
            else
            {
                BoxCollider box = Undo.AddComponent<BoxCollider>(target);
                box.center = bounds.center;
                box.size = bounds.size;
            }
        }
    }

    private static void ConfigureCapsule(CapsuleCollider capsule, Bounds bounds)
    {
        Vector3 size = bounds.size;
        capsule.center = bounds.center;

        if (size.x >= size.y && size.x >= size.z)
        {
            capsule.direction = 0;
            capsule.height = Mathf.Max(size.x, 0.01f);
            capsule.radius = Mathf.Max(Mathf.Min(size.y, size.z) * 0.5f, 0.01f);
        }
        else if (size.z >= size.x && size.z >= size.y)
        {
            capsule.direction = 2;
            capsule.height = Mathf.Max(size.z, 0.01f);
            capsule.radius = Mathf.Max(Mathf.Min(size.x, size.y) * 0.5f, 0.01f);
        }
        else
        {
            capsule.direction = 1;
            capsule.height = Mathf.Max(size.y, 0.01f);
            capsule.radius = Mathf.Max(Mathf.Min(size.x, size.z) * 0.5f, 0.01f);
        }
    }

    private static void PlaceSolarArray(GameObject root)
    {
        Vector3 targetPosition = ResolvePlacementPosition();
        Quaternion targetRotation = Quaternion.Euler(0f, ResolveSunFacingYaw(), 0f);

        Undo.RecordObject(root.transform, "Place Solar Array");
        root.transform.position = targetPosition;
        root.transform.rotation = targetRotation;
        root.transform.localScale = Vector3.one;
        SnapBoundsToTerrain(root);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Solar array import: scene object placed at " + root.transform.position + " with rotation " + root.transform.eulerAngles);
    }

    private static Vector3 ResolvePlacementPosition()
    {
        Transform solarPad = FindSceneTransform("REF_SolarPad");
        if (solarPad != null)
        {
            return solarPad.position;
        }

        Transform airlock = FindSceneTransform("Airlock") ?? FindSceneTransform("REF_AirlockPlacement") ?? FindSceneTransform("Base Entrance");
        if (airlock != null)
        {
            return airlock.position + new Vector3(34f, 0f, 5f);
        }

        return new Vector3(42f, 4.3f, 12f);
    }

    private static Transform FindSceneTransform(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    private static float ResolveSunFacingYaw()
    {
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        Light directional = lights.FirstOrDefault(light => light != null && light.type == LightType.Directional);
        return directional != null ? directional.transform.eulerAngles.y : 152f;
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

        float yOffset = terrainY - bounds.min.y;
        root.transform.position += new Vector3(0f, yOffset, 0f);
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

    private static void SavePrefab(GameObject root)
    {
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabAssetPath, InteractionMode.UserAction);
        if (savedPrefab == null)
        {
            Debug.LogError("Solar array import: prefab could not be saved at " + PrefabAssetPath);
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Solar array import: prefab saved at " + PrefabAssetPath);
    }

    private static bool IsFlatPanel(MeshRenderer renderer)
    {
        Vector3 size = renderer.bounds.size;
        float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        return largest > 0.01f && smallest <= largest * 0.12f;
    }

    private static bool IsSupportLike(MeshRenderer renderer)
    {
        string lowerName = renderer.gameObject.name.ToLowerInvariant();
        if (ContainsAny(lowerName, "cylinder", "support", "leg", "brace"))
        {
            return true;
        }

        Vector3 size = renderer.bounds.size;
        float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float middle = size.x + size.y + size.z - largest - smallest;

        return largest > 0.01f && middle <= largest * 0.22f && smallest <= largest * 0.22f;
    }

    private static bool LooksWhite(MeshRenderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        if (materials == null)
        {
            return false;
        }

        foreach (Material material in materials)
        {
            if (material == null)
            {
                continue;
            }

            if (material.name.ToLowerInvariant().Contains("white"))
            {
                return true;
            }

            Color color;
            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
            }
            else if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
            }
            else
            {
                continue;
            }

            if (color.r > 0.72f && color.g > 0.72f && color.b > 0.72f)
            {
                return true;
            }
        }

        return false;
    }

    private static Mesh GetSharedMesh(GameObject target)
    {
        MeshFilter meshFilter = target.GetComponent<MeshFilter>();
        return meshFilter != null ? meshFilter.sharedMesh : null;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        foreach (string term in terms)
        {
            if (value.Contains(term))
            {
                return true;
            }
        }

        return false;
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

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetMaterialTexture(Material material, Texture2D texture)
    {
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
    }

    private static void SetMaterialFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private sealed class SolarMaterials
    {
        public Material Cell;
        public Material Frame;
        public Material Support;
    }
}
