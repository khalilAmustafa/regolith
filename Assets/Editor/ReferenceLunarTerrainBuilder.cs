using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class ReferenceLunarTerrainBuilder : EditorWindow
{
    private const string TerrainName = "Moon_Hero_Terrain";
    private const string TerrainDataFolder = "Assets/_Environment";
    private const string TerrainDataPath = TerrainDataFolder + "/MoonHeroReferenceTerrainData.asset";
    private const string MoonMaterialFolder = "Assets/_Materials/Moon";
    private const string DustLayerPath = MoonMaterialFolder + "/TL_Moon_Dust.terrainlayer";
    private const string RockLayerPath = MoonMaterialFolder + "/TL_Moon_Rock.terrainlayer";
    private const string ShadowLayerPath = MoonMaterialFolder + "/TL_Moon_Shadow.terrainlayer";
    private const string HeightmapRoot = "Assets/All In One - Heightmaps/Heightmaps";
    private const int HeightmapResolution = 513;
    private const int AlphamapResolution = 512;
    private const int RandomSeed = 642025;

    private float terrainWidth = 90f;
    private float terrainLength = 70f;
    private float terrainHeight = 8f;
    private Vector2 bunkerPitCenter = new Vector2(45f, 38f);
    private float bunkerPitRadiusX = 10f;
    private float bunkerPitRadiusZ = 7f;
    private float bunkerPitDepth = 1.5f;
    private float pathWidth = 6f;
    private float pathDepth = 0.25f;
    private float farRidgeHeight = 2.5f;
    private float noiseStrength = 0.35f;
    private int craterCount = 12;
    private int smoothIterations = 2;
    private Texture2D importedHeightmap;
    private bool useImportedHeightmap = true;

    private Vector2 scrollPosition;

    private struct Crater
    {
        public Vector2 Center;
        public float Radius;
        public float Depth;
        public float RimStrength;
    }

    [MenuItem("Tools/Regolith/Create Reference Lunar Base Terrain")]
    public static void OpenAndCreate()
    {
        ReferenceLunarTerrainBuilder window = GetWindow<ReferenceLunarTerrainBuilder>("Reference Lunar Terrain");
        window.TryAutoSelectHeightmap();
        window.CreateNewReferenceTerrain();
    }

    private void OnEnable()
    {
        TryAutoSelectHeightmap();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.LabelField("Reference Lunar Base Terrain", EditorStyles.boldLabel);
        EditorGUILayout.Space(6f);

        terrainWidth = Mathf.Max(10f, EditorGUILayout.FloatField("Terrain Width", terrainWidth));
        terrainLength = Mathf.Max(10f, EditorGUILayout.FloatField("Terrain Length", terrainLength));
        terrainHeight = Mathf.Max(1f, EditorGUILayout.FloatField("Terrain Height", terrainHeight));
        bunkerPitCenter = EditorGUILayout.Vector2Field("Bunker Pit Center", bunkerPitCenter);
        bunkerPitRadiusX = Mathf.Max(1f, EditorGUILayout.FloatField("Bunker Pit Radius X", bunkerPitRadiusX));
        bunkerPitRadiusZ = Mathf.Max(1f, EditorGUILayout.FloatField("Bunker Pit Radius Z", bunkerPitRadiusZ));
        bunkerPitDepth = Mathf.Max(0f, EditorGUILayout.FloatField("Bunker Pit Depth", bunkerPitDepth));
        pathWidth = Mathf.Max(0.5f, EditorGUILayout.FloatField("Path Width", pathWidth));
        pathDepth = Mathf.Max(0f, EditorGUILayout.FloatField("Path Depth", pathDepth));
        farRidgeHeight = Mathf.Max(0f, EditorGUILayout.FloatField("Far Ridge Height", farRidgeHeight));
        noiseStrength = Mathf.Max(0f, EditorGUILayout.FloatField("Noise Strength", noiseStrength));
        craterCount = Mathf.Max(0, EditorGUILayout.IntField("Crater Count", craterCount));
        smoothIterations = Mathf.Clamp(EditorGUILayout.IntField("Smooth Iterations", smoothIterations), 0, 6);

        EditorGUILayout.Space(8f);
        useImportedHeightmap = EditorGUILayout.Toggle("Use Imported Heightmap", useImportedHeightmap);
        using (new EditorGUILayout.HorizontalScope())
        {
            importedHeightmap = (Texture2D)EditorGUILayout.ObjectField("Low Strength Heightmap", importedHeightmap, typeof(Texture2D), false);
            if (GUILayout.Button("Auto", GUILayout.Width(54f)))
            {
                TryAutoSelectHeightmap();
            }
        }

        EditorGUILayout.Space(12f);
        if (GUILayout.Button("Create New Reference Terrain", GUILayout.Height(32f)))
        {
            CreateNewReferenceTerrain();
        }

        if (GUILayout.Button("Regenerate Current Reference Terrain", GUILayout.Height(32f)))
        {
            RegenerateCurrentReferenceTerrain();
        }

        if (GUILayout.Button("Delete Reference Terrain Only", GUILayout.Height(32f)))
        {
            DeleteReferenceTerrainOnly();
        }

        EditorGUILayout.EndScrollView();
    }

    private void CreateNewReferenceTerrain()
    {
        ValidateSettings();

        GameObject terrainObject = GameObject.Find(TerrainName);
        if (terrainObject != null)
        {
            Debug.LogWarning($"{TerrainName} already exists. Use Regenerate Current Reference Terrain to update it, or Delete Reference Terrain Only before creating a new exact-name reference terrain.");
            Selection.activeGameObject = terrainObject;
            return;
        }

        EnsureFolder(TerrainDataFolder);
        TerrainData terrainData = new TerrainData();
        AssetDatabase.CreateAsset(terrainData, AssetDatabase.GenerateUniqueAssetPath(TerrainDataPath));

        terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        terrainObject.name = TerrainName;
        terrainObject.transform.position = Vector3.zero;
        terrainObject.transform.rotation = Quaternion.identity;
        terrainObject.isStatic = true;
        Undo.RegisterCreatedObjectUndo(terrainObject, "Create Reference Lunar Terrain");

        Terrain terrain = terrainObject.GetComponent<Terrain>();
        RegenerateTerrain(terrain);
        CreateOrUpdateMarkers(terrainObject.transform);
        ApplySimpleLighting();

        Selection.activeGameObject = terrainObject;
        EditorSceneManager.MarkSceneDirty(terrainObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created {TerrainName} at {terrainObject.transform.position} with size {terrainData.size}.");
    }

    private void RegenerateCurrentReferenceTerrain()
    {
        GameObject terrainObject = GameObject.Find(TerrainName);
        if (terrainObject == null)
        {
            Debug.LogWarning($"{TerrainName} was not found. Creating a new reference terrain.");
            CreateNewReferenceTerrain();
            return;
        }

        Terrain terrain = terrainObject.GetComponent<Terrain>();
        if (terrain == null)
        {
            terrain = terrainObject.AddComponent<Terrain>();
        }

        if (terrain.terrainData == null)
        {
            terrain.terrainData = new TerrainData();
            EnsureFolder(TerrainDataFolder);
            AssetDatabase.CreateAsset(terrain.terrainData, AssetDatabase.GenerateUniqueAssetPath(TerrainDataPath));
        }

        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Regenerate Reference Lunar Terrain");
        Undo.RecordObject(terrainObject.transform, "Regenerate Reference Lunar Terrain");
        terrainObject.transform.position = Vector3.zero;
        terrainObject.transform.rotation = Quaternion.identity;
        terrainObject.isStatic = true;

        RegenerateTerrain(terrain);
        CreateOrUpdateMarkers(terrainObject.transform);
        ApplySimpleLighting();

        Selection.activeGameObject = terrainObject;
        EditorSceneManager.MarkSceneDirty(terrainObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Regenerated {TerrainName}.");
    }

    private void DeleteReferenceTerrainOnly()
    {
        GameObject terrainObject = GameObject.Find(TerrainName);
        if (terrainObject == null)
        {
            Debug.LogWarning($"{TerrainName} was not found. Nothing was deleted.");
            return;
        }

        Undo.DestroyObjectImmediate(terrainObject);
        Debug.Log($"Deleted reference terrain object {TerrainName}. Existing non-reference terrain objects were untouched.");
    }

    private void RegenerateTerrain(Terrain terrain)
    {
        ValidateSettings();
        TerrainData terrainData = terrain.terrainData;
        terrainData.heightmapResolution = HeightmapResolution;
        terrainData.alphamapResolution = AlphamapResolution;
        terrainData.size = new Vector3(terrainWidth, terrainHeight, terrainLength);

        EnsureHeightmapReadable();
        Crater[] craters = GenerateCraters();
        float[,] heights = BuildHeightmap(craters);

        for (int i = 0; i < smoothIterations; i++)
        {
            heights = SmoothHeights(heights);
        }

        terrainData.SetHeights(0, 0, heights);
        terrainData.terrainLayers = GetTerrainLayers();
        PaintTerrain(terrainData, heights, craters);
        terrainData.SyncHeightmap();

        TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (terrainCollider == null)
        {
            terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
        }

        terrainCollider.terrainData = terrainData;
        terrain.gameObject.layer = LayerMask.NameToLayer("Ground") >= 0 ? LayerMask.NameToLayer("Ground") : terrain.gameObject.layer;

        EditorUtility.SetDirty(terrainData);
        EditorUtility.SetDirty(terrain);
        Debug.Log($"Reference lunar terrain generated. Mostly flat playable plain, shallow bunker pit, smooth access path, low far ridges, no tire tracks.");
    }

    private float[,] BuildHeightmap(Crater[] craters)
    {
        float[,] heights = new float[HeightmapResolution, HeightmapResolution];

        for (int z = 0; z < HeightmapResolution; z++)
        {
            float v = (float)z / (HeightmapResolution - 1);
            float localZ = v * terrainLength;

            for (int x = 0; x < HeightmapResolution; x++)
            {
                float u = (float)x / (HeightmapResolution - 1);
                float localX = u * terrainWidth;
                Vector2 point = new Vector2(localX, localZ);

                float worldHeight = BuildHeight(point, u, v, craters);
                heights[z, x] = Mathf.Clamp01(worldHeight / terrainHeight);
            }
        }

        return heights;
    }

    private float BuildHeight(Vector2 point, float u, float v, Crater[] craters)
    {
        float height = 0.72f;

        float imported = SampleImportedHeightmap(u, v);
        height += imported * 0.16f;

        float broad = FractalNoise(point, 0.025f, 4, 19.3f) * noiseStrength * 0.55f;
        float fine = FractalNoise(point, 0.12f, 3, 83.1f) * noiseStrength * 0.18f;
        height += broad + fine;

        foreach (Crater crater in craters)
        {
            height = ApplyCrater(height, point, crater);
        }

        height = ApplyBunkerPit(height, point);
        height = ApplyAccessPath(height, point);
        height = ApplyFarRidges(height, point, v);
        height = KeepSpawnAndCenterPlayable(height, point);

        return Mathf.Clamp(height, 0.05f, terrainHeight * 0.86f);
    }

    private float ApplyBunkerPit(float height, Vector2 point)
    {
        Vector2 offset = point - bunkerPitCenter;
        float oval = Mathf.Sqrt(
            Mathf.Pow(offset.x / Mathf.Max(0.01f, bunkerPitRadiusX), 2f)
            + Mathf.Pow(offset.y / Mathf.Max(0.01f, bunkerPitRadiusZ), 2f));

        float pitMask = 1f - Smooth01(Mathf.InverseLerp(0.74f, 1.2f, oval));
        float floorMask = 1f - Smooth01(Mathf.InverseLerp(0.28f, 0.58f, oval));
        float targetPitHeight = 0.72f - bunkerPitDepth;
        height = Mathf.Lerp(height, targetPitHeight, pitMask * 0.82f);

        Vector2 airlockCenter = bunkerPitCenter + new Vector2(0f, bunkerPitRadiusZ * 0.55f);
        Vector2 airlockOffset = point - airlockCenter;
        float platform = SuperEllipse(airlockOffset, 4.5f, 2.7f, 4f);
        float platformMask = 1f - Smooth01(Mathf.InverseLerp(0.78f, 1.18f, platform));
        height = Mathf.Lerp(height, targetPitHeight + 0.05f, Mathf.Max(floorMask * 0.55f, platformMask));

        return height;
    }

    private float ApplyAccessPath(float height, Vector2 point)
    {
        Vector2 start = new Vector2(terrainWidth * 0.5f, terrainLength * 0.13f);
        Vector2 end = bunkerPitCenter - new Vector2(0f, bunkerPitRadiusZ * 0.45f);
        float distanceToPath = DistancePointToSegment(point, start, end);
        float pathMask = 1f - Smooth01(Mathf.InverseLerp(pathWidth * 0.5f, pathWidth * 0.98f, distanceToPath));

        if (pathMask <= 0f)
        {
            return height;
        }

        float progress = PathProgress(point, start, end);
        float pathHeight = Mathf.Lerp(0.62f, 0.72f - pathDepth, Smooth01(progress));
        return Mathf.Lerp(height, pathHeight, pathMask * 0.72f);
    }

    private float ApplyFarRidges(float height, Vector2 point, float v)
    {
        float ridgeMask = Smooth01(Mathf.InverseLerp(0.76f, 1f, v));
        if (ridgeMask <= 0f)
        {
            return height;
        }

        float ridgeNoise = Mathf.Max(0f, FractalNoise(point + new Vector2(200f, -40f), 0.045f, 4, 41.7f));
        float ridge = ridgeMask * farRidgeHeight * (0.35f + ridgeNoise * 0.65f);
        return height + ridge;
    }

    private float KeepSpawnAndCenterPlayable(float height, Vector2 point)
    {
        Vector2 spawn = new Vector2(terrainWidth * 0.5f, terrainLength * 0.12f);
        float spawnMask = 1f - Smooth01(Mathf.InverseLerp(4f, 10f, Vector2.Distance(point, spawn)));
        float centerMask = 1f - Smooth01(Mathf.InverseLerp(0.16f, 0.34f, Mathf.Abs(point.x - terrainWidth * 0.5f) / terrainWidth));
        centerMask *= 1f - Smooth01(Mathf.InverseLerp(0.06f, 0.55f, point.y / terrainLength));
        float playableMask = Mathf.Max(spawnMask, centerMask * 0.35f);

        return Mathf.Lerp(height, 0.64f, playableMask * 0.6f);
    }

    private float ApplyCrater(float height, Vector2 point, Crater crater)
    {
        float distance = Vector2.Distance(point, crater.Center);
        if (distance > crater.Radius * 1.32f)
        {
            return height;
        }

        if (distance < crater.Radius)
        {
            float t = distance / crater.Radius;
            float depression = crater.Depth * (1f - Smooth01(t));
            return height - depression;
        }

        float rimT = Mathf.InverseLerp(crater.Radius, crater.Radius * 1.32f, distance);
        float rim = Mathf.Sin((1f - rimT) * Mathf.PI) * crater.Depth * crater.RimStrength;
        return height + rim;
    }

    private Crater[] GenerateCraters()
    {
        System.Random random = new System.Random(RandomSeed);
        List<Crater> craters = new List<Crater>();
        Vector2 pathStart = new Vector2(terrainWidth * 0.5f, terrainLength * 0.13f);
        Vector2 pathEnd = bunkerPitCenter - new Vector2(0f, bunkerPitRadiusZ * 0.45f);

        int attempts = Mathf.Max(craterCount * 16, 48);
        for (int i = 0; i < attempts && craters.Count < craterCount; i++)
        {
            Vector2 center = new Vector2(Range(random, 7f, terrainWidth - 7f), Range(random, 9f, terrainLength - 9f));
            float radius = Range(random, 1.6f, 4.8f);

            if (Vector2.Distance(center, bunkerPitCenter) < Mathf.Max(bunkerPitRadiusX, bunkerPitRadiusZ) + radius + 2f)
            {
                continue;
            }

            if (DistancePointToSegment(center, pathStart, pathEnd) < pathWidth * 0.8f + radius)
            {
                continue;
            }

            craters.Add(new Crater
            {
                Center = center,
                Radius = radius,
                Depth = Range(random, 0.08f, 0.34f),
                RimStrength = Range(random, 0.08f, 0.22f)
            });
        }

        return craters.ToArray();
    }

    private void PaintTerrain(TerrainData terrainData, float[,] heights, Crater[] craters)
    {
        TerrainLayer[] layers = terrainData.terrainLayers;
        if (layers == null || layers.Length < 3)
        {
            return;
        }

        float[,,] alphamaps = new float[AlphamapResolution, AlphamapResolution, layers.Length];

        for (int z = 0; z < AlphamapResolution; z++)
        {
            float v = (float)z / (AlphamapResolution - 1);
            float localZ = v * terrainLength;
            int hz = Mathf.Clamp(Mathf.RoundToInt(v * (HeightmapResolution - 1)), 0, HeightmapResolution - 1);

            for (int x = 0; x < AlphamapResolution; x++)
            {
                float u = (float)x / (AlphamapResolution - 1);
                float localX = u * terrainWidth;
                int hx = Mathf.Clamp(Mathf.RoundToInt(u * (HeightmapResolution - 1)), 0, HeightmapResolution - 1);
                Vector2 point = new Vector2(localX, localZ);

                float slope = EstimateSlope(heights, hx, hz);
                float pit = GetBunkerPitMask(point);
                float path = GetAccessPathMask(point);
                float ridge = Smooth01(Mathf.InverseLerp(0.76f, 1f, v));
                float craterMask = GetCraterMask(point, craters);
                float grain = Mathf.PerlinNoise(localX * 0.21f + 11.2f, localZ * 0.21f + 39.4f);

                float dust = 0.84f + grain * 0.08f;
                float rock = ridge * 0.72f + slope * 0.35f + pit * 0.18f;
                float shadow = pit * 0.52f + craterMask * 0.45f + path * 0.10f;

                NormalizeAndAssign(alphamaps, z, x, dust, rock, shadow);
            }
        }

        terrainData.SetAlphamaps(0, 0, alphamaps);
    }

    private float GetBunkerPitMask(Vector2 point)
    {
        Vector2 offset = point - bunkerPitCenter;
        float oval = Mathf.Sqrt(
            Mathf.Pow(offset.x / Mathf.Max(0.01f, bunkerPitRadiusX), 2f)
            + Mathf.Pow(offset.y / Mathf.Max(0.01f, bunkerPitRadiusZ), 2f));
        return 1f - Smooth01(Mathf.InverseLerp(0.62f, 1.15f, oval));
    }

    private float GetAccessPathMask(Vector2 point)
    {
        Vector2 start = new Vector2(terrainWidth * 0.5f, terrainLength * 0.13f);
        Vector2 end = bunkerPitCenter - new Vector2(0f, bunkerPitRadiusZ * 0.45f);
        float distance = DistancePointToSegment(point, start, end);
        return 1f - Smooth01(Mathf.InverseLerp(pathWidth * 0.45f, pathWidth, distance));
    }

    private static float GetCraterMask(Vector2 point, Crater[] craters)
    {
        float mask = 0f;
        foreach (Crater crater in craters)
        {
            float distance = Vector2.Distance(point, crater.Center);
            mask = Mathf.Max(mask, 1f - Smooth01(Mathf.InverseLerp(crater.Radius * 0.25f, crater.Radius, distance)));
        }

        return mask;
    }

    private static void NormalizeAndAssign(float[,,] alphamaps, int z, int x, float dust, float rock, float shadow)
    {
        float total = Mathf.Max(0.0001f, dust + rock + shadow);
        alphamaps[z, x, 0] = dust / total;
        alphamaps[z, x, 1] = rock / total;
        alphamaps[z, x, 2] = shadow / total;

        for (int i = 3; i < alphamaps.GetLength(2); i++)
        {
            alphamaps[z, x, i] = 0f;
        }
    }

    private TerrainLayer[] GetTerrainLayers()
    {
        EnsureFolder(MoonMaterialFolder);

        TerrainLayer dust = LoadOrCreateLayer(DustLayerPath, "TL_Moon_Dust", new Color32(0x76, 0x76, 0x72, 0xff), 0.02f);
        TerrainLayer rock = LoadOrCreateLayer(RockLayerPath, "TL_Moon_Rock", new Color32(0x54, 0x54, 0x52, 0xff), 0.01f);
        TerrainLayer shadow = LoadOrCreateLayer(ShadowLayerPath, "TL_Moon_Shadow", new Color32(0x2f, 0x30, 0x32, 0xff), 0f);
        return new[] { dust, rock, shadow };
    }

    private TerrainLayer LoadOrCreateLayer(string path, string layerName, Color color, float smoothness)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer != null)
        {
            return layer;
        }

        layer = new TerrainLayer();
        Texture2D texture = CreateFallbackTexture(path, color);
        layer.name = layerName;
        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(5f, 5f);
        layer.metallic = 0f;
        layer.smoothness = Mathf.Clamp(smoothness, 0f, 0.02f);
        layer.specular = Color.black;
        layer.normalScale = 0f;
        AssetDatabase.CreateAsset(layer, path);
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static Texture2D CreateFallbackTexture(string layerPath, Color color)
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
                float noise = Mathf.PerlinNoise(x * 0.37f + color.r * 8.3f, y * 0.37f + color.g * 12.1f);
                pixels[y * 16 + x] = color * Mathf.Lerp(0.86f, 1.1f, noise);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private void CreateOrUpdateMarkers(Transform parent)
    {
        CreateOrMoveMarker(parent, "Marker_PlayerSpawn", new Vector3(terrainWidth * 0.5f, 0f, terrainLength * 0.12f));
        CreateOrMoveMarker(parent, "Marker_BunkerEntrance", new Vector3(bunkerPitCenter.x, 0f, bunkerPitCenter.y + bunkerPitRadiusZ * 0.58f));
        CreateOrMoveMarker(parent, "Marker_SolarPanelArea", new Vector3(terrainWidth * 0.28f, 0f, terrainLength * 0.44f));
        CreateOrMoveMarker(parent, "Marker_OxygenExtractorArea", new Vector3(terrainWidth * 0.72f, 0f, terrainLength * 0.43f));
        CreateOrMoveMarker(parent, "Marker_RoverArea", new Vector3(terrainWidth * 0.62f, 0f, terrainLength * 0.23f));
        CreateOrMoveMarker(parent, "Marker_BackgroundTowerArea", new Vector3(terrainWidth * 0.5f, 0f, terrainLength * 0.73f));
    }

    private void CreateOrMoveMarker(Transform parent, string markerName, Vector3 position)
    {
        Transform marker = parent.Find(markerName);
        if (marker == null)
        {
            GameObject markerObject = new GameObject(markerName);
            Undo.RegisterCreatedObjectUndo(markerObject, "Create Reference Terrain Marker");
            marker = markerObject.transform;
            marker.SetParent(parent);
        }

        marker.position = new Vector3(position.x, SampleCurrentTerrainHeight(position), position.z);
        marker.rotation = Quaternion.identity;
        EditorUtility.SetDirty(marker.gameObject);
    }

    private float SampleCurrentTerrainHeight(Vector3 world)
    {
        GameObject terrainObject = GameObject.Find(TerrainName);
        Terrain terrain = terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
        if (terrain == null)
        {
            return 0f;
        }

        return terrain.transform.position.y + terrain.SampleHeight(world) + 0.05f;
    }

    private void ApplySimpleLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.035f, 0.037f, 0.042f);
        RenderSettings.reflectionIntensity = 0.05f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = Color.black;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.skybox = CreateOrUpdateBlackSkybox();

        Light directional = FindDirectionalLight();
        if (directional == null)
        {
            GameObject lightObject = new GameObject("Reference Cool Directional Light");
            Undo.RegisterCreatedObjectUndo(lightObject, "Create Reference Directional Light");
            directional = lightObject.AddComponent<Light>();
        }

        directional.type = LightType.Directional;
        directional.color = new Color(0.82f, 0.9f, 1f);
        directional.intensity = 1.2f;
        directional.shadows = LightShadows.Soft;
        directional.transform.rotation = Quaternion.Euler(33f, -28f, 0f);
        EditorUtility.SetDirty(directional);
    }

    private static Light FindDirectionalLight()
    {
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (light.type == LightType.Directional)
            {
                return light;
            }
        }

        return null;
    }

    private static Material CreateOrUpdateBlackSkybox()
    {
        const string path = "Assets/_Materials/Reference Black Space Skybox.mat";
        EnsureFolder("Assets/_Materials");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_SkyTint"))
        {
            material.SetColor("_SkyTint", Color.black);
        }

        if (material.HasProperty("_GroundColor"))
        {
            material.SetColor("_GroundColor", Color.black);
        }

        if (material.HasProperty("_Exposure"))
        {
            material.SetFloat("_Exposure", 0f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private float SampleImportedHeightmap(float u, float v)
    {
        if (!useImportedHeightmap || importedHeightmap == null)
        {
            return 0f;
        }

        float value = importedHeightmap.GetPixelBilinear(u, v).grayscale;
        return (value - 0.5f) * 2f;
    }

    private void TryAutoSelectHeightmap()
    {
        if (importedHeightmap != null)
        {
            return;
        }

        string[] preferredFolders =
        {
            HeightmapRoot + "/Moon",
            HeightmapRoot + "/FlatsDry",
            HeightmapRoot + "/Desert",
            HeightmapRoot + "/HillsDry",
            HeightmapRoot + "/Mars",
            HeightmapRoot + "/CanyonsV1",
            HeightmapRoot + "/CanyonsV2",
            HeightmapRoot + "/CanyonsV3",
            HeightmapRoot + "/CanyonsV4"
        };

        foreach (string folder in preferredFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                continue;
            }

            string guid = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).FirstOrDefault();
            if (string.IsNullOrEmpty(guid))
            {
                continue;
            }

            importedHeightmap = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            return;
        }
    }

    private void EnsureHeightmapReadable()
    {
        if (importedHeightmap == null)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(importedHeightmap);
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

        if (importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
            importedHeightmap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    private void ValidateSettings()
    {
        terrainWidth = Mathf.Max(10f, terrainWidth);
        terrainLength = Mathf.Max(10f, terrainLength);
        terrainHeight = Mathf.Max(1f, terrainHeight);
        bunkerPitCenter.x = Mathf.Clamp(bunkerPitCenter.x, bunkerPitRadiusX + 2f, terrainWidth - bunkerPitRadiusX - 2f);
        bunkerPitCenter.y = Mathf.Clamp(bunkerPitCenter.y, terrainLength * 0.28f, terrainLength * 0.72f);
        bunkerPitRadiusX = Mathf.Max(1f, bunkerPitRadiusX);
        bunkerPitRadiusZ = Mathf.Max(1f, bunkerPitRadiusZ);
        bunkerPitDepth = Mathf.Clamp(bunkerPitDepth, 0f, terrainHeight * 0.45f);
        pathWidth = Mathf.Max(0.5f, pathWidth);
        pathDepth = Mathf.Clamp(pathDepth, 0f, terrainHeight * 0.2f);
        farRidgeHeight = Mathf.Clamp(farRidgeHeight, 0f, terrainHeight * 0.55f);
        noiseStrength = Mathf.Clamp(noiseStrength, 0f, 1.5f);
        craterCount = Mathf.Max(0, craterCount);
        smoothIterations = Mathf.Clamp(smoothIterations, 0, 6);
    }

    private static float[,] SmoothHeights(float[,] source)
    {
        int height = source.GetLength(0);
        int width = source.GetLength(1);
        float[,] result = new float[height, width];

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x == 0 || z == 0 || x == width - 1 || z == height - 1)
                {
                    result[z, x] = source[z, x];
                    continue;
                }

                float center = source[z, x] * 4f;
                float neighbours = source[z, x - 1] + source[z, x + 1] + source[z - 1, x] + source[z + 1, x];
                result[z, x] = (center + neighbours) / 8f;
            }
        }

        return result;
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
        return Mathf.Clamp01((dx + dz) * 55f);
    }

    private static float FractalNoise(Vector2 point, float frequency, int octaves, float offset)
    {
        float value = 0f;
        float amplitude = 1f;
        float totalAmplitude = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += SignedPerlin(point, frequency, offset + i * 31.7f) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.1f;
        }

        return totalAmplitude > 0f ? value / totalAmplitude : 0f;
    }

    private static float SignedPerlin(Vector2 point, float frequency, float offset)
    {
        return Mathf.PerlinNoise(point.x * frequency + offset, point.y * frequency + offset * 0.71f) * 2f - 1f;
    }

    private static float SuperEllipse(Vector2 offset, float radiusX, float radiusZ, float exponent)
    {
        return Mathf.Pow(Mathf.Abs(offset.x) / Mathf.Max(0.01f, radiusX), exponent)
            + Mathf.Pow(Mathf.Abs(offset.y) / Mathf.Max(0.01f, radiusZ), exponent);
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 ab = end - start;
        float denominator = Vector2.Dot(ab, ab);
        if (denominator <= Mathf.Epsilon)
        {
            return Vector2.Distance(point, start);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - start, ab) / denominator);
        return Vector2.Distance(point, start + ab * t);
    }

    private static float PathProgress(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 ab = end - start;
        float denominator = Vector2.Dot(ab, ab);
        if (denominator <= Mathf.Epsilon)
        {
            return 0f;
        }

        return Mathf.Clamp01(Vector2.Dot(point - start, ab) / denominator);
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
}
