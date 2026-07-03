using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class LunarBasinTerrainGenerator : MonoBehaviour
{
    public int heightmapResolution = 257;
    public float terrainSize = 60f;
    public float maxMountainHeight = 12f;
    public float basinRadius = 18f;
    public float ridgeStartRadius = 23f;
    public float noiseScale = 12f;
    public float basinNoiseStrength = 0.35f;
    public float ridgeNoiseStrength = 3f;
    public int craterCount = 18;
    public float craterRadiusMin = 1.2f;
    public float craterRadiusMax = 4.5f;
    public float craterDepthMin = 0.25f;
    public float craterDepthMax = 1.2f;
    public Vector2 vaultDirection = new Vector2(0f, 1f);
    public float vaultPlatformWidth = 12f;
    public float vaultPlatformDepth = 8f;
    public float vaultPlatformHeight = 2f;
    public GameObject[] rockPrefabs;
    public int rockCount = 40;

    private const string TerrainName = "Lunar Basin Terrain";
    private const string RockParentName = "Generated_Rocks";
    private const int RandomSeed = 24681357;

    private struct Crater
    {
        public Vector2 center;
        public float radius;
        public float depth;
        public float rimStrength;
    }

    [ContextMenu("Generate Terrain")]
    public void GenerateTerrain()
    {
        ValidateSettings();

        Terrain terrain = GetOrCreateTerrain();
        TerrainData terrainData = terrain.terrainData;

#if UNITY_EDITOR
        Undo.RegisterCompleteObjectUndo(terrainData, "Generate Lunar Basin Terrain");
        Undo.RecordObject(terrain.transform, "Position Lunar Basin Terrain");
#endif

        int resolution = GetValidHeightmapResolution(heightmapResolution);
        heightmapResolution = resolution;

        float terrainHeight = Mathf.Max(maxMountainHeight + 2f, vaultPlatformHeight + 4f, 8f);
        terrainData.heightmapResolution = resolution;
        terrainData.size = new Vector3(terrainSize, terrainHeight, terrainSize);

        Vector3 center = GetTerrainCenter(terrain);
        terrain.transform.position = new Vector3(center.x - terrainSize * 0.5f, center.y, center.z - terrainSize * 0.5f);
        terrain.name = TerrainName;

        Vector2 vaultDir = GetVaultDirection();
        Vector2 vaultCenter = GetVaultPlatformCenter(vaultDir);
        List<Crater> craters = GenerateCraters(vaultCenter, vaultDir);
        float[,] heights = new float[resolution, resolution];

        for (int z = 0; z < resolution; z++)
        {
            float normalizedZ = (float)z / (resolution - 1);
            float localZ = normalizedZ * terrainSize - terrainSize * 0.5f;

            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = (float)x / (resolution - 1);
                float localX = normalizedX * terrainSize - terrainSize * 0.5f;
                Vector2 point = new Vector2(localX, localZ);

                float worldHeight = BuildHeight(point, craters, vaultDir, vaultCenter);
                heights[z, x] = Mathf.Clamp01(worldHeight / terrainHeight);
            }
        }

        terrainData.SetHeights(0, 0, heights);
        terrainData.SyncHeightmap();
        terrain.materialTemplate = CreateOrUpdateLunarMaterial();
        ApplyLunarTerrainLayer(terrainData);

        TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (terrainCollider != null)
        {
            terrainCollider.terrainData = terrainData;
        }

        ClearGeneratedRocks();
        ScatterRocks(terrain, vaultDir, vaultCenter);

#if UNITY_EDITOR
        EditorUtility.SetDirty(terrainData);
        EditorUtility.SetDirty(terrain);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    [ContextMenu("Clear Generated Rocks")]
    public void ClearGeneratedRocks()
    {
        Transform existing = transform.Find(RockParentName);
        if (existing == null)
        {
            GameObject existingObject = GameObject.Find(RockParentName);
            existing = existingObject != null ? existingObject.transform : null;
        }

        if (existing == null)
        {
            return;
        }

        DestroyObject(existing.gameObject);
    }

    private float BuildHeight(Vector2 point, List<Crater> craters, Vector2 vaultDir, Vector2 vaultCenter)
    {
        float radiusFromCenter = point.magnitude;
        float edgeRadius = terrainSize * 0.5f;

        float shoulderT = Smooth01(Mathf.InverseLerp(basinRadius, ridgeStartRadius, radiusFromCenter));
        float ridgeT = Smooth01(Mathf.InverseLerp(ridgeStartRadius, edgeRadius, radiusFromCenter));

        float height = 0.55f;
        height += shoulderT * 0.9f;
        height += ridgeT * (maxMountainHeight - 1.45f);

        float pathMask = GetPathMask(point, vaultCenter);
        float basinNoiseMask = 1f - Smooth01(Mathf.InverseLerp(basinRadius - 2f, ridgeStartRadius, radiusFromCenter));
        float ridgeNoiseMask = Smooth01(Mathf.InverseLerp(ridgeStartRadius - 1f, edgeRadius, radiusFromCenter));

        float basinNoise = SignedPerlin(point, 1f, 37.2f) * basinNoiseStrength * basinNoiseMask;
        basinNoise += SignedPerlin(point, 4.2f, 58.4f) * basinNoiseStrength * 0.22f * basinNoiseMask;

        float ridgeNoise = BuildJaggedRidgeNoise(point) * ridgeNoiseStrength * 1.2f * ridgeNoiseMask;

        height += basinNoise * (1f - pathMask * 0.85f);
        height += ridgeNoise * (1f - pathMask * 0.65f);

        float pathProgress = Mathf.Clamp01(Vector2.Dot(point, vaultDir) / Mathf.Max(vaultCenter.magnitude, 0.01f));
        float smoothPathHeight = Mathf.Lerp(0.6f, vaultPlatformHeight, Smooth01(pathProgress));
        height = Mathf.Lerp(height, smoothPathHeight, pathMask * 0.72f);

        for (int i = 0; i < craters.Count; i++)
        {
            height = ApplyCrater(height, point, craters[i], pathMask);
        }

        height = Mathf.Lerp(height, smoothPathHeight, pathMask * 0.28f);
        height = ApplyVaultPlatform(height, point, vaultDir, vaultCenter);

        return Mathf.Clamp(height, 0.05f, maxMountainHeight + 1f);
    }

    private float ApplyCrater(float height, Vector2 point, Crater crater, float pathMask)
    {
        float distance = Vector2.Distance(point, crater.center);
        float craterAvoidance = 1f - pathMask * 0.9f;

        if (distance < crater.radius)
        {
            float craterT = distance / crater.radius;
            float depression = crater.depth * (1f - Smooth01(craterT));
            return height - depression * craterAvoidance;
        }

        float rimOuterRadius = crater.radius * 1.22f;
        if (distance < rimOuterRadius)
        {
            float rimT = Mathf.InverseLerp(crater.radius, rimOuterRadius, distance);
            float rim = Mathf.Sin((1f - rimT) * Mathf.PI) * crater.depth * crater.rimStrength;
            return height + rim * (1f - pathMask * 0.7f);
        }

        return height;
    }

    private float ApplyVaultPlatform(float height, Vector2 point, Vector2 vaultDir, Vector2 vaultCenter)
    {
        Vector2 sideDir = new Vector2(-vaultDir.y, vaultDir.x);
        Vector2 offset = point - vaultCenter;

        float side = Vector2.Dot(offset, sideDir);
        float forward = Vector2.Dot(offset, vaultDir);
        float halfWidth = Mathf.Max(0.5f, vaultPlatformWidth * 0.5f);
        float halfDepth = Mathf.Max(0.5f, vaultPlatformDepth * 0.5f);

        float superEllipse = Mathf.Pow(Mathf.Abs(side) / halfWidth, 4f) + Mathf.Pow(Mathf.Abs(forward) / halfDepth, 4f);
        float platformMask = 1f - Smooth01(Mathf.InverseLerp(0.78f, 1.32f, superEllipse));

        return Mathf.Lerp(height, vaultPlatformHeight, platformMask);
    }

    private List<Crater> GenerateCraters(Vector2 vaultCenter, Vector2 vaultDir)
    {
        List<Crater> craters = new List<Crater>(craterCount);
        System.Random random = new System.Random(RandomSeed);

        int attempts = Mathf.Max(craterCount * 16, 32);
        for (int i = 0; i < attempts && craters.Count < craterCount; i++)
        {
            bool mediumCrater = craters.Count >= craterCount * 0.58f;
            float radius = mediumCrater
                ? Range(random, Mathf.Lerp(craterRadiusMin, craterRadiusMax, 0.45f), craterRadiusMax)
                : Range(random, craterRadiusMin, Mathf.Lerp(craterRadiusMin, craterRadiusMax, 0.48f));

            float depth = mediumCrater
                ? Range(random, Mathf.Lerp(craterDepthMin, craterDepthMax, 0.35f), craterDepthMax)
                : Range(random, craterDepthMin, Mathf.Lerp(craterDepthMin, craterDepthMax, 0.52f));

            float usableRadius = terrainSize * 0.5f - radius - 1f;

            float angle = Range(random, 0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt(Range(random, 0f, 1f)) * usableRadius;
            Vector2 center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            float distanceToPath = DistancePointToSegment(center, Vector2.zero, vaultCenter);
            bool deepOnPath = depth > Mathf.Lerp(craterDepthMin, craterDepthMax, 0.38f) && distanceToPath < radius + 2.8f;
            bool pathCraterTooClose = distanceToPath < radius + 1.35f;

            if (deepOnPath || pathCraterTooClose || IsInsideVaultPlatformInfluence(center, vaultDir, vaultCenter, 1.4f))
            {
                continue;
            }

            craters.Add(new Crater
            {
                center = center,
                radius = radius,
                depth = depth,
                rimStrength = mediumCrater ? 0.18f : 0.12f
            });
        }

        return craters;
    }

    private void ScatterRocks(Terrain terrain, Vector2 vaultDir, Vector2 vaultCenter)
    {
        if (rockPrefabs == null || rockPrefabs.Length == 0 || rockCount <= 0)
        {
            return;
        }

        GameObject rockParent = new GameObject(RockParentName);
        rockParent.transform.SetParent(transform, false);

        System.Random random = new System.Random(RandomSeed + 41);
        int placed = 0;
        int attempts = Mathf.Max(rockCount * 12, 80);

        for (int i = 0; i < attempts && placed < rockCount; i++)
        {
            GameObject prefab = rockPrefabs[random.Next(0, rockPrefabs.Length)];
            if (prefab == null)
            {
                continue;
            }

            float angle = Range(random, 0f, Mathf.PI * 2f);
            float radius = Range(random, ridgeStartRadius * 0.82f, terrainSize * 0.5f - 1.5f);
            Vector2 localPoint = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            if (DistancePointToSegment(localPoint, Vector2.zero, vaultCenter) < 4.5f
                || IsInsideVaultPlatformInfluence(localPoint, vaultDir, vaultCenter, 1.35f))
            {
                continue;
            }

            Vector3 world = terrain.transform.position + new Vector3(localPoint.x + terrainSize * 0.5f, 0f, localPoint.y + terrainSize * 0.5f);
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y;

            GameObject rock = InstantiatePrefab(prefab);
            rock.name = $"{prefab.name}_Generated";
            rock.transform.SetParent(rockParent.transform, true);
            rock.transform.position = world;
            rock.transform.rotation = Quaternion.Euler(0f, Range(random, 0f, 360f), 0f);

            float scale = Range(random, 0.65f, 1.45f);
            rock.transform.localScale = Vector3.Scale(prefab.transform.localScale, new Vector3(scale, scale * Range(random, 0.75f, 1.25f), scale));

            placed++;
        }
    }

    private Terrain GetOrCreateTerrain()
    {
        Terrain terrain = GetComponent<Terrain>();
        if (terrain == null)
        {
            terrain = FindAnyObjectByType<Terrain>();
        }

        if (terrain == null)
        {
            TerrainData terrainData = new TerrainData();
            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = TerrainName;
            terrain = terrainObject.GetComponent<Terrain>();
        }

        if (terrain.terrainData == null)
        {
            terrain.terrainData = new TerrainData();
        }

        TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (terrainCollider == null)
        {
            terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
        }

        terrainCollider.terrainData = terrain.terrainData;
        return terrain;
    }

    private Vector3 GetTerrainCenter(Terrain terrain)
    {
        if (terrain.transform != transform)
        {
            return transform.position;
        }

        TerrainData terrainData = terrain.terrainData;
        Vector3 size = terrainData != null ? terrainData.size : new Vector3(terrainSize, 0f, terrainSize);
        return terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
    }

    private Vector2 GetVaultDirection()
    {
        if (vaultDirection.sqrMagnitude < 0.001f)
        {
            return Vector2.up;
        }

        return vaultDirection.normalized;
    }

    private Vector2 GetVaultPlatformCenter(Vector2 vaultDir)
    {
        float edgeRadius = terrainSize * 0.5f;
        float distance = Mathf.Min(edgeRadius - vaultPlatformDepth * 0.65f, ridgeStartRadius + vaultPlatformDepth * 0.45f);
        distance = Mathf.Clamp(distance, basinRadius + 2f, edgeRadius - 1f);
        return vaultDir * distance;
    }

    private float GetPathMask(Vector2 point, Vector2 vaultCenter)
    {
        float distanceToPath = DistancePointToSegment(point, Vector2.zero, vaultCenter);
        return 1f - Smooth01(Mathf.InverseLerp(2.7f, 5.2f, distanceToPath));
    }

    private bool IsInsideVaultPlatformInfluence(Vector2 point, Vector2 vaultDir, Vector2 vaultCenter, float multiplier)
    {
        Vector2 sideDir = new Vector2(-vaultDir.y, vaultDir.x);
        Vector2 offset = point - vaultCenter;
        float side = Mathf.Abs(Vector2.Dot(offset, sideDir));
        float forward = Mathf.Abs(Vector2.Dot(offset, vaultDir));

        return side < vaultPlatformWidth * 0.5f * multiplier
            && forward < vaultPlatformDepth * 0.5f * multiplier;
    }

    private float FractalNoise(Vector2 point, int octaves)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float totalAmplitude = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += SignedPerlin(point, frequency, 102.7f + i * 19.1f) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= 0.52f;
            frequency *= 2.15f;
        }

        return totalAmplitude > 0f ? value / totalAmplitude : 0f;
    }

    private float BuildJaggedRidgeNoise(Vector2 point)
    {
        float broad = FractalNoise(point, 4) * 0.62f;
        float chipped = RidgedNoise(point, 4.5f, 197.3f) * 0.8f;
        float fractured = RidgedNoise(point, 9.5f, 421.6f) * 0.34f;
        float fine = SignedPerlin(point, 13.5f, 311.9f) * 0.22f;

        return broad + chipped + fractured + fine;
    }

    private float RidgedNoise(Vector2 point, float frequency, float offset)
    {
        float value = SignedPerlin(point, frequency, offset);
        float ridge = 1f - Mathf.Abs(value);
        return ridge * ridge * 2f - 0.65f;
    }

    private float SignedPerlin(Vector2 point, float frequency, float offset)
    {
        float scale = Mathf.Max(0.01f, noiseScale);
        float sampleX = point.x / scale * frequency + offset;
        float sampleY = point.y / scale * frequency + offset * 0.73f;
        return Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
    }

    private Material CreateOrUpdateLunarMaterial()
    {
#if UNITY_EDITOR
        const string folder = "Assets/_Materials";
        const string path = "Assets/_Materials/Lunar Basin Terrain.mat";

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "_Materials");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(FindTerrainShader());
            AssetDatabase.CreateAsset(material, path);
        }
