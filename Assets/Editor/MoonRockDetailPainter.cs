using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class MoonRockDetailPainter : EditorWindow
{
    private const string ExternalRockFolder = "Assets/_External/Models/Rocks";
    private const string PrefabRockFolder = "Assets/_Prefabs/Environment";
    private const int MaxRockAssets = 3;

    private Terrain targetTerrain;
    private Terrain lastTerrain;
    private readonly List<Object> rockAssets = new List<Object>();
    private Vector2 scrollPosition;

    private int detailResolution = 512;
    private int detailResolutionPerPatch = 16;
    private int density = 2;
    private float minWidth = 0.03f;
    private float maxWidth = 0.10f;
    private float minHeight = 0.03f;
    private float maxHeight = 0.10f;
    private float noiseSpread = 0.35f;
    private bool usePrototypeMesh = true;
    private bool useInstancing = true;
    private bool avoidMainPath = true;
    private float pathClearWidth = 5f;
    private bool avoidVaultEntrance = true;
    private float vaultClearRadius = 6f;

    [MenuItem("Tools/Regolith/Paint Moon Rocks As Terrain Details")]
    public static void Open()
    {
        GetWindow<MoonRockDetailPainter>("Moon Rock Details");
    }

    private void OnEnable()
    {
        AutoFindTerrain();
    }

    private void OnGUI()
    {
        AutoFindTerrain();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawTerrainField();
        EditorGUILayout.Space(8f);
        DrawRockList();
        EditorGUILayout.Space(8f);
        DrawDetailSettings();
        EditorGUILayout.Space(12f);
        DrawButtons();
        EditorGUILayout.EndScrollView();
    }

    private void DrawTerrainField()
    {
        EditorGUILayout.LabelField("Terrain", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Active Terrain", targetTerrain, typeof(Terrain), true);
        if (EditorGUI.EndChangeCheck())
        {
            lastTerrain = targetTerrain;
        }

        if (GUILayout.Button("Auto-Find Terrain"))
        {
            targetTerrain = null;
            AutoFindTerrain();
        }
    }

    private void DrawRockList()
    {
        EditorGUILayout.LabelField("Rock Detail Prefabs / Meshes", EditorStyles.boldLabel);
        Rect dropRect = GUILayoutUtility.GetRect(0f, 48f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "Drop 1 to 3 low-poly rock prefabs, model prefabs, meshes, or rock folders here", EditorStyles.helpBox);
        HandleRockDrop(dropRect);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Common Rock Assets"))
            {
                AddCommonRockAssets();
            }

            if (GUILayout.Button("Clear List"))
            {
                rockAssets.Clear();
            }
        }

        int removeIndex = -1;
        for (int i = 0; i < rockAssets.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Object changed = EditorGUILayout.ObjectField(rockAssets[i], typeof(Object), false);
                if (changed != rockAssets[i])
                {
                    rockAssets[i] = ResolvePrototypeObject(changed) != null ? changed : null;
                }

                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    removeIndex = i;
                }
            }
        }

        if (removeIndex >= 0)
        {
            rockAssets.RemoveAt(removeIndex);
        }

        rockAssets.RemoveAll(asset => asset == null || ResolvePrototypeObject(asset) == null);
        while (rockAssets.Count > MaxRockAssets)
        {
            rockAssets.RemoveAt(rockAssets.Count - 1);
        }
    }

    private void DrawDetailSettings()
    {
        EditorGUILayout.LabelField("Detail Settings", EditorStyles.boldLabel);
        detailResolution = Mathf.Clamp(EditorGUILayout.IntField("Detail Resolution", detailResolution), 32, 4096);
        detailResolutionPerPatch = Mathf.Clamp(EditorGUILayout.IntField("Resolution Per Patch", detailResolutionPerPatch), 8, 128);
        density = Mathf.Clamp(EditorGUILayout.IntField("Density", density), 0, 16);
        minWidth = Mathf.Max(0.001f, EditorGUILayout.FloatField("Min Width", minWidth));
        maxWidth = Mathf.Max(minWidth, EditorGUILayout.FloatField("Max Width", maxWidth));
        minHeight = Mathf.Max(0.001f, EditorGUILayout.FloatField("Min Height", minHeight));
        maxHeight = Mathf.Max(minHeight, EditorGUILayout.FloatField("Max Height", maxHeight));
        noiseSpread = Mathf.Max(0.01f, EditorGUILayout.FloatField("Noise Spread", noiseSpread));
        usePrototypeMesh = EditorGUILayout.Toggle("Use Prototype Mesh", usePrototypeMesh);
        useInstancing = EditorGUILayout.Toggle("Use Instancing", useInstancing);
        avoidMainPath = EditorGUILayout.Toggle("Avoid Main Path", avoidMainPath);
        pathClearWidth = Mathf.Max(0f, EditorGUILayout.FloatField("Path Clear Width", pathClearWidth));
        avoidVaultEntrance = EditorGUILayout.Toggle("Avoid Vault Entrance", avoidVaultEntrance);
        vaultClearRadius = Mathf.Max(0f, EditorGUILayout.FloatField("Vault Clear Radius", vaultClearRadius));
    }

    private void DrawButtons()
    {
        if (GUILayout.Button("Add Rock Detail Prototypes", GUILayout.Height(32f)))
        {
            AddRockDetailPrototypes();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Paint Rock Details", GUILayout.Height(32f)))
            {
                PaintRockDetails();
            }

            if (GUILayout.Button("Clear Rock Details", GUILayout.Height(32f)))
            {
                ClearRockDetails();
            }
        }
    }

    private void AddRockDetailPrototypes()
    {
        if (!ValidateTerrainAndAssets())
        {
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        Undo.RegisterCompleteObjectUndo(terrainData, "Add Moon Rock Detail Prototypes");

        List<DetailPrototype> prototypes = terrainData.detailPrototypes.ToList();
        foreach (Object rockAsset in rockAssets.Take(MaxRockAssets))
        {
            GameObject prototypeObject = ResolvePrototypeObject(rockAsset);
            if (prototypeObject == null)
            {
                continue;
            }

            int existingIndex = FindPrototypeIndex(prototypes, prototypeObject);
            DetailPrototype prototype = CreateDetailPrototype(prototypeObject);
            if (existingIndex >= 0)
            {
                prototypes[existingIndex] = prototype;
            }
            else
            {
                prototypes.Add(prototype);
            }
        }

        terrainData.detailPrototypes = prototypes.ToArray();
        EnsureDetailResolution(terrainData);
        EditorUtility.SetDirty(terrainData);
        AssetDatabase.SaveAssets();

        Debug.Log($"Moon Rock Detail Painter: Terrain '{targetTerrain.name}' now has {terrainData.detailPrototypes.Length} DetailPrototype entries.");
    }

    private void PaintRockDetails()
    {
        if (!ValidateTerrainAndAssets())
        {
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        Undo.RegisterCompleteObjectUndo(terrainData, "Paint Moon Rock Details");

        AddRockDetailPrototypes();
        EnsureDetailResolution(terrainData);

        List<int> prototypeIndices = GetSelectedPrototypeIndices(terrainData);
        if (prototypeIndices.Count == 0)
        {
            Debug.LogWarning("Moon Rock Detail Painter: no matching rock DetailPrototype entries were found after adding prototypes.");
            return;
        }

        PathInfo pathInfo = BuildMainPathInfo(targetTerrain);
        List<Vector3> vaultAvoidancePoints = BuildVaultAvoidancePoints();
        int paintedCells = 0;
        int estimatedInstances = 0;
        int resolution = terrainData.detailResolution;

        for (int prototypeSlot = 0; prototypeSlot < prototypeIndices.Count; prototypeSlot++)
        {
            int prototypeIndex = prototypeIndices[prototypeSlot];
            int[,] layer = new int[resolution, resolution];
            System.Random random = new System.Random(982451653 + prototypeSlot * 7919);

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Vector3 world = DetailIndexToWorld(x, z, resolution, targetTerrain);
                    float weight = GetDensityWeight(world, targetTerrain, pathInfo, vaultAvoidancePoints);

                    if (weight <= 0f || random.NextDouble() > weight)
                    {
                        continue;
                    }

                    int value = Mathf.Clamp(Mathf.CeilToInt(weight * density), 0, density);
                    if (value <= 0)
                    {
                        continue;
                    }

                    layer[z, x] = value;
                    paintedCells++;
                    estimatedInstances += value;
                }
            }

            terrainData.SetDetailLayer(0, 0, prototypeIndex, layer);
        }

        EditorUtility.SetDirty(terrainData);
        AssetDatabase.SaveAssets();

        Debug.Log($"Moon Rock Detail Painter: painted Terrain '{targetTerrain.name}'.");
        Debug.Log($"Moon Rock Detail Painter: estimated painted detail cells {paintedCells}, estimated detail instances {estimatedInstances}.");
        Debug.Log($"Moon Rock Detail Painter: Terrain '{targetTerrain.name}' has {terrainData.detailPrototypes.Length} DetailPrototype entries.");
    }

    private void ClearRockDetails()
    {
        AutoFindTerrain();
        if (targetTerrain == null || targetTerrain.terrainData == null)
        {
            Debug.LogWarning("Moon Rock Detail Painter: no active Terrain found.");
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        Undo.RegisterCompleteObjectUndo(terrainData, "Clear Moon Rock Details");

        List<int> indices = GetClearablePrototypeIndices(terrainData);
        int resolution = terrainData.detailResolution;
        int[,] emptyLayer = new int[resolution, resolution];

        foreach (int index in indices)
        {
            terrainData.SetDetailLayer(0, 0, index, emptyLayer);
        }

        EditorUtility.SetDirty(terrainData);
        AssetDatabase.SaveAssets();
        Debug.Log($"Moon Rock Detail Painter: cleared {indices.Count} rock detail layer(s) on Terrain '{targetTerrain.name}'.");
    }

    private bool ValidateTerrainAndAssets()
    {
        AutoFindTerrain();
        if (targetTerrain == null || targetTerrain.terrainData == null)
        {
            Debug.LogWarning("Moon Rock Detail Painter: no active Terrain found.");
            return false;
        }

        rockAssets.RemoveAll(asset => asset == null || ResolvePrototypeObject(asset) == null);
        while (rockAssets.Count > MaxRockAssets)
        {
            rockAssets.RemoveAt(rockAssets.Count - 1);
        }

        if (rockAssets.Count == 0)
        {
            Debug.LogWarning("Moon Rock Detail Painter: no rock prefab, model prefab, or mesh assigned.");
            return false;
        }

        return true;
    }

    private DetailPrototype CreateDetailPrototype(GameObject prototypeObject)
    {
        DetailPrototype prototype = new DetailPrototype
        {
            prototype = prototypeObject,
            prototypeTexture = null,
            renderMode = DetailRenderMode.VertexLit,
            minWidth = minWidth,
            maxWidth = maxWidth,
            minHeight = minHeight,
            maxHeight = maxHeight,
            noiseSpread = noiseSpread,
            healthyColor = new Color(0.74f, 0.74f, 0.72f, 1f),
            dryColor = new Color(0.48f, 0.48f, 0.47f, 1f)
        };

        SetDetailPrototypeBool(prototype, "usePrototypeMesh", usePrototypeMesh);
        SetDetailPrototypeBool(prototype, "useInstancing", useInstancing);
        return prototype;
    }

    private static void SetDetailPrototypeBool(DetailPrototype prototype, string memberName, bool value)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo property = typeof(DetailPrototype).GetProperty(memberName, flags);
        if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
        {
            property.SetValue(prototype, value);
            return;
        }

        FieldInfo field = typeof(DetailPrototype).GetField(memberName, flags);
        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(prototype, value);
        }
    }

    private void EnsureDetailResolution(TerrainData terrainData)
    {
        int safeResolution = Mathf.Clamp(detailResolution, 32, 4096);
        int safePerPatch = Mathf.Clamp(detailResolutionPerPatch, 8, 128);

        if (terrainData.detailResolution != safeResolution || terrainData.detailResolutionPerPatch != safePerPatch)
        {
            terrainData.SetDetailResolution(safeResolution, safePerPatch);
        }
    }

    private List<int> GetSelectedPrototypeIndices(TerrainData terrainData)
    {
        List<int> indices = new List<int>();
        List<GameObject> selectedPrototypes = rockAssets
            .Select(ResolvePrototypeObject)
            .Where(prototype => prototype != null)
            .ToList();

        DetailPrototype[] prototypes = terrainData.detailPrototypes;
        for (int i = 0; i < prototypes.Length; i++)
        {
            if (prototypes[i].prototype != null && selectedPrototypes.Contains(prototypes[i].prototype))
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    private List<int> GetClearablePrototypeIndices(TerrainData terrainData)
    {
        List<int> selected = GetSelectedPrototypeIndices(terrainData);
        if (selected.Count > 0)
        {
            return selected;
        }

        List<int> rockLike = new List<int>();
        DetailPrototype[] prototypes = terrainData.detailPrototypes;
        for (int i = 0; i < prototypes.Length; i++)
        {
            GameObject prototype = prototypes[i].prototype;
            if (prototype == null)
            {
                continue;
            }

            string path = AssetDatabase.GetAssetPath(prototype).Replace("\\", "/");
            string name = prototype.name.ToLowerInvariant();
            if (path.StartsWith(ExternalRockFolder) || path.StartsWith(PrefabRockFolder) || name.Contains("rock") || name.Contains("moon"))
            {
                rockLike.Add(i);
            }
        }

        return rockLike;
    }

    private static int FindPrototypeIndex(List<DetailPrototype> prototypes, GameObject prototypeObject)
    {
        for (int i = 0; i < prototypes.Count; i++)
        {
            if (prototypes[i].prototype == prototypeObject)
            {
                return i;
            }
        }

        return -1;
    }

    private void AutoFindTerrain()
    {
        if (targetTerrain == null)
        {
            targetTerrain = Terrain.activeTerrain;
            if (targetTerrain == null)
            {
                targetTerrain = Object.FindAnyObjectByType<Terrain>();
            }
        }

        if (targetTerrain != null && targetTerrain != lastTerrain)
        {
            lastTerrain = targetTerrain;
        }
    }

    private void HandleRockDrop(Rect dropRect)
    {
        Event current = Event.current;
        if (!dropRect.Contains(current.mousePosition))
        {
            return;
        }

        if (current.type == EventType.DragUpdated || current.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (Object draggedObject in DragAndDrop.objectReferences)
                {
                    AddRockAsset(draggedObject);
                }
            }

            current.Use();
        }
    }

    private void AddCommonRockAssets()
    {
        AddRockAssetsFromFolder(PrefabRockFolder);
        AddRockAssetsFromFolder(ExternalRockFolder);
    }

    private void AddRockAssetsFromFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder) || rockAssets.Count >= MaxRockAssets)
        {
            return;
        }

        string[] gameObjectGuids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
        foreach (string guid in gameObjectGuids)
        {
            if (rockAssets.Count >= MaxRockAssets)
            {
                return;
            }

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            AddRockAsset(asset);
        }

        string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { folder });
        foreach (string guid in meshGuids)
        {
            if (rockAssets.Count >= MaxRockAssets)
            {
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Mesh)
                {
                    AddRockAsset(asset);
                }
            }
        }
    }

    private void AddRockAsset(Object asset)
    {
        if (asset == null || rockAssets.Count >= MaxRockAssets)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(asset);
        if (AssetDatabase.IsValidFolder(path))
        {
            AddRockAssetsFromFolder(path);
            return;
        }

        GameObject prototype = ResolvePrototypeObject(asset);
        if (prototype == null)
        {
            Debug.LogWarning($"Moon Rock Detail Painter: skipped unsupported asset '{asset.name}'. Use a prefab, model prefab, or mesh from a model asset.");
            return;
        }

        if (!rockAssets.Contains(asset) && !rockAssets.Any(existing => ResolvePrototypeObject(existing) == prototype))
        {
            rockAssets.Add(asset);
        }
    }

    private static GameObject ResolvePrototypeObject(Object asset)
    {
        if (asset == null)
        {
            return null;
        }

        if (asset is GameObject gameObject && HasRenderableMesh(gameObject))
        {
            return gameObject;
        }

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        GameObject mainObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (mainObject != null && HasRenderableMesh(mainObject))
        {
            return mainObject;
        }

        return null;
    }

    private static bool HasRenderableMesh(GameObject gameObject)
    {
        return gameObject != null && gameObject.GetComponentInChildren<MeshFilter>() != null;
    }

    private float GetDensityWeight(Vector3 world, Terrain terrain, PathInfo pathInfo, List<Vector3> vaultAvoidancePoints)
    {
        if (!IsInsideTerrain(world, terrain))
        {
            return 0f;
        }

        if (avoidVaultEntrance)
        {
            foreach (Vector3 vaultPoint in vaultAvoidancePoints)
            {
                if (DistanceXZ(world, vaultPoint) < vaultClearRadius)
                {
                    return 0f;
                }
            }
        }

        float slope = Vector3.Angle(Vector3.up, GetTerrainNormal(world, terrain)) / 90f;
        float edge = GetEdgeFactor(world, terrain);
        float weight = Mathf.Lerp(0.08f, 0.42f, edge);
        weight += Mathf.Clamp01(slope) * 0.18f;

        if (avoidMainPath && pathInfo.HasPath)
        {
            float pathDistance = DistancePointToSegmentXZ(world, pathInfo.Start, pathInfo.End);
            float pathFade = Smooth01(Mathf.InverseLerp(pathClearWidth, pathClearWidth * 2.6f + 0.01f, pathDistance));
            weight *= pathFade;
        }

        float noise = Mathf.PerlinNoise(world.x / Mathf.Max(0.01f, noiseSpread * 35f), world.z / Mathf.Max(0.01f, noiseSpread * 35f));
        weight *= Mathf.Lerp(0.45f, 1.35f, noise);

        return Mathf.Clamp01(weight);
    }

    private static Vector3 DetailIndexToWorld(int x, int z, int resolution, Terrain terrain)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float u = (x + 0.5f) / resolution;
        float v = (z + 0.5f) / resolution;
        Vector3 world = terrainPosition + new Vector3(u * size.x, 0f, v * size.z);
        world.y = terrainPosition.y + terrain.SampleHeight(world);
        return world;
    }

    private static bool IsInsideTerrain(Vector3 point, Terrain terrain)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        return point.x >= terrainPosition.x
            && point.x <= terrainPosition.x + size.x
            && point.z >= terrainPosition.z
            && point.z <= terrainPosition.z + size.z;
    }

    private static float GetEdgeFactor(Vector3 point, Terrain terrain)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        Vector3 terrainCenter = terrainPosition + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        float edgeX = Mathf.Abs(point.x - terrainCenter.x) / Mathf.Max(0.01f, size.x * 0.5f);
        float edgeZ = Mathf.Abs(point.z - terrainCenter.z) / Mathf.Max(0.01f, size.z * 0.5f);
        return Smooth01(Mathf.Clamp01(Mathf.Max(edgeX, edgeZ)));
    }

    private static Vector3 GetTerrainNormal(Vector3 worldPoint, Terrain terrain)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float normalizedX = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + size.x, worldPoint.x);
        float normalizedZ = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + size.z, worldPoint.z);
        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
        return terrain.transform.TransformDirection(normal).normalized;
    }

    private PathInfo BuildMainPathInfo(Terrain terrain)
    {
        GameObject player = GameObject.Find("Player");
        GameObject destination = FindFirstNamedObject("Base Entrance", "Vault Door", "Vault Entrance");

        if (player != null && destination != null)
        {
            return new PathInfo
            {
                HasPath = true,
                Start = player.transform.position,
                End = destination.transform.position
            };
        }

        Vector3 terrainCenter = terrain.transform.position + new Vector3(
            terrain.terrainData.size.x * 0.5f,
            0f,
            terrain.terrainData.size.z * 0.5f);

        return new PathInfo
        {
            HasPath = true,
            Start = terrainCenter + Vector3.back * terrain.terrainData.size.z * 0.18f,
            End = terrainCenter + Vector3.forward * terrain.terrainData.size.z * 0.48f
        };
    }

    private static GameObject FindFirstNamedObject(params string[] names)
    {
        foreach (string name in names)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                return found;
            }
        }

        foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            string objectName = transform.name.ToLowerInvariant();
            if (objectName.Contains("vault")
                || objectName.Contains("base entrance")
                || objectName.Contains("entrance")
                || objectName.Contains("door"))
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    private static List<Vector3> BuildVaultAvoidancePoints()
    {
        List<Vector3> points = new List<Vector3>();
        foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            string objectName = transform.name.ToLowerInvariant();
            if (objectName.Contains("vault")
                || objectName.Contains("base entrance")
                || objectName.Contains("entrance")
                || objectName.Contains("door"))
            {
                points.Add(transform.position);
            }
        }

        return points;
    }

    private static float DistancePointToSegmentXZ(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 a = new Vector2(start.x, start.z);
        Vector2 b = new Vector2(end.x, end.z);
        Vector2 ab = b - a;
        float denominator = Vector2.Dot(ab, ab);

        if (denominator <= Mathf.Epsilon)
        {
            return Vector2.Distance(p, a);
        }

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denominator);
        return Vector2.Distance(p, a + ab * t);
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x;
        float z = a.z - b.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private struct PathInfo
    {
        public bool HasPath;
        public Vector3 Start;
        public Vector3 End;
    }
}
