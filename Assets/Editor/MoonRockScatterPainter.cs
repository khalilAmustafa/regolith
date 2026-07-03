using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class MoonRockScatterPainter : EditorWindow
{
    private const string ParentName = "RockScatter_Static";
    private const string ExternalRockFolder = "Assets/_External/Models/Rocks";
    private const string PrefabRockFolder = "Assets/_Prefabs/Environment";

    private Terrain targetTerrain;
    private Terrain lastTerrain;
    private readonly List<Object> rockAssets = new List<Object>();
    private Vector2 scrollPosition;

    private int count = 80;
    private float radius = 25f;
    private Vector3 center = Vector3.zero;
    private float minScale = 0.03f;
    private float maxScale = 0.10f;
    private float yOffset = 0.02f;
    private bool alignToTerrainNormal = true;
    private float maxTiltDegrees = 18f;
    private bool randomYRotation = true;
    private bool markStatic = true;
    private bool disableColliders = true;
    private bool avoidMainPath = true;
    private float pathClearWidth = 5f;
    private bool avoidVaultEntrance = true;
    private float vaultClearRadius = 5f;

    [MenuItem("Tools/Regolith/Rock Scatter Painter")]
    public static void Open()
    {
        GetWindow<MoonRockScatterPainter>("Rock Scatter Painter");
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
        DrawRockAssetList();
        EditorGUILayout.Space(8f);
        DrawScatterSettings();
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
            SetCenterToTerrainCenter();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto-Find Terrain"))
            {
                targetTerrain = null;
                AutoFindTerrain();
                SetCenterToTerrainCenter();
            }

            if (GUILayout.Button("Use Terrain Center"))
            {
                SetCenterToTerrainCenter();
            }
        }
    }

    private void DrawRockAssetList()
    {
        EditorGUILayout.LabelField("Rock Prefabs / Meshes", EditorStyles.boldLabel);
        Rect dropRect = GUILayoutUtility.GetRect(0f, 48f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "Drop rock prefabs, model prefabs, meshes, or rock folders here", EditorStyles.helpBox);
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
                Object newAsset = EditorGUILayout.ObjectField(rockAssets[i], typeof(Object), false);
                if (newAsset != rockAssets[i])
                {
                    rockAssets[i] = IsValidRockAsset(newAsset) ? newAsset : null;
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

        rockAssets.RemoveAll(asset => asset == null);
    }

    private void DrawScatterSettings()
    {
        EditorGUILayout.LabelField("Scatter Settings", EditorStyles.boldLabel);
        count = Mathf.Max(0, EditorGUILayout.IntField("Count", count));
        radius = Mathf.Max(0.1f, EditorGUILayout.FloatField("Radius", radius));
        center = EditorGUILayout.Vector3Field("Center", center);
        minScale = Mathf.Max(0.001f, EditorGUILayout.FloatField("Min Scale", minScale));
        maxScale = Mathf.Max(minScale, EditorGUILayout.FloatField("Max Scale", maxScale));
        yOffset = EditorGUILayout.FloatField("Y Offset", yOffset);
        alignToTerrainNormal = EditorGUILayout.Toggle("Align To Terrain Normal", alignToTerrainNormal);
        maxTiltDegrees = Mathf.Clamp(EditorGUILayout.FloatField("Max Tilt Degrees", maxTiltDegrees), 0f, 89f);
        randomYRotation = EditorGUILayout.Toggle("Random Y Rotation", randomYRotation);
        markStatic = EditorGUILayout.Toggle("Mark Static", markStatic);
        disableColliders = EditorGUILayout.Toggle("Disable Colliders", disableColliders);
        avoidMainPath = EditorGUILayout.Toggle("Avoid Main Path", avoidMainPath);
        pathClearWidth = Mathf.Max(0f, EditorGUILayout.FloatField("Path Clear Width", pathClearWidth));
        avoidVaultEntrance = EditorGUILayout.Toggle("Avoid Vault Entrance", avoidVaultEntrance);
        vaultClearRadius = Mathf.Max(0f, EditorGUILayout.FloatField("Vault Clear Radius", vaultClearRadius));
    }

    private void DrawButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scatter Rocks", GUILayout.Height(34f)))
            {
                ScatterRocks();
            }

            if (GUILayout.Button("Clear Generated Rocks", GUILayout.Height(34f)))
            {
                ClearGeneratedRocks();
            }
        }

        if (GUILayout.Button("Select Generated Rocks", GUILayout.Height(28f)))
        {
            SelectGeneratedRocks();
        }
    }

    private void ScatterRocks()
    {
        AutoFindTerrain();
        if (targetTerrain == null || targetTerrain.terrainData == null)
        {
            Debug.LogWarning("Rock Scatter Painter: no active Terrain found.");
            return;
        }

        rockAssets.RemoveAll(asset => !IsValidRockAsset(asset));
        if (rockAssets.Count == 0)
        {
            Debug.LogWarning("Rock Scatter Painter: no rock prefabs or mesh assets assigned.");
            return;
        }

        int requestedCount = Mathf.Max(0, count);
        if (requestedCount == 0)
        {
            Debug.Log("Rock Scatter Painter: count is 0, no rocks spawned.");
            return;
        }

        GameObject parent = GetOrCreateParent();
        PathInfo pathInfo = BuildMainPathInfo(targetTerrain);
        List<Vector3> vaultAvoidancePoints = BuildVaultAvoidancePoints();
        List<GameObject> spawned = new List<GameObject>(requestedCount);
        System.Random random = new System.Random(System.Environment.TickCount);
        int attempts = 0;
        int maxAttempts = Mathf.Max(requestedCount * 80, 240);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Scatter Moon Rocks");

        while (spawned.Count < requestedCount && attempts < maxAttempts)
        {
            attempts++;
            Vector3 candidate = GetCandidatePoint(random);

            if (!IsInsideTerrain(candidate, targetTerrain))
            {
                continue;
            }

            if (avoidMainPath && pathInfo.HasPath && DistancePointToSegmentXZ(candidate, pathInfo.Start, pathInfo.End) < pathClearWidth)
            {
                continue;
            }

            if (avoidVaultEntrance && IsInsideVaultAvoidance(candidate, vaultAvoidancePoints))
            {
                continue;
            }

            if (!PassesDensity(candidate, random, targetTerrain, pathInfo))
            {
                continue;
            }

            GroundHit ground = GetGroundHit(candidate, targetTerrain);
            if (!ground.Valid)
            {
                continue;
            }

            Object rockAsset = rockAssets[random.Next(rockAssets.Count)];
            GameObject rock = CreateRockInstance(rockAsset);
            if (rock == null)
            {
                continue;
            }

            rock.name = $"{rockAsset.name}_Scatter_{spawned.Count + 1:000}";
            rock.transform.SetParent(parent.transform, true);
            rock.transform.position = ground.Position + Vector3.up * yOffset;
            rock.transform.rotation = BuildRockRotation(ground.Normal, random);

            float scale = Range(random, minScale, maxScale);
            rock.transform.localScale = Vector3.Scale(rock.transform.localScale, Vector3.one * scale);

            if (disableColliders)
            {
                DisableColliders(rock);
            }

            if (markStatic)
            {
                SetStaticRecursive(rock);
            }

            Undo.RegisterCreatedObjectUndo(rock, "Scatter Moon Rock");
            spawned.Add(rock);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(parent);
        Selection.objects = spawned.ToArray();

        Debug.Log($"Rock Scatter Painter: spawned {spawned.Count} of {requestedCount} rocks under {ParentName}.");
        Debug.Log($"Rock Scatter Painter: terrain used '{targetTerrain.name}'.");
        if (spawned.Count < requestedCount)
        {
            Debug.LogWarning("Rock Scatter Painter: placement constraints prevented the full requested count. Reduce avoidance widths or increase radius.");
        }
    }

    private void ClearGeneratedRocks()
    {
        GameObject parent = GameObject.Find(ParentName);
        if (parent == null)
        {
            Debug.Log("Rock Scatter Painter: no generated rock parent found.");
            return;
        }

        int cleared = parent.transform.childCount;
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(parent.transform.GetChild(i).gameObject);
        }

        Debug.Log($"Rock Scatter Painter: cleared {cleared} generated rocks from {ParentName}.");
    }

    private void SelectGeneratedRocks()
    {
        GameObject parent = GameObject.Find(ParentName);
        if (parent == null || parent.transform.childCount == 0)
        {
            Debug.LogWarning("Rock Scatter Painter: no generated rocks to select.");
            return;
        }

        Object[] selection = new Object[parent.transform.childCount];
        for (int i = 0; i < parent.transform.childCount; i++)
        {
            selection[i] = parent.transform.GetChild(i).gameObject;
        }

        Selection.objects = selection;
        EditorGUIUtility.PingObject(parent);
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
            SetCenterToTerrainCenter();
        }
    }

    private void SetCenterToTerrainCenter()
    {
        if (targetTerrain == null || targetTerrain.terrainData == null)
        {
            return;
        }

        Vector3 size = targetTerrain.terrainData.size;
        center = targetTerrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        center.y = targetTerrain.transform.position.y + targetTerrain.SampleHeight(center);
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
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string[] gameObjectGuids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
        foreach (string guid in gameObjectGuids)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            AddRockAsset(asset);
        }

        string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { folder });
        foreach (string guid in meshGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in assets)
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
        if (asset == null)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(asset);
        if (AssetDatabase.IsValidFolder(path))
        {
            AddRockAssetsFromFolder(path);
            return;
        }

        if (!IsValidRockAsset(asset))
        {
            Debug.LogWarning($"Rock Scatter Painter: skipped unsupported asset '{asset.name}'. Use GameObject prefabs/model prefabs or Mesh assets.");
            return;
        }

        if (!rockAssets.Contains(asset))
        {
            rockAssets.Add(asset);
        }
    }

    private static bool IsValidRockAsset(Object asset)
    {
        return asset is GameObject || asset is Mesh;
    }

    private GameObject GetOrCreateParent()
    {
        GameObject parent = GameObject.Find(ParentName);
        if (parent == null)
        {
            parent = new GameObject(ParentName);
            Undo.RegisterCreatedObjectUndo(parent, "Create Rock Scatter Parent");
        }

        if (markStatic)
        {
            parent.isStatic = true;
        }

        return parent;
    }

    private GameObject CreateRockInstance(Object rockAsset)
    {
        if (rockAsset is GameObject rockPrefab)
        {
            GameObject instance = null;
            string path = AssetDatabase.GetAssetPath(rockPrefab);
            if (!string.IsNullOrEmpty(path))
            {
                instance = PrefabUtility.InstantiatePrefab(rockPrefab) as GameObject;
            }

            if (instance == null)
            {
                instance = Instantiate(rockPrefab);
            }

            return instance;
        }

        if (rockAsset is Mesh mesh)
        {
            GameObject instance = new GameObject(mesh.name);
            MeshFilter meshFilter = instance.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = instance.AddComponent<MeshRenderer>();
            Material[] materials = LoadMeshMaterials(mesh);
            if (materials.Length > 0)
            {
                meshRenderer.sharedMaterials = ExpandMaterials(materials, mesh.subMeshCount);
            }

            return instance;
        }

        return null;
    }

    private static Material[] LoadMeshMaterials(Mesh mesh)
    {
        string path = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(path))
        {
            return new Material[0];
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        List<Material> materials = new List<Material>();
        foreach (Object asset in assets)
        {
            if (asset is Material material)
            {
                materials.Add(material);
            }
        }

        return materials.ToArray();
    }

    private static Material[] ExpandMaterials(Material[] source, int subMeshCount)
    {
        int count = Mathf.Max(1, subMeshCount);
        Material[] expanded = new Material[count];
        for (int i = 0; i < expanded.Length; i++)
        {
            expanded[i] = source[Mathf.Min(i, source.Length - 1)];
        }

        return expanded;
    }

    private Vector3 GetCandidatePoint(System.Random random)
    {
        float angle = Range(random, 0f, Mathf.PI * 2f);
        bool edgeBiased = random.NextDouble() < 0.68;
        float distance01 = edgeBiased
            ? Mathf.Pow((float)random.NextDouble(), 0.34f)
            : Mathf.Sqrt((float)random.NextDouble());
        float distance = distance01 * radius;

        return center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
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

    private GroundHit GetGroundHit(Vector3 candidate, Terrain terrain)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        Vector3 samplePoint = new Vector3(candidate.x, terrainPosition.y + terrain.SampleHeight(candidate), candidate.z);
        Vector3 normal = GetTerrainNormal(candidate, terrain);

        Ray ray = new Ray(new Vector3(candidate.x, terrainPosition.y + size.y + 100f, candidate.z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, size.y + 200f))
        {
            Terrain hitTerrain = hit.collider.GetComponent<Terrain>();
            TerrainCollider terrainCollider = hit.collider as TerrainCollider;
            bool hitTargetTerrain = hitTerrain == terrain
                || (terrainCollider != null && terrainCollider.terrainData == terrain.terrainData);

            if (hitTargetTerrain)
            {
                samplePoint = hit.point;
                normal = hit.normal;
            }
        }

        return new GroundHit
        {
            Valid = samplePoint.y >= terrainPosition.y - 0.05f,
            Position = samplePoint,
            Normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up
        };
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

    private Quaternion BuildRockRotation(Vector3 terrainNormal, System.Random random)
    {
        float yAngle = randomYRotation ? Range(random, 0f, 360f) : 0f;

        if (!alignToTerrainNormal)
        {
            return Quaternion.Euler(0f, yAngle, 0f);
        }

        float maxRadians = maxTiltDegrees * Mathf.Deg2Rad;
        Vector3 clampedUp = Vector3.RotateTowards(Vector3.up, terrainNormal.normalized, maxRadians, 0f).normalized;
        Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, clampedUp);
        return Quaternion.AngleAxis(yAngle, clampedUp) * slopeRotation;
    }

    private bool PassesDensity(Vector3 point, System.Random random, Terrain terrain, PathInfo pathInfo)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        Vector3 terrainCenter = terrainPosition + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        float edgeX = Mathf.Abs(point.x - terrainCenter.x) / Mathf.Max(0.01f, size.x * 0.5f);
        float edgeZ = Mathf.Abs(point.z - terrainCenter.z) / Mathf.Max(0.01f, size.z * 0.5f);
        float edge = Mathf.Clamp01(Mathf.Max(edgeX, edgeZ));
        float density = Mathf.Lerp(0.38f, 1f, Smooth01(edge));

        if (pathInfo.HasPath)
        {
            float pathDistance = DistancePointToSegmentXZ(point, pathInfo.Start, pathInfo.End);
            float pathFade = Smooth01(Mathf.InverseLerp(pathClearWidth, pathClearWidth * 2.5f + 0.01f, pathDistance));
            density *= Mathf.Lerp(0.42f, 1f, pathFade);
        }

        return random.NextDouble() <= density;
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
            Start = terrainCenter + Vector3.back * radius * 0.55f,
            End = terrainCenter + Vector3.forward * radius * 0.95f
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

        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        foreach (Transform transform in transforms)
        {
            string objectName = transform.name.ToLowerInvariant();
            if (objectName.Contains("vault")
                || objectName.Contains("entrance")
                || objectName.Contains("base entrance")
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
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);

        foreach (Transform transform in transforms)
        {
            string objectName = transform.name.ToLowerInvariant();
            if (objectName.Contains("vault")
                || objectName.Contains("entrance")
                || objectName.Contains("base entrance")
                || objectName.Contains("door"))
            {
                points.Add(transform.position);
            }
        }

        return points;
    }

    private bool IsInsideVaultAvoidance(Vector3 point, List<Vector3> vaultAvoidancePoints)
    {
        foreach (Vector3 vaultPoint in vaultAvoidancePoints)
        {
            if (DistanceXZ(point, vaultPoint) < vaultClearRadius)
            {
                return true;
            }
        }

        return false;
    }

    private static void DisableColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }

    private static void SetStaticRecursive(GameObject root)
    {
        root.isStatic = true;
        foreach (Transform child in root.transform)
        {
            SetStaticRecursive(child.gameObject);
        }
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

    private static float Range(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private struct GroundHit
    {
        public bool Valid;
        public Vector3 Position;
        public Vector3 Normal;
    }

    private struct PathInfo
    {
        public bool HasPath;
        public Vector3 Start;
        public Vector3 End;
    }
}