#else
        Material material = new Material(FindTerrainShader());
#endif

        SetMaterialColor(material, new Color32(0x72, 0x72, 0x72, 0xff));
        SetMaterialFloat(material, "_Metallic", 0f);
        SetMaterialFloat(material, "_Smoothness", 0.02f);
        material.SetFloat("_Metallic0", 0f);
        material.SetFloat("_Metallic1", 0f);
        material.SetFloat("_Metallic2", 0f);
        material.SetFloat("_Metallic3", 0f);
        material.SetFloat("_Smoothness0", 0.02f);
        material.SetFloat("_Smoothness1", 0.02f);
        material.SetFloat("_Smoothness2", 0.02f);
        material.SetFloat("_Smoothness3", 0.02f);
        SetMaterialFloat(material, "_SpecularHighlights", 0f);
        SetMaterialFloat(material, "_EnvironmentReflections", 0f);
        material.name = "Lunar Basin Terrain";

#if UNITY_EDITOR
        EditorUtility.SetDirty(material);
#endif

        return material;
    }

    private void ApplyLunarTerrainLayer(TerrainData terrainData)
    {
        TerrainLayer layer = CreateOrUpdateLunarTerrainLayer();
        terrainData.terrainLayers = new[] { layer };
    }

    private TerrainLayer CreateOrUpdateLunarTerrainLayer()
    {
#if UNITY_EDITOR
        const string folder = "Assets/_Materials";
        const string layerPath = "Assets/_Materials/Lunar Regolith.terrainlayer";
        const string texturePath = "Assets/_Materials/Lunar Regolith Swatch.asset";

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "_Materials");
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, true);
            AssetDatabase.CreateAsset(texture, texturePath);
        }

        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, layerPath);
        }
