using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ImpactCrackSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Impact Crack Decal";
    private const string TriggerPath = "Assets/Editor/ImpactCrackSetupTrigger.txt";
    private const string SourceTexturePath = @"C:\Users\khali\Downloads\CRACK.PNG";
    private const string TexturePath = "Assets/_Art/Decals/CRACK.PNG";
    private const string MaterialPath = "Assets/_Materials/M_ImpactCrack.mat";
    private const string PrefabPath = "Assets/_Prefabs/ImpactCrack.prefab";
    private const string ImpactPointName = "Asteroid_ImpactPoint";

    static ImpactCrackSetupUtility()
    {
        EditorApplication.delayCall += RunTriggeredSetup;
    }

    [MenuItem(MenuPath)]
    public static void SetupImpactCrackDecal()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Impact crack setup cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        EnsureFolders();
        CopyTextureIfAvailable();
        ConfigureTextureImporter();

        Material material = CreateOrUpdateMaterial();
        GameObject prefab = CreateOrUpdatePrefab(material);
        ImpactCrackController controller = AddOrUpdateSceneController(prefab);
        WireCinematic(controller);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log(
            "Impact crack setup complete:\n" +
            "- Texture: " + TexturePath + "\n" +
            "- Material: " + MaterialPath + "\n" +
            "- Prefab: " + PrefabPath + "\n" +
            "- Controller attached to: " + (controller != null ? controller.gameObject.name : "missing") + "\n" +
            "- Cinematic wired: " + (controller != null ? "yes" : "no"));
    }

    private static void RunTriggeredSetup()
    {
        if (!File.Exists(AssetPathToAbsolutePath(TriggerPath)))
        {
            return;
        }

        try
        {
            SetupImpactCrackDecal();
        }
        finally
        {
            File.Delete(AssetPathToAbsolutePath(TriggerPath));
            AssetDatabase.Refresh();
        }
    }

    private static void CopyTextureIfAvailable()
    {
        string destinationPath = AssetPathToAbsolutePath(TexturePath);
        if (File.Exists(SourceTexturePath))
        {
            File.Copy(SourceTexturePath, destinationPath, true);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return;
        }

        if (!File.Exists(destinationPath))
        {
            Debug.LogError("Impact crack texture source was not found at " + SourceTexturePath + ".");
        }
    }

    private static void ConfigureTextureImporter()
    {
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.compressionQuality = 100;
        importer.SaveAndReimport();
    }

    private static Material CreateOrUpdateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");

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

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture != null)
        {
            SetTexture(material, "_BaseMap", "_MainTex", texture);
        }

        SetColor(material, "_BaseColor", "_Color", new Color(0.12f, 0.12f, 0.12f, 1f));
        ConfigureTransparentMaterial(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateOrUpdatePrefab(Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "ImpactCrack_01";
        quad.transform.localScale = new Vector3(30f, 30f, 1f);

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        quad.SetActive(false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(quad, PrefabPath);
        UnityEngine.Object.DestroyImmediate(quad);
        return prefab;
    }

    private static ImpactCrackController AddOrUpdateSceneController(GameObject prefab)
    {
        GameObject impactPointObject = GameObject.Find(ImpactPointName);
        if (impactPointObject == null)
        {
            Debug.LogWarning("Impact crack setup could not find " + ImpactPointName + " in the active scene.");
            return null;
        }

        ImpactCrackController controller = impactPointObject.GetComponent<ImpactCrackController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<ImpactCrackController>(impactPointObject);
        }
        else
        {
            Undo.RecordObject(controller, "Configure impact crack controller");
        }

        controller.impactPoint = impactPointObject.transform;
        controller.crackPrefab = prefab;
        controller.positionOffset = Vector3.zero;
        controller.rotationOffsetEuler = Vector3.zero;
        controller.crackScale = new Vector3(30f, 30f, 1f);
        controller.parentToImpactPoint = true;
        controller.HideCrack();
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void WireCinematic(ImpactCrackController controller)
    {
        if (controller == null)
        {
            return;
        }

        MicrometeoriteOpeningCinematic cinematic = UnityEngine.Object.FindAnyObjectByType<MicrometeoriteOpeningCinematic>();
        if (cinematic == null)
        {
            Debug.LogWarning("Impact crack setup could not find MicrometeoriteOpeningCinematic in the active scene.");
            return;
        }

        Undo.RecordObject(cinematic, "Assign impact crack controller");
        cinematic.impactCrackController = controller;
        EditorUtility.SetDirty(cinematic);
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
        EnsureAssetFolder("Assets/_Art");
        EnsureAssetFolder("Assets/_Art/Decals");
        EnsureAssetFolder("Assets/_Materials");
        EnsureAssetFolder("Assets/_Prefabs");
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
