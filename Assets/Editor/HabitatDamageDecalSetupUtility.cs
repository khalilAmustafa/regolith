using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HabitatDamageDecalSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Habitat Crack Damage Decal";
    private const string TextureFolder = "Assets/_Textures/Damage";
    private const string MaterialFolder = "Assets/_Materials/Damage";
    private const string CrackTexturePath = TextureFolder + "/Crack_Impact_01.png";
    private const string CrackMaterialPath = MaterialFolder + "/Crack_Impact_Mat.mat";
    private const string PatchMaterialPath = MaterialFolder + "/Regolith_Concrete_Patch_Mat.mat";
    private const string CrackObjectName = "Crack_Impact_01";
    private const string PatchObjectName = "ConcretePatch_01";

    [MenuItem(MenuPath)]
    public static void SetupHabitatCrackDamageDecal()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Habitat crack damage decal setup cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        EnsureFolders();

        string sourcePng = FindNewestCrackPng();
        bool textureCopied = CopyCrackTexture(sourcePng);
        ConfigureCrackTextureImporter();

        bool mayNeedCleanup = TextureMayNeedTransparencyCleanup(CrackTexturePath);
        if (mayNeedCleanup)
        {
            Debug.LogWarning("Crack texture may need transparent background cleanup.");
        }

        Material crackMaterial = CreateOrUpdateCrackMaterial();
        Material patchMaterial = CreateOrUpdatePatchMaterial();

        GameObject habitat = GameObject.Find("Habitat_01");
        if (habitat == null)
        {
            Debug.LogError("Habitat crack damage decal setup stopped: Habitat_01 was not found in the active scene.");
            return;
        }

        GameObject crack = CreateOrUpdateCrackQuad(habitat, crackMaterial);
        GameObject patch = CreateOrUpdateConcretePatch(habitat, patchMaterial, crack);
        HabitatDamageVisualState visualState = habitat.GetComponent<HabitatDamageVisualState>();
        if (visualState == null)
        {
            visualState = Undo.AddComponent<HabitatDamageVisualState>(habitat);
        }
        else
        {
            Undo.RecordObject(visualState, "Configure habitat damage visual state");
        }

        visualState.crackObject = crack;
        visualState.concretePatchObject = patch;
        visualState.ShowDamaged();

        EditorUtility.SetDirty(visualState);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Habitat crack damage decal setup complete:\n" +
            "- Crack texture copied: " + (textureCopied ? "yes" : "reused existing/missing source") + "\n" +
            "- Crack texture path: " + CrackTexturePath + "\n" +
            "- Material created: " + (crackMaterial != null ? "yes" : "no") + "\n" +
            "- Crack quad placed: " + (crack != null ? "yes" : "no") + "\n" +
            "- Patch object created: " + (patch != null ? "yes" : "no") + "\n" +
            "- Visual state script added: " + (visualState != null ? "yes" : "no"));
    }

    private static string FindNewestCrackPng()
    {
        List<string> searchFolders = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            @"C:\Users\khal\Downloads",
            @"C:\Users\khali\Downloads"
        };

        List<FileInfo> candidates = new List<FileInfo>();
        foreach (string folder in searchFolders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            candidates.AddRange(new DirectoryInfo(folder).GetFiles("*.png", SearchOption.TopDirectoryOnly));
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("No PNG files found in Downloads for crack decal import.");
            return null;
        }

        FileInfo[] likelyCracks = candidates
            .Where(file => IsLikelyCrackName(file.Name))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();

        FileInfo[] selectionPool = likelyCracks.Length > 0
            ? likelyCracks
            : candidates.OrderByDescending(file => file.LastWriteTimeUtc).ToArray();

        if (selectionPool.Length > 1)
        {
            Debug.Log(
                "Crack PNG candidates found:\n" +
                string.Join("\n", selectionPool.Take(8).Select(file => "- " + file.Name)));
        }

        FileInfo selected = selectionPool[0];
        Debug.Log("Selected newest crack PNG: " + selected.Name);
        return selected.FullName;
    }

    private static bool IsLikelyCrackName(string fileName)
    {
        string lower = fileName.ToLowerInvariant();
        return lower.Contains("crack")
            || lower.Contains("damage")
            || lower.Contains("impact")
            || lower.Contains("decal")
            || lower.Contains("fracture")
            || lower.Contains("broken");
    }

    private static bool CopyCrackTexture(string sourcePng)
    {
        if (string.IsNullOrWhiteSpace(sourcePng) || !File.Exists(sourcePng))
        {
            if (File.Exists(AssetPathToAbsolutePath(CrackTexturePath)))
            {
                Debug.LogWarning("Crack texture source not found. Reusing existing " + CrackTexturePath + ".");
                return false;
            }

            Debug.LogError("Crack texture source not found and no existing project crack texture is available.");
            return false;
        }

        File.Copy(sourcePng, AssetPathToAbsolutePath(CrackTexturePath), true);
        AssetDatabase.ImportAsset(CrackTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        Debug.Log("Crack texture copied: " + sourcePng + " -> " + CrackTexturePath);
        return true;
    }

    private static void ConfigureCrackTextureImporter()
    {
        TextureImporter importer = AssetImporter.GetAtPath(CrackTexturePath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.compressionQuality = 50;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    private static bool TextureMayNeedTransparencyCleanup(string assetPath)
    {
        string absolutePath = AssetPathToAbsolutePath(assetPath);
        if (!File.Exists(absolutePath))
        {
            return false;
        }

        byte[] bytes = File.ReadAllBytes(absolutePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            return false;
        }

        Color[] pixels = texture.GetPixels();
        int transparentCount = 0;
        int whiteCornerCount = 0;
        Color[] corners =
        {
            texture.GetPixel(0, 0),
            texture.GetPixel(texture.width - 1, 0),
            texture.GetPixel(0, texture.height - 1),
            texture.GetPixel(texture.width - 1, texture.height - 1)
        };

        foreach (Color pixel in pixels)
        {
            if (pixel.a < 0.95f)
            {
                transparentCount++;
            }
        }

        foreach (Color corner in corners)
        {
            if (corner.r > 0.9f && corner.g > 0.9f && corner.b > 0.9f && corner.a > 0.9f)
            {
                whiteCornerCount++;
            }
        }

        UnityEngine.Object.DestroyImmediate(texture);
        return transparentCount < pixels.Length * 0.02f && whiteCornerCount >= 3;
    }

    private static Material CreateOrUpdateCrackMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CrackMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, CrackMaterialPath);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CrackTexturePath);
        if (texture != null)
        {
            SetTexture(material, "_BaseMap", "_MainTex", texture);
        }

        SetColor(material, "_BaseColor", "_Color", Color.white);
        ConfigureTransparentMaterial(material);
        EditorUtility.SetDirty(material);
        Debug.Log("Crack material created/updated: " + CrackMaterialPath);
        return material;
    }

    private static Material CreateOrUpdatePatchMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(PatchMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, PatchMaterialPath);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        SetColor(material, "_BaseColor", "_Color", new Color(0.62f, 0.64f, 0.61f, 1f));
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.15f);
        }

        EditorUtility.SetDirty(material);
        Debug.Log("Concrete patch material created/updated: " + PatchMaterialPath);
        return material;
    }

    private static GameObject CreateOrUpdateCrackQuad(GameObject habitat, Material material)
    {
        Transform existing = habitat.transform.Find(CrackObjectName);
        GameObject quad = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Quad);
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(quad, "Create crack impact quad");
            quad.name = CrackObjectName;
            quad.transform.SetParent(habitat.transform, true);
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(quad, "Update crack impact quad");
        }

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        PositionDamageObject(habitat, quad.transform, 0.035f);
        quad.transform.localScale = new Vector3(1.25f, 1.25f, 1f);

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        quad.SetActive(true);
        EditorUtility.SetDirty(quad);
        Debug.Log("Crack quad placed: " + CrackObjectName);
        return quad;
    }

    private static GameObject CreateOrUpdateConcretePatch(GameObject habitat, Material material, GameObject crack)
    {
        Transform existing = habitat.transform.Find(PatchObjectName);
        GameObject patch = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(patch, "Create concrete patch");
            patch.name = PatchObjectName;
            patch.transform.SetParent(habitat.transform, true);
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(patch, "Update concrete patch");
        }

        if (crack != null)
        {
            patch.transform.SetPositionAndRotation(
                crack.transform.position + crack.transform.forward * 0.015f,
                crack.transform.rotation);
        }
        else
        {
            PositionDamageObject(habitat, patch.transform, 0.055f);
        }

        patch.transform.localScale = new Vector3(1.35f, 1.35f, 0.045f);
        MeshRenderer renderer = patch.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        patch.SetActive(false);

        EditorUtility.SetDirty(patch);
        Debug.Log("Patch object created/updated: " + PatchObjectName);
        return patch;
    }

    private static void PositionDamageObject(GameObject habitat, Transform target, float offset)
    {
        Bounds bounds = CalculateRendererBounds(habitat);
        Vector3 right = habitat.transform.right.normalized;
        Vector3 up = habitat.transform.up.normalized;
        Vector3 forward = habitat.transform.forward.normalized;

        Vector3 position = bounds.center
            + right * (Mathf.Max(1f, bounds.extents.x) + offset)
            + up * Mathf.Clamp(bounds.extents.y * 0.16f, 0.35f, 1.1f)
            - forward * Mathf.Clamp(bounds.extents.z * 0.18f, 0.25f, 1.1f);

        Quaternion rotation = Quaternion.LookRotation(right, up);
        target.SetPositionAndRotation(position, rotation);
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(root.transform.position, Vector3.one);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer.gameObject.name == CrackObjectName || renderer.gameObject.name == PatchObjectName)
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

    private static void ConfigureTransparentMaterial(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private static void SetTexture(Material material, string primaryProperty, string fallbackProperty, Texture texture)
    {
        if (material.HasProperty(primaryProperty))
        {
            material.SetTexture(primaryProperty, texture);
        }
        else if (material.HasProperty(fallbackProperty))
        {
            material.SetTexture(fallbackProperty, texture);
        }
    }

    private static void SetColor(Material material, string primaryProperty, string fallbackProperty, Color color)
    {
        if (material.HasProperty(primaryProperty))
        {
            material.SetColor(primaryProperty, color);
        }
        else if (material.HasProperty(fallbackProperty))
        {
            material.SetColor(fallbackProperty, color);
        }
    }

    private static void EnsureFolders()
    {
        EnsureAssetFolder("Assets/_Textures");
        EnsureAssetFolder(TextureFolder);
        EnsureAssetFolder("Assets/_Materials");
        EnsureAssetFolder(MaterialFolder);
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
}
