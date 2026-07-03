using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class MountainBackdropController : MonoBehaviour
{
    public Vector3 backdropPosition = new Vector3(0f, -2f, 35f);
    public Vector3 backdropRotation = Vector3.zero;
    public Vector3 terrainSize = new Vector3(90f, 25f, 45f);
    public bool colliderEnabled = false;

    public const string BackdropName = "Moon_Mountain_Backdrop";
    public const string ParentName = "Environment_Static";

#if UNITY_EDITOR
    private const int HeightmapResolution = 257;
    private const int AlphamapResolution = 256;
    private const int RandomSeed = 5102026;
    private const string TerrainDataPath = "Assets/_Environment/MoonMountainBackdropTerrainData.asset";
    private const string MoonMaterialFolder = "Assets/_Materials/Moon";
    private const string DustLayerPath = MoonMaterialFolder + "/TL_Moon_Dust.terrainlayer";
    private const string RockLayerPath = MoonMaterialFolder + "/TL_Moon_Rock.terrainlayer";
    private const string ShadowLayerPath = MoonMaterialFolder + "/TL_Moon_Shadow.terrainlayer";
    private const string FallbackDustLayerPath = MoonMaterialFolder + "/TL_Moon_Dust_Fallback.terrainlayer";
    private const string FallbackRockLayerPath = MoonMaterialFolder + "/TL_Moon_Rock_Fallback.terrainlayer";
    private const string FallbackShadowLayerPath = MoonMaterialFolder + "/TL_Moon_Shadow_Fallback.terrainlayer";

    private struct Crater
    {
        public Vector2 Center;
        public float Radius;
        public float Depth;
        public float RimStrength;
    }

    [ContextMenu("Apply Transform Settings")]
    public void ApplyTransformSettings()
    {
        ClampSettings();
        transform.position = backdropPosition;
        transform.eulerAngles = backdropRotation;

        Terrain terrain = GetComponent<Terrain>();
        if (terrain != null && terrain.terrainData != null)
        {
            terrain.terrainData.size = terrainSize;
            EditorUtility.SetDirty(terrain.terrainData);
        }

        ApplyColliderState();
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"Mountain backdrop transform applied. Position: {backdropPosition}, Rotation: {backdropRotation}, Size: {terrainSize}");
    }

    [ContextMenu("Regenerate Mountain Terrain")]
    public void RegenerateMountainTerrain()
    {
        Regenerate(this);
    }

    [ContextMenu("Disable Terrain Collider")]
    public void DisableTerrainCollider()
    {
        colliderEnabled = false;
        ApplyColliderState();
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("Mountain backdrop terrain collider disabled.");
    }

    public static GameObject CreateOrUpdateBackdrop()
    {
        GameObject parent = GameObject.Find(ParentName);
        if (parent == null)
        {
            parent = new GameObject(ParentName);
            parent.isStatic = true;
            Undo.RegisterCreatedObjectUndo(parent, "Create Environment Static Parent");
        }

        GameObject backdrop = GameObject.Find(BackdropName);
        bool existed = backdrop != null;

        if (backdrop == null)
        {
            TerrainData terrainData = GetOrCreateTerrainDataAsset();
            backdrop = Terrain.CreateTerrainGameObject(terrainData);
            backdrop.name = BackdropName;
            Undo.RegisterCreatedObjectUndo(backdrop, "Create Mountain Backdrop Terrain");
        }
        else
        {
            Debug.LogWarning($"{BackdropName} already exists. Updating the existing mountain backdrop instead of creating a duplicate.");
        }

        backdrop.transform.SetParent(parent.transform, true);
        backdrop.isStatic = true;

        MountainBackdropController controller = backdrop.GetComponent<MountainBackdropController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<MountainBackdropController>(backdrop);
        }

        Terrain terrain = backdrop.GetComponent<Terrain>();
        if (terrain == null)
        {
            terrain = backdrop.AddComponent<Terrain>();
        }

        TerrainData data = GetOrCreateTerrainDataAsset();
        terrain.terrainData = data;

        TerrainCollider terrainCollider = backdrop.GetComponent<TerrainCollider>();
        if (terrainCollider == null)
        {
            terrainCollider = backdrop.AddComponent<TerrainCollider>();
        }

        terrainCollider.terrainData = data;

        controller.ClampSettings();
        controller.ApplyTransformSettings();
        Regenerate(controller);

        Selection.activeGameObject = backdrop;
        EditorSceneManager.MarkSceneDirty(backdrop.scene);

        Debug.Log($"{(existed ? "Updated" : "Created")} {BackdropName}. Size: {controller.terrainSize}, Position: {controller.backdropPosition}");
        return backdrop;
    }

    private static void Regenerate(MountainBackdropController controller)
    {
        if (controller == null)
        {
            Debug.LogError("Mountain backdrop regeneration failed: missing MountainBackdropController.");
            return;
        }

        Terrain terrain = controller.GetComponent<Terrain>();
        if (terrain == null)
        {
            terrain = controller.gameObject.AddComponent<Terrain>();
        }

        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(terrainData)))
        {
            terrainData = GetOrCreateTerrainDataAsset();
            terrain.terrainData = terrainData;
        }

        Undo.RegisterCompleteObjectUndo(terrainData, "Regenerate Mountain Backdrop Terrain");
        Undo.RecordObject(controller.transform, "Position Mountain Backdrop Terrain");

        controller.ClampSettings();
        terrainData.heightmapResolution = HeightmapResolution;
        terrainData.alphamapResolution = AlphamapResolution;
        terrainData.size = controller.terrainSize;

        Crater[] craters = GenerateCraters();
        float[,] heights = BuildHeights(controller.terrainSize, craters);
        terrainData.SetHeights(0, 0, heights);
        terrainData.terrainLayers = GetTerrainLayers();
        PaintTerrainLayers(terrainData, heights);
        terrainData.SyncHeightmap();

        TerrainCollider terrainCollider = controller.GetComponent<TerrainCollider>();
        if (terrainCollider == null)
        {
            terrainCollider = controller.gameObject.AddComponent<TerrainCollider>();
        }

        terrainCollider.terrainData = terrainData;
        controller.ApplyTransformSettings();

        EditorUtility.SetDirty(terrainData);
        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);

        Debug.Log($"Regenerated {BackdropName}. Size: {terrainData.size}, Position: {controller.transform.position}");
    }

    private static float[,] BuildHeights(Vector3 size, Crater[] craters)
    {
        float[,] heights = new float[HeightmapResolution, HeightmapResolution];
        float safeHeight = Mathf.Max(1f, size.y);

        for (int z = 0; z < HeightmapResolution; z++)
        {
            float v = (float)z / (HeightmapResolution - 1);
            float localZ = v * size.z;

            for (int x = 0; x < HeightmapResolution; x++)
            {
                float u = (float)x / (HeightmapResolution - 1);
                float localX = (u - 0.5f) * size.x;

                float worldHeight = BuildMountainHeight(localX, localZ, u, v, size, craters);
                heights[z, x] = Mathf.Clamp01(worldHeight / safeHeight);
            }
        }

        return heights;
    }

    private static float BuildMountainHeight(float localX, float localZ, float u, float v, Vector3 size, Crater[] craters)
    {
        float frontLow = 1f - Smooth01(Mathf.InverseLerp(0.18f, 0.24f, v));
        float middleRise = Smooth01(Mathf.InverseLerp(0.2f, 0.62f, v));
        float ridgeRise = Smooth01(Mathf.InverseLerp(0.58f, 1f, v));
        float backMask = Smooth01(Mathf.InverseLerp(0.6f, 1f, v));

        float ridgeLine = 0.7f
            + RidgedNoise(localX * 0.11f, 0.3f, 2.6f, 11.7f) * 0.18f
            + SignedPerlin(localX * 0.08f, 3.4f, 91.3f) * 0.12f;
        float ridgeBand = 1f - Mathf.Clamp01(Mathf.Abs(v - ridgeLine) / 0.36f);
        ridgeBand = Mathf.Pow(ridgeBand, 0.72f);

        float height = 0.25f;
        height += middleRise * 4.2f;
        height += ridgeRise * 8.8f;
        height += ridgeBand * ridgeRise * 8.2f;

        float roughMask = Mathf.Clamp01(0.18f + middleRise * 0.45f + backMask * 0.85f);
        float broad = FractalNoise(localX, localZ, 4, 0.075f, 29.1f) * 2.4f;
        float ridged = RidgedNoise(localX, localZ, 0.18f, 117.4f) * 5.8f;
        float fractured = RidgedNoise(localX, localZ, 0.42f, 271.9f) * 2.4f;
        float chipped = SignedPerlin(localX, localZ, 0.95f, 410.3f) * 1.15f;

        height += (broad + ridged + fractured + chipped) * roughMask;

        float jaggedSilhouette = Mathf.Max(0f, SignedPerlin(localX, 1.75f, 713.5f)) * backMask * 5.4f;
        jaggedSilhouette += Mathf.Max(0f, RidgedNoise(localX, 0.85f, 4.7f, 608.2f)) * backMask * 3.6f;
        height += jaggedSilhouette;

        for (int i = 0; i < craters.Length; i++)
        {
            height = ApplyCrater(height, new Vector2(localX, localZ), craters[i], middleRise);
        }

        float sideShard = Mathf.Pow(Mathf.Abs(u - 0.5f) * 2f, 1.7f) * ridgeRise;
        height += sideShard * 2.5f * (0.45f + Mathf.Max(0f, SignedPerlin(localZ, 1.2f, 355.2f)));

        height = Mathf.Lerp(height, 0.18f + Mathf.Max(0f, broad) * 0.2f, frontLow);
        return Mathf.Clamp(height, 0.04f, size.y * 0.96f);
    }

    private static float ApplyCrater(float height, Vector2 point, Crater crater, float terrainMask)
    {
        float distance = Vector2.Distance(point, crater.Center);
        if (distance > crater.Radius * 1.35f)
        {
            return height;
        }

        float inner = distance / crater.Radius;
        if (inner < 1f)
        {
            float depression = crater.Depth * (1f - Smooth01(inner));
            height -= depression * terrainMask;
        }
        else
        {
            float rimT = Mathf.InverseLerp(crater.Radius, crater.Radius * 1.35f, distance);
            float rim = Mathf.Sin((1f - rimT) * Mathf.PI) * crater.Depth * crater.RimStrength;
            height += rim * terrainMask;
        }

        return height;
    }

    private static Crater[] GenerateCraters()
    {
        System.Random random = new System.Random(RandomSeed);
        Crater[] craters = new Crater[22];

        for (int i = 0; i < craters.Length; i++)
        {
            float z = Range(random, 10f, 43f);
            float xLimit = Mathf.Lerp(18f, 44f, z / 45f);

            craters[i] = new Crater
            {
                Center = new Vector2(Range(random, -xLimit, xLimit), z),
                Radius = Range(random, 1.6f, i % 4 == 0 ? 5.4f : 3.7f),
                Depth = Range(random, 0.45f, i % 5 == 0 ? 2.1f : 1.3f),
                RimStrength = Range(random, 0.12f, 0.34f)
            };
        }

        return craters;
    }

    private static void PaintTerrainLayers(TerrainData terrainData, float[,] heights)
    {
        TerrainLayer[] layers = terrainData.terrainLayers;
        if (layers == null || layers.Length == 0)
        {
            return;
        }

        float[,,] alphamaps = new float[AlphamapResolution, AlphamapResolution, layers.Length];

        for (int z = 0; z < AlphamapResolution; z++)
        {
            float v = (float)z / (AlphamapResolution - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(v * (HeightmapResolution - 1)), 0, HeightmapResolution - 1);

            for (int x = 0; x < AlphamapResolution; x++)
            {
                float u = (float)x / (AlphamapResolution - 1);
                int hx = Mathf.Clamp(Mathf.RoundToInt(u * (HeightmapResolution - 1)), 0, HeightmapResolution - 1);
                float height = heights[hz, hx];
                float slope = EstimateSlope(heights, hx, hz);
                float back = Smooth01(Mathf.InverseLerp(0.35f, 1f, v));

                float dust = Mathf.Clamp01(0.35f * (1f - back) + 0.16f * (1f - slope));
                float rock = Mathf.Clamp01(0.58f + back * 0.2f + slope * 0.14f);
                float shadow = Mathf.Clamp01(0.18f + back * 0.28f + slope * 0.42f + height * 0.12f);

                NormalizeAndAssign(alphamaps, z, x, dust, rock, shadow, layers.Length);
            }
        }

        terrainData.SetAlphamaps(0, 0, alphamaps);
    }

    private static void NormalizeAndAssign(float[,,] alphamaps, int z, int x, float dust, float rock, float shadow, int layerCount)
    {
        float total = dust + rock + shadow;
        if (total <= 0.0001f)
        {
            rock = 1f;
            total = 1f;
        }

        float[] weights = { dust / total, rock / total, shadow / total };
        for (int i = 0; i < layerCount; i++)
        {
            alphamaps[z, x, i] = weights[Mathf.Min(i, weights.Length - 1)];
        }
    }

    private static float EstimateSlope(float[,] heights, int x, int z)
    {
        int max = HeightmapResolution - 1;
        int left = Mathf.Max(0, x - 1);
        int right = Mathf.Min(max, x + 1);
        int down = Mathf.Max(0, z - 1);
        int up = Mathf.Min(max, z + 1);
        float dx = Mathf.Abs(heights[z, right] - heights[z, left]);
        float dz = Mathf.Abs(heights[up, x] - heights[down, x]);
        return Mathf.Clamp01((dx + dz) * 42f);
    }

    private static TerrainLayer[] GetTerrainLayers()
    {
        EnsureFolder(MoonMaterialFolder);

        TerrainLayer dust = LoadOrCreateLayer(DustLayerPath, FallbackDustLayerPath, "TL_Moon_Dust_Fallback", new Color32(0x77, 0x77, 0x73, 0xff), 0.02f);
        TerrainLayer rock = LoadOrCreateLayer(RockLayerPath, FallbackRockLayerPath, "TL_Moon_Rock_Fallback", new Color32(0x58, 0x58, 0x56, 0xff), 0.01f);
        TerrainLayer shadow = LoadOrCreateLayer(ShadowLayerPath, FallbackShadowLayerPath, "TL_Moon_Shadow_Fallback", new Color32(0x32, 0x33, 0x35, 0xff), 0f);

        return new[] { dust, rock, shadow };
    }

    private static TerrainLayer LoadOrCreateLayer(string preferredPath, string fallbackPath, string layerName, Color color, float smoothness)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(preferredPath);
        if (layer != null)
        {
            return layer;
        }

        layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(fallbackPath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, fallbackPath);
        }

        Texture2D texture = CreateOrUpdateFallbackTexture(fallbackPath, color);
        layer.name = layerName;
        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(5f, 5f);
        layer.metallic = 0f;
        layer.smoothness = Mathf.Clamp(smoothness, 0f, 0.02f);
        layer.specular = Color.black;
        layer.normalScale = 0f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static Texture2D CreateOrUpdateFallbackTexture(string layerPath, Color color)
    {
        string texturePath = Path.ChangeExtension(layerPath, ".asset");
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            texture = new Texture2D(16, 16, TextureFormat.RGBA32, true);
            AssetDatabase.CreateAsset(texture, texturePath);
        }

        Color[] pixels = new Color[16 * 16];
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.41f + color.r * 9.7f, y * 0.41f + color.g * 13.3f);
                pixels[y * 16 + x] = color * Mathf.Lerp(0.86f, 1.12f, noise);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static TerrainData GetOrCreateTerrainDataAsset()
    {
        EnsureFolder(Path.GetDirectoryName(TerrainDataPath)?.Replace("\\", "/"));

        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        if (terrainData == null)
        {
            terrainData = new TerrainData();
            AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
        }

        return terrainData;
    }

    private void ApplyColliderState()
    {
        Terrain terrain = GetComponent<Terrain>();
        TerrainCollider terrainCollider = GetComponent<TerrainCollider>();
        if (terrainCollider == null)
        {
            return;
        }

        terrainCollider.enabled = colliderEnabled;
        if (terrain != null)
        {
            terrainCollider.terrainData = terrain.terrainData;
        }

        EditorUtility.SetDirty(terrainCollider);
    }

    private void ClampSettings()
    {
        terrainSize = new Vector3(
            Mathf.Max(1f, terrainSize.x),
            Mathf.Max(1f, terrainSize.y),
            Mathf.Max(1f, terrainSize.z));
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
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

    private static float FractalNoise(float x, float z, int octaves, float frequency, float offset)
    {
        float value = 0f;
        float amplitude = 1f;
        float totalAmplitude = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += SignedPerlin(x, z, frequency, offset + i * 31.7f) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= 0.52f;
            frequency *= 2.05f;
        }

        return totalAmplitude > 0f ? value / totalAmplitude : 0f;
    }

    private static float RidgedNoise(float x, float z, float frequency, float offset)
    {
        float value = SignedPerlin(x, z, frequency, offset);
        float ridge = 1f - Mathf.Abs(value);
        return ridge * ridge * 2f - 0.65f;
    }

    private static float SignedPerlin(float x, float z, float frequency, float offset)
    {
        return Mathf.PerlinNoise(x * frequency + offset, z * frequency + offset * 0.73f) * 2f - 1f;
    }

    private static float SignedPerlin(float x, float frequency, float offset)
    {
        return Mathf.PerlinNoise(x * frequency + offset, offset * 0.37f) * 2f - 1f;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float Range(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
#else
    [ContextMenu("Apply Transform Settings")]
    public void ApplyTransformSettings()
    {
        transform.position = backdropPosition;
        transform.eulerAngles = backdropRotation;
    }

    [ContextMenu("Regenerate Mountain Terrain")]
    public void RegenerateMountainTerrain()
    {
        Debug.LogWarning("Mountain backdrop terrain regeneration is editor-only.");
    }

    [ContextMenu("Disable Terrain Collider")]
    public void DisableTerrainCollider()
    {
        colliderEnabled = false;
        TerrainCollider terrainCollider = GetComponent<TerrainCollider>();
        if (terrainCollider != null)
        {
            terrainCollider.enabled = false;
        }
    }
#endif
}