#else
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        TerrainLayer layer = new TerrainLayer();
#endif

        if (texture.width != 64 || texture.height != 64)
        {
            texture.Reinitialize(64, 64, TextureFormat.RGBA32, true);
        }

        Color regolith = new Color32(0x72, 0x72, 0x72, 0xff);
        Color[] pixels = new Color[64 * 64];

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float largeDust = Mathf.PerlinNoise(x * 0.12f + 12.7f, y * 0.12f + 44.1f);
                float fineDust = Mathf.PerlinNoise(x * 0.47f + 91.3f, y * 0.47f + 16.6f);
                float shade = Mathf.Lerp(0.82f, 1.13f, largeDust * 0.68f + fineDust * 0.32f);
                pixels[y * 64 + x] = regolith * shade;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);

        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(6f, 6f);
        layer.metallic = 0f;
        layer.smoothness = 0.02f;
        layer.normalScale = 0f;

#if UNITY_EDITOR
        EditorUtility.SetDirty(texture);
        EditorUtility.SetDirty(layer);
#endif

        return layer;
    }

    private Shader FindTerrainShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }

    private void SetMaterialColor(Material material, Color color)
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

    private void SetMaterialFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private GameObject InstantiatePrefab(GameObject prefab)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                Undo.RegisterCreatedObjectUndo(instance, "Scatter Lunar Rock");
                return instance;
            }
        }
