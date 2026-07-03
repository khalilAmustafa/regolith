using System.IO;
using UnityEditor;
using UnityEngine;

public class ApplyPngHeightmapToTerrainEditor : EditorWindow
{
    private const string TerrainLayerFolder = "Assets/_Materials";
    private const string TerrainLayerPath = TerrainLayerFolder + "/TL_Moon_Regolith_Grey.terrainlayer";
    private const string TerrainLayerTexturePath = TerrainLayerFolder + "/T_Moon_Regolith_Grey.asset";

    private Terrain targetTerrain;
    private Texture2D heightmapTexture;
    private float terrainWidth = 60f;
    private float terrainLength = 60f;
    private float terrainHeight = 18f;
    private bool invertHeight;
    private bool smoothAfterApply = true;
    private int smoothIterations = 1;

    [MenuItem("Tools/Regolith/Apply PNG Heightmap To Terrain")]
    public static void Open()
    {
        GetWindow<ApplyPngHeightmapToTerrainEditor>("PNG Heightmap");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Apply PNG Heightmap To Terrain", EditorStyles.boldLabel);
        EditorGUILayout.Space(6f);

        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", targetTerrain, typeof(Terrain), true);
        heightmapTexture = (Texture2D)EditorGUILayout.ObjectField("Heightmap Texture", heightmapTexture, typeof(Texture2D), false);

        EditorGUILayout.Space(6f);
        terrainWidth = EditorGUILayout.FloatField("Terrain Width", terrainWidth);
        terrainLength = EditorGUILayout.FloatField("Terrain Length", terrainLength);
        terrainHeight = EditorGUILayout.FloatField("Terrain Height", terrainHeight);

        EditorGUILayout.Space(6f);
        invertHeight = EditorGUILayout.Toggle("Invert Height", invertHeight);
        smoothAfterApply = EditorGUILayout.Toggle("Smooth After Apply", smoothAfterApply);

        using (new EditorGUI.DisabledScope(!smoothAfterApply))
        {
            smoothIterations = EditorGUILayout.IntSlider("Smooth Iterations", smoothIterations, 1, 5);
        }

        EditorGUILayout.Space(10f);

        if (GUILayout.Button("Apply Heightmap", GUILayout.Height(32f)))
        {
            ApplyHeightmap();
        }
    }

    private void ApplyHeightmap()
    {
        if (targetTerrain == null)
        {
            Debug.LogError("Apply PNG Heightmap: assign a target Terrain.");
            return;
        }

        if (heightmapTexture == null)
        {
            Debug.LogError("Apply PNG Heightmap: assign a Texture2D heightmap.");
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        if (terrainData == null)
        {
            terrainData = new TerrainData();
            targetTerrain.terrainData = terrainData;
        }

        terrainWidth = Mathf.Max(1f, terrainWidth);
        terrainLength = Mathf.Max(1f, terrainLength);
        terrainHeight = Mathf.Max(0.1f, terrainHeight);
        smoothIterations = Mathf.Max(1, smoothIterations);

        EnsureTextureReadable(heightmapTexture);
        heightmapTexture = ReloadTextureIfAsset(heightmapTexture);

        Undo.RegisterCompleteObjectUndo(terrainData, "Apply PNG Heightmap To Terrain");
        Undo.RecordObject(targetTerrain, "Apply PNG Heightmap To Terrain");

        terrainData.size = new Vector3(terrainWidth, terrainHeight, terrainLength);

        int resolution = terrainData.heightmapResolution;
        float[,] heights = new float[resolution, resolution];

        for (int z = 0; z < resolution; z++)
        {
            float v = resolution <= 1 ? 0f : (float)z / (resolution - 1);

            for (int x = 0; x < resolution; x++)
            {
                float u = resolution <= 1 ? 0f : (float)x / (resolution - 1);
                float value = heightmapTexture.GetPixelBilinear(u, v).grayscale;

                if (invertHeight)
                {
                    value = 1f - value;
                }

                heights[z, x] = Mathf.Clamp01(value);
            }
        }

        if (smoothAfterApply)
        {
            heights = SmoothHeights(heights, smoothIterations);
        }

        terrainData.SetHeights(0, 0, heights);
        AssignMoonRegolithLayer(terrainData);

        TerrainCollider terrainCollider = targetTerrain.GetComponent<TerrainCollider>();
        if (terrainCollider != null)
        {
            terrainCollider.terrainData = terrainData;
        }

        EditorUtility.SetDirty(terrainData);
        EditorUtility.SetDirty(targetTerrain);
        AssetDatabase.SaveAssets();

        Debug.Log($"Applied PNG heightmap '{heightmapTexture.name}' to terrain '{targetTerrain.name}' at {resolution}x{resolution}.");
    }

    private static void EnsureTextureReadable(Texture2D texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;

        if (!importer.isReadable)
        {
            importer.isReadable = true;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
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
        }
    }

    private static Texture2D ReloadTextureIfAsset(Texture2D texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path))
        {
            return texture;
        }

        Texture2D reloaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return reloaded != null ? reloaded : texture;
    }

    private static float[,] SmoothHeights(float[,] source, int iterations)
    {
        int height = source.GetLength(0);
        int width = source.GetLength(1);
        float[,] current = source;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            float[,] next = new float[height, width];

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x == 0 || z == 0 || x == width - 1 || z == height - 1)
                    {
                        next[z, x] = current[z, x];
                        continue;
                    }

                    float center = current[z, x] * 4f;
                    float neighbours = current[z, x - 1]
                        + current[z, x + 1]
                        + current[z - 1, x]
                        + current[z + 1, x];

                    next[z, x] = (center + neighbours) / 8f;
                }
            }

            current = next;
        }

        return current;
    }

    private static void AssignMoonRegolithLayer(TerrainData terrainData)
    {
        TerrainLayer layer = CreateOrUpdateMoonRegolithLayer();
        terrainData.terrainLayers = new[] { layer };
    }

    private static TerrainLayer CreateOrUpdateMoonRegolithLayer()
    {
        EnsureFolder(TerrainLayerFolder);

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainLayerTexturePath);
        if (texture == null)
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            AssetDatabase.CreateAsset(texture, TerrainLayerTexturePath);
        }

        Color grey = new Color32(0x6e, 0x70, 0x72, 0xff);
        texture.SetPixels(new[] { grey * 0.95f, grey, grey * 1.05f, grey * 0.98f });
        texture.Apply(true, false);
        EditorUtility.SetDirty(texture);

        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(TerrainLayerPath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, TerrainLayerPath);
        }

        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(6f, 6f);
        layer.metallic = 0f;
        layer.smoothness = 0.02f;
        layer.normalScale = 0f;

        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
        string name = Path.GetFileName(folder);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(string.IsNullOrEmpty(parent) ? "Assets" : parent, name);
    }
}
