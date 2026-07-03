using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MoonTerrainLayerSetup
{
    private const string MoonMaterialFolder = "Assets/_Materials/Moon";
    private const string MoonTextureRoot = "Assets/_Materials/Moon/Textures";
    private const string DustLayerPath = MoonMaterialFolder + "/TL_Moon_Dust.terrainlayer";
    private const string RockLayerPath = MoonMaterialFolder + "/TL_Moon_Rock.terrainlayer";
    private const string ShadowLayerPath = MoonMaterialFolder + "/TL_Moon_Shadow.terrainlayer";

    private static readonly string[] TextureExtensions =
    {
        ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".exr", ".psd"
    };

    private static readonly string[] AlbedoTerms =
    {
        "albedo", "basecolor", "base_color", "diffuse", "color", "col"
    };

    private static readonly string[] NormalTerms =
    {
        "normal", "nrm", "nor"
    };

    private static readonly string[] HeightTerms =
    {
        "height", "disp", "displacement"
    };

    private static readonly string[] OptionalLinearTerms =
    {
        "rough", "roughness", "ao", "ambientocclusion", "ambient_occlusion"
    };

    [MenuItem("Tools/Regolith/Setup Moon Terrain Layers")]
    public static void SetupMoonTerrainLayers()
    {
        EnsureMoonMaterialFolder();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        List<TextureCandidate> textures = FindMoonTextures();
        if (textures.Count == 0)
        {
            Debug.LogWarning($"No moon textures found under {MoonTextureRoot}. Terrain layers will be created without texture maps.");
        }

        TextureCandidate dustAlbedo = PickBestTexture(textures, LayerRole.Dust, MapRole.Albedo);
        TextureCandidate dustNormal = PickBestMatchingNormal(textures, dustAlbedo, LayerRole.Dust);
        TextureCandidate rockAlbedo = PickBestTexture(textures, LayerRole.Rock, MapRole.Albedo);
        TextureCandidate rockNormal = PickBestMatchingNormal(textures, rockAlbedo, LayerRole.Rock);
        TextureCandidate shadowAlbedo = PickBestTexture(textures, LayerRole.Shadow, MapRole.Albedo) ?? dustAlbedo ?? rockAlbedo;
        TextureCandidate shadowNormal = PickBestMatchingNormal(textures, shadowAlbedo, LayerRole.Shadow) ?? dustNormal ?? rockNormal;

        ConfigureNormalMap(dustNormal);
        ConfigureNormalMap(rockNormal);
        ConfigureNormalMap(shadowNormal);
        ConfigureLinearTextureImport(textures);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        TerrainLayer dustLayer = CreateOrUpdateLayer(
            DustLayerPath,
            "TL_Moon_Dust",
            dustAlbedo,
            dustNormal,
            0.02f,
            Vector4.zero,
            Vector4.one);

        TerrainLayer rockLayer = CreateOrUpdateLayer(
            RockLayerPath,
            "TL_Moon_Rock",
            rockAlbedo,
            rockNormal,
            0.01f,
            Vector4.zero,
            Vector4.one);

        TerrainLayer shadowLayer = CreateOrUpdateLayer(
            ShadowLayerPath,
            "TL_Moon_Shadow",
            shadowAlbedo,
            shadowNormal,
            0f,
            new Vector4(0.18f, 0.19f, 0.2f, 1f),
            new Vector4(0.58f, 0.6f, 0.62f, 1f));

        AssignLayersToActiveTerrain(dustLayer, rockLayer, shadowLayer);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        LogSelection("TL_Moon_Dust", dustAlbedo, dustNormal);
        LogSelection("TL_Moon_Rock", rockAlbedo, rockNormal);
        LogSelection("TL_Moon_Shadow", shadowAlbedo, shadowNormal);
        Debug.Log("Moon terrain layer setup complete.");
    }

    private static void EnsureMoonMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "_Materials");
        }

        if (!AssetDatabase.IsValidFolder(MoonMaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/_Materials", "Moon");
        }
    }

    private static List<TextureCandidate> FindMoonTextures()
    {
        if (!AssetDatabase.IsValidFolder(MoonTextureRoot))
        {
            return new List<TextureCandidate>();
        }

        return AssetDatabase.FindAssets("t:Texture", new[] { MoonTextureRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => TextureExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            .Select(TextureCandidate.FromPath)
            .Where(candidate => candidate.MapRole != MapRole.Unknown)
            .OrderBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static TextureCandidate PickBestTexture(List<TextureCandidate> textures, LayerRole layerRole, MapRole mapRole)
    {
        return textures
            .Where(candidate => candidate.MapRole == mapRole)
            .OrderByDescending(candidate => Score(candidate, layerRole))
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static TextureCandidate PickBestMatchingNormal(List<TextureCandidate> textures, TextureCandidate albedo, LayerRole layerRole)
    {
        return textures
            .Where(candidate => candidate.MapRole == MapRole.Normal)
            .OrderByDescending(candidate => ScoreNormal(candidate, albedo, layerRole))
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int Score(TextureCandidate candidate, LayerRole layerRole)
    {
        int score = 0;

        switch (layerRole)
        {
            case LayerRole.Dust:
                score += candidate.PathLower.Contains("moon02") || candidate.PathLower.Contains("moon_02") ? 100 : 0;
                score += candidate.PathLower.Contains("dust") ? 30 : 0;
                score += candidate.PathLower.Contains("lunarrock") || candidate.PathLower.Contains("lunar-rock") ? -20 : 0;
                break;
            case LayerRole.Rock:
                score += candidate.PathLower.Contains("lunarrock") || candidate.PathLower.Contains("lunar-rock") ? 100 : 0;
                score += candidate.FileNameLower.Contains("rock") ? 30 : 0;
                score += candidate.PathLower.Contains("moon01") || candidate.PathLower.Contains("moon_01") ? 10 : 0;
                break;
            case LayerRole.Shadow:
                score += candidate.FileNameLower.Contains("shadow") ? 120 : 0;
                score += candidate.FileNameLower.Contains("crater") ? 100 : 0;
                score += candidate.FileNameLower.Contains("dark") ? 90 : 0;
                score += candidate.PathLower.Contains("moon02") || candidate.PathLower.Contains("moon_02") ? 20 : 0;
                break;
        }

        score += candidate.FileNameLower.Contains("diff") ? 8 : 0;
        score += candidate.FileNameLower.Contains("albedo") ? 8 : 0;
        score += candidate.FileNameLower.Contains("base") ? 6 : 0;

        return score;
    }

    private static int ScoreNormal(TextureCandidate normal, TextureCandidate albedo, LayerRole layerRole)
    {
        int score = Score(normal, layerRole);

        if (albedo != null)
        {
            score += string.Equals(normal.Directory, albedo.Directory, StringComparison.OrdinalIgnoreCase) ? 80 : 0;
            score += SharedPrefixLength(normal.FileNameLower, albedo.FileNameLower);
        }

        return score;
    }

    private static int SharedPrefixLength(string a, string b)
    {
        int length = Math.Min(a.Length, b.Length);
        int shared = 0;
        for (int i = 0; i < length; i++)
        {
            if (a[i] != b[i])
            {
                break;
            }

            shared++;
        }

        return shared;
    }

    private static void ConfigureNormalMap(TextureCandidate candidate)
    {
        if (candidate == null)
        {
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(candidate.Path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Could not configure normal map importer for {candidate.Path}.");
            return;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            changed = true;
        }

        if (importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
            Debug.Log($"Configured moon normal map import settings: {candidate.Path}");
        }
    }

    private static void ConfigureLinearTextureImport(List<TextureCandidate> textures)
    {
        foreach (TextureCandidate candidate in textures.Where(candidate => candidate.MapRole == MapRole.Linear))
        {
            TextureImporter importer = AssetImporter.GetAtPath(candidate.Path) as TextureImporter;
            if (importer == null || !importer.sRGBTexture)
            {
                continue;
            }

            importer.sRGBTexture = false;
            importer.SaveAndReimport();
            Debug.Log($"Configured moon linear texture import settings: {candidate.Path}");
        }
    }

    private static TerrainLayer CreateOrUpdateLayer(
        string path,
        string layerName,
        TextureCandidate albedoCandidate,
        TextureCandidate normalCandidate,
        float smoothness,
        Vector4 diffuseRemapMin,
        Vector4 diffuseRemapMax)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }

        layer.name = layerName;
        layer.diffuseTexture = LoadTexture(albedoCandidate);
        layer.normalMapTexture = LoadTexture(normalCandidate);
        layer.tileSize = new Vector2(4f, 4f);
        layer.tileOffset = Vector2.zero;
        layer.metallic = 0f;
        layer.smoothness = smoothness;
        layer.specular = Color.black;
        layer.diffuseRemapMin = diffuseRemapMin;
        layer.diffuseRemapMax = diffuseRemapMax;

        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static Texture2D LoadTexture(TextureCandidate candidate)
    {
        return candidate == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(candidate.Path);
    }

    private static void AssignLayersToActiveTerrain(TerrainLayer dustLayer, TerrainLayer rockLayer, TerrainLayer shadowLayer)
    {
        Terrain terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("No active Terrain found in the current scene. Moon terrain layers were created but not assigned.");
            return;
        }

        if (terrain.terrainData == null)
        {
            Debug.LogWarning($"Terrain '{terrain.name}' has no TerrainData. Moon terrain layers were created but not assigned.");
            return;
        }

        terrain.terrainData.terrainLayers = new[] { dustLayer, rockLayer, shadowLayer };
        EditorUtility.SetDirty(terrain.terrainData);
        EditorUtility.SetDirty(terrain);
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        Debug.Log($"Assigned moon terrain layers to Terrain '{terrain.name}'.");
    }

    private static void LogSelection(string layerName, TextureCandidate albedo, TextureCandidate normal)
    {
        Debug.Log($"{layerName} diffuse: {PathOrNone(albedo)} | normal: {PathOrNone(normal)}");
    }

    private static string PathOrNone(TextureCandidate candidate)
    {
        return candidate == null ? "<none>" : candidate.Path;
    }

    private enum LayerRole
    {
        Dust,
        Rock,
        Shadow
    }

    private enum MapRole
    {
        Unknown,
        Albedo,
        Normal,
        Linear
    }

    private sealed class TextureCandidate
    {
        public string Path { get; private set; }
        public string PathLower { get; private set; }
        public string Directory { get; private set; }
        public string FileNameLower { get; private set; }
        public MapRole MapRole { get; private set; }

        public static TextureCandidate FromPath(string path)
        {
            string fileNameLower = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            return new TextureCandidate
            {
                Path = path,
                PathLower = path.ToLowerInvariant().Replace("\\", "/"),
                Directory = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/") ?? string.Empty,
                FileNameLower = fileNameLower,
                MapRole = DetermineMapRole(fileNameLower)
            };
        }

        private static MapRole DetermineMapRole(string fileNameLower)
        {
            if (NormalTerms.Any(fileNameLower.Contains))
            {
                return MapRole.Normal;
            }

            if (HeightTerms.Any(fileNameLower.Contains) || OptionalLinearTerms.Any(fileNameLower.Contains))
            {
                return MapRole.Linear;
            }

            if (AlbedoTerms.Any(fileNameLower.Contains))
            {
                return MapRole.Albedo;
            }

            return MapRole.Unknown;
        }
    }
}