#endif

        return Instantiate(prefab);
    }

    private void DestroyObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.DestroyObjectImmediate(target);
            return;
        }
#endif

        Destroy(target);
    }

    private void ValidateSettings()
    {
        terrainSize = Mathf.Max(10f, terrainSize);
        maxMountainHeight = Mathf.Max(1f, maxMountainHeight);
        basinRadius = Mathf.Clamp(basinRadius, 1f, terrainSize * 0.45f);
        ridgeStartRadius = Mathf.Clamp(ridgeStartRadius, basinRadius + 1f, terrainSize * 0.49f);
        noiseScale = Mathf.Max(0.1f, noiseScale);
        basinNoiseStrength = Mathf.Max(0f, basinNoiseStrength);
        ridgeNoiseStrength = Mathf.Max(0f, ridgeNoiseStrength);
        craterCount = Mathf.Max(0, craterCount);
        craterRadiusMin = Mathf.Max(0.2f, craterRadiusMin);
        craterRadiusMax = Mathf.Max(craterRadiusMin, craterRadiusMax);
        craterDepthMin = Mathf.Max(0f, craterDepthMin);
        craterDepthMax = Mathf.Max(craterDepthMin, craterDepthMax);
        vaultPlatformWidth = Mathf.Max(1f, vaultPlatformWidth);
        vaultPlatformDepth = Mathf.Max(1f, vaultPlatformDepth);
        vaultPlatformHeight = Mathf.Clamp(vaultPlatformHeight, 0.1f, maxMountainHeight);
        rockCount = Mathf.Max(0, rockCount);
    }

    private int GetValidHeightmapResolution(int requestedResolution)
    {
        int clamped = Mathf.Clamp(requestedResolution, 33, 4097);
        int power = Mathf.ClosestPowerOfTwo(clamped - 1);
        return Mathf.Clamp(power + 1, 33, 4097);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denominator = Vector2.Dot(ab, ab);
        if (denominator <= Mathf.Epsilon)
        {
            return Vector2.Distance(point, a);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / denominator);
        return Vector2.Distance(point, a + ab * t);
    }

    private static float Range(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
