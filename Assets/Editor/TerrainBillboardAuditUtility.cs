using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TerrainBillboardAuditUtility : EditorWindow
{
    private const int UtilityVersion = 5;
    private const string ReportPath = "Library/TerrainBillboardAuditReport.txt";
    private const string TriggerPath = "Library/TerrainBillboardAudit.trigger";
    private const string ClearBadDetailsTriggerPath = "Library/TerrainBillboardClearBadDetails.trigger";
    private const string EnlargeMainTerrainTriggerPath = "Library/TerrainBillboardEnlargeMainTerrain.trigger";
    private const string FillTerrainHolesTriggerPath = "Library/TerrainBillboardFillTerrainHoles.trigger";
    private const string BadGrassTexturePath = "Assets/_External/moon/moon-surface/textures/Diffuse.jpeg";

    private Terrain targetTerrain;
    private int detailPrototypeIndex;
    private int treePrototypeIndex;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Regolith/Terrain Billboard Audit")]
    public static void Open()
    {
        GetWindow<TerrainBillboardAuditUtility>("Terrain Billboard Audit");
    }

    [MenuItem("Tools/Regolith/Terrain Billboard Audit/Write Active Scene Report")]
    public static void WriteActiveSceneAuditReport()
    {
        string scenePath = GetPrimaryScenePath();
        if (!string.IsNullOrWhiteSpace(scenePath))
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        string report = BuildReport(scenePath);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report);
        Debug.Log(report);
        Debug.Log($"Terrain billboard audit report written to {ReportPath}");
    }

    [InitializeOnLoadMethod]
    private static void RunDeferredAuditIfTriggered()
    {
        EditorApplication.delayCall += () =>
        {
            bool shouldWriteReport = File.Exists(TriggerPath);
            bool shouldClearBadDetails = File.Exists(ClearBadDetailsTriggerPath);
            bool shouldEnlargeMainTerrain = File.Exists(EnlargeMainTerrainTriggerPath);
            bool shouldFillTerrainHoles = File.Exists(FillTerrainHolesTriggerPath);
            if (!shouldWriteReport && !shouldClearBadDetails && !shouldEnlargeMainTerrain && !shouldFillTerrainHoles)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));

            if (shouldWriteReport)
            {
                File.Delete(TriggerPath);
                string report = BuildReport(SceneManager.GetActiveScene().path);
                File.WriteAllText(ReportPath, report);
                Debug.Log(report);
                Debug.Log($"Terrain billboard audit report written to {ReportPath}");
            }

            if (shouldClearBadDetails)
            {
                File.Delete(ClearBadDetailsTriggerPath);
                ClearConfirmedBadGrassBillboardDetails();
                string afterClearReport = BuildReport(SceneManager.GetActiveScene().path);
                File.WriteAllText(ReportPath, afterClearReport);
                Debug.Log(afterClearReport);
                Debug.Log($"Terrain billboard audit report updated after clearing bad details at {ReportPath}");
            }

            if (shouldEnlargeMainTerrain)
            {
                File.Delete(EnlargeMainTerrainTriggerPath);
                EnlargeMainTerrain();
                string afterResizeReport = BuildReport(SceneManager.GetActiveScene().path);
                File.WriteAllText(ReportPath, afterResizeReport);
                Debug.Log(afterResizeReport);
                Debug.Log($"Terrain billboard audit report updated after resizing main terrain at {ReportPath}");
            }

            if (shouldFillTerrainHoles)
            {
                File.Delete(FillTerrainHolesTriggerPath);
                FillKnownTerrainHoles();
                string afterFillReport = BuildReport(SceneManager.GetActiveScene().path);
                File.WriteAllText(ReportPath, afterFillReport);
                Debug.Log(afterFillReport);
                Debug.Log($"Terrain billboard audit report updated after filling terrain holes at {ReportPath}");
            }
        };
    }

    private static void FillKnownTerrainHoles()
    {
        const string scenePath = "Assets/saved.unity";
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        string[] terrainDataPaths =
        {
            "Assets/New Terrain 1.asset",
            "Assets/_Environment/MoonMountainBackdropTerrainData.asset",
            "Assets/_Environment/TerrainData_61f9bb67-a04c-44db-87e3-9930dd8476c1.asset"
        };

        int totalFilledCells = 0;
        foreach (string terrainDataPath in terrainDataPaths)
        {
            totalFilledCells += FillTerrainDataHoles(terrainDataPath);
        }

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Terrain holes: filled {totalFilledCells} hole cells across known lunar TerrainData assets. Heightmaps, terrain layers, and details were not modified.");
    }

    private static int FillTerrainDataHoles(string terrainDataPath)
    {
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
        if (data == null)
        {
            Debug.LogWarning($"Terrain holes: could not load TerrainData at {terrainDataPath}.");
            return 0;
        }

        int resolution = data.holesResolution;
        if (resolution <= 0)
        {
            Debug.Log($"Terrain holes: {terrainDataPath} has no holes texture.");
            return 0;
        }

        bool[,] holes = data.GetHoles(0, 0, resolution, resolution);
        int holeCells = 0;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                if (!holes[y, x])
                {
                    holeCells++;
                    holes[y, x] = true;
                }
            }
        }

        if (holeCells == 0)
        {
            Debug.Log($"Terrain holes: {terrainDataPath} already has no hole cells.");
            return 0;
        }

        Undo.RegisterCompleteObjectUndo(data, "Fill lunar terrain holes");
        data.SetHoles(0, 0, holes);
        EditorUtility.SetDirty(data);
        Debug.Log($"Terrain holes: filled {holeCells} hole cells in {terrainDataPath}.");
        return holeCells;
    }

    private static void EnlargeMainTerrain()
    {
        const string scenePath = "Assets/saved.unity";
        const string terrainDataPath = "Assets/New Terrain 1.asset";
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
        if (data == null)
        {
            Debug.LogWarning($"Terrain resize: could not load TerrainData at {terrainDataPath}.");
            return;
        }

        Terrain terrain = Terrain.activeTerrains.FirstOrDefault(candidate => candidate != null && candidate.name == "Terrain" && candidate.terrainData == data);
        if (terrain == null)
        {
            terrain = FindObjectsByType<Terrain>(FindObjectsInactive.Include)
                .FirstOrDefault(candidate => candidate != null && candidate.name == "Terrain" && candidate.terrainData == data);
        }

        if (terrain == null)
        {
            Debug.LogWarning($"Terrain resize: no Terrain object named 'Terrain' uses {terrainDataPath} in {scenePath}. TerrainData was not resized.");
            return;
        }

        Vector3 oldSize = data.size;
        Vector3 newSize = new Vector3(120f, oldSize.y, 120f);
        if (Mathf.Approximately(oldSize.x, newSize.x) && Mathf.Approximately(oldSize.z, newSize.z))
        {
            Debug.Log($"Terrain resize: '{terrain.name}' already has size {newSize}.");
            return;
        }

        Vector3 oldPosition = terrain.transform.position;
        Vector3 oldCenter = oldPosition + new Vector3(oldSize.x * 0.5f, 0f, oldSize.z * 0.5f);
        Vector3 newPosition = oldCenter - new Vector3(newSize.x * 0.5f, 0f, newSize.z * 0.5f);

        Undo.RegisterCompleteObjectUndo(data, "Enlarge main lunar TerrainData");
        Undo.RegisterCompleteObjectUndo(terrain.transform, "Recenter enlarged lunar Terrain");

        data.size = newSize;
        terrain.transform.position = new Vector3(newPosition.x, oldPosition.y, newPosition.z);
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(terrain);
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        EditorSceneManager.SaveScene(terrain.gameObject.scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Terrain resize: enlarged '{terrain.name}' using {terrainDataPath} from {oldSize} to {newSize}. Position moved from {oldPosition} to {terrain.transform.position} to keep world center fixed.");
    }

    private static void ClearConfirmedBadGrassBillboardDetails()
    {
        int clearedCount = 0;
        clearedCount += ClearKnownBadDetailLayer("Assets/New Terrain 1.asset", 3);
        clearedCount += ClearKnownBadDetailLayer("Assets/_Environment/MoonMountainBackdropTerrainData.asset", 0);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Terrain billboard audit: cleared {clearedCount} painted bad grass/detail instances. Terrain heightmaps and terrain layers were not modified.");
    }

    private static int ClearKnownBadDetailLayer(string terrainDataPath, int prototypeIndex)
    {
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
        if (data == null)
        {
            Debug.LogWarning($"Terrain billboard audit: could not load TerrainData at {terrainDataPath}.");
            return 0;
        }

        if (prototypeIndex < 0 || prototypeIndex >= data.detailPrototypes.Length)
        {
            Debug.LogWarning($"Terrain billboard audit: detail prototype [{prototypeIndex}] is out of range for {terrainDataPath}.");
            return 0;
        }

        DetailPrototype prototype = data.detailPrototypes[prototypeIndex];
        string texturePath = AssetDatabase.GetAssetPath(prototype.prototypeTexture);
        if (prototype.renderMode != DetailRenderMode.GrassBillboard || prototype.usePrototypeMesh || texturePath != BadGrassTexturePath)
        {
            Debug.LogWarning($"Terrain billboard audit: refused to clear {terrainDataPath} detail prototype [{prototypeIndex}] because it no longer matches the confirmed bad grass billboard source.");
            return 0;
        }

        int count = CountDetailInstances(data, prototypeIndex);
        Undo.RegisterCompleteObjectUndo(data, "Clear bad terrain grass billboard details");
        data.SetDetailLayer(0, 0, prototypeIndex, new int[data.detailHeight, data.detailWidth]);
        EditorUtility.SetDirty(data);
        Debug.Log($"Terrain billboard audit: cleared {count} painted instances from {terrainDataPath} detail prototype [{prototypeIndex}] using {texturePath}.");
        return count;
    }

    private void OnEnable()
    {
        SyncSelection();
    }

    private void OnSelectionChange()
    {
        SyncSelection();
        Repaint();
    }

    private void OnGUI()
    {
        SyncSelection();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.LabelField("Terrain Billboard Audit", EditorStyles.boldLabel);
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", targetTerrain, typeof(Terrain), true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Log Active Scene Report"))
            {
                Debug.Log(BuildReport(SceneManager.GetActiveScene().path));
            }

            if (GUILayout.Button("Write Report File"))
            {
                WriteActiveSceneAuditReport();
            }
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.HelpBox("Clearing below only removes painted instances for one selected prototype. It does not touch heightmap data or terrain layers.", MessageType.Warning);

        detailPrototypeIndex = EditorGUILayout.IntField("Detail Prototype Index", detailPrototypeIndex);
        if (GUILayout.Button("Clear Selected Detail Prototype Instances"))
        {
            ClearDetailPrototypeInstances();
        }

        treePrototypeIndex = EditorGUILayout.IntField("Tree Prototype Index", treePrototypeIndex);
        if (GUILayout.Button("Clear Selected Tree Prototype Instances"))
        {
            ClearTreePrototypeInstances();
        }

        EditorGUILayout.EndScrollView();
    }

    private static string BuildReport(string scenePath)
    {
        Terrain[] terrains = Terrain.activeTerrains;
        var sb = new StringBuilder(16 * 1024);

        sb.AppendLine("Terrain Billboard Audit");
        sb.AppendLine($"Scene: {SceneManager.GetActiveScene().name}");
        sb.AppendLine($"Scene path: {SceneManager.GetActiveScene().path}");
        sb.AppendLine($"Primary scene path: {scenePath}");
        sb.AppendLine($"Terrain count: {terrains.Length}");
        sb.AppendLine();

        foreach (Terrain terrain in terrains.OrderBy(terrain => terrain.name, StringComparer.OrdinalIgnoreCase))
        {
            AppendTerrainReport(sb, terrain);
            sb.AppendLine();
        }

        AppendKnownTerrainDataAssetReport(sb);
        AppendRendererMaterialSummary(sb);
        return sb.ToString();
    }

    private static void AppendTerrainReport(StringBuilder sb, Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        string terrainDataPath = data != null ? AssetDatabase.GetAssetPath(data) : "<missing>";

        sb.AppendLine($"Terrain: {GetHierarchyPath(terrain.transform)}");
        sb.AppendLine($"  TerrainData: {terrainDataPath}");
        sb.AppendLine($"  Position: {terrain.transform.position}");

        if (data == null)
        {
            sb.AppendLine("  No TerrainData assigned.");
            return;
        }

        sb.AppendLine($"  Size: {data.size}");
        AppendHolesReport(sb, data, "  ");
        sb.AppendLine($"  Detail resolution: {data.detailWidth} x {data.detailHeight}");
        sb.AppendLine($"  Terrain layers: {data.terrainLayers.Length}");
        for (int i = 0; i < data.terrainLayers.Length; i++)
        {
            TerrainLayer layer = data.terrainLayers[i];
            sb.AppendLine($"    [{i}] {ObjectName(layer)} path={AssetPath(layer)} diffuse={AssetPath(layer != null ? layer.diffuseTexture : null)} normal={AssetPath(layer != null ? layer.normalMapTexture : null)} mask={AssetPath(layer != null ? layer.maskMapTexture : null)}");
        }

        AppendDetailReport(sb, data);
        AppendTreeReport(sb, data);
    }

    private static void AppendKnownTerrainDataAssetReport(StringBuilder sb)
    {
        string[] paths =
        {
            "Assets/New Terrain 1.asset",
            "Assets/_Environment/MoonMountainBackdropTerrainData.asset",
            "Assets/_Environment/TerrainData_61f9bb67-a04c-44db-87e3-9930dd8476c1.asset"
        };

        sb.AppendLine("Known TerrainData asset detail verification:");
        foreach (string path in paths)
        {
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null)
            {
                sb.AppendLine($"  TerrainData: {path} <missing>");
                continue;
            }

            sb.AppendLine($"  TerrainData: {path}");
            sb.AppendLine($"    Size: {data.size}");
            AppendHolesReport(sb, data, "    ");
            sb.AppendLine($"    Detail resolution: {data.detailWidth} x {data.detailHeight}");
            AppendDetailReport(sb, data);
            AppendTreeReport(sb, data);
        }

        sb.AppendLine();
    }

    private static void AppendHolesReport(StringBuilder sb, TerrainData data, string indent)
    {
        int resolution = data.holesResolution;
        if (resolution <= 0)
        {
            sb.AppendLine($"{indent}Holes: resolution=0 holeCells=0");
            return;
        }

        bool[,] holes = data.GetHoles(0, 0, resolution, resolution);
        int holeCells = 0;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                if (!holes[y, x])
                {
                    holeCells++;
                }
            }
        }

        sb.AppendLine($"{indent}Holes: resolution={resolution} holeCells={holeCells}");
    }

    private static void AppendDetailReport(StringBuilder sb, TerrainData data)
    {
        DetailPrototype[] details = data.detailPrototypes;
        sb.AppendLine($"  Detail prototypes: {details.Length}");

        for (int i = 0; i < details.Length; i++)
        {
            DetailPrototype detail = details[i];
            int count = CountDetailInstances(data, i);
            sb.AppendLine($"    [{i}] paintedCount={count} renderMode={detail.renderMode} usePrototypeMesh={detail.usePrototypeMesh}");
            sb.AppendLine($"        prefab={AssetPath(detail.prototype)}");
            sb.AppendLine($"        texture={AssetPath(detail.prototypeTexture)}");
            sb.AppendLine($"        healthyColor={detail.healthyColor} dryColor={detail.dryColor}");
            sb.AppendLine($"        size min=({detail.minWidth:0.###}, {detail.minHeight:0.###}) max=({detail.maxWidth:0.###}, {detail.maxHeight:0.###}) noiseSpread={detail.noiseSpread:0.###}");

            if (detail.prototype != null)
            {
                AppendPrefabRendererSources(sb, detail.prototype, "        ");
            }
        }
    }

    private static void AppendTreeReport(StringBuilder sb, TerrainData data)
    {
        TreePrototype[] trees = data.treePrototypes;
        Dictionary<int, int> treeCounts = data.treeInstances
            .GroupBy(instance => instance.prototypeIndex)
            .ToDictionary(group => group.Key, group => group.Count());

        sb.AppendLine($"  Tree prototypes: {trees.Length}");
        sb.AppendLine($"  Total painted trees: {data.treeInstances.Length}");

        for (int i = 0; i < trees.Length; i++)
        {
            TreePrototype tree = trees[i];
            treeCounts.TryGetValue(i, out int count);
            sb.AppendLine($"    [{i}] paintedCount={count} bendFactor={tree.bendFactor:0.###} prefab={AssetPath(tree.prefab)}");

            if (tree.prefab != null)
            {
                AppendPrefabRendererSources(sb, tree.prefab, "        ");
            }
        }
    }

    private static void AppendPrefabRendererSources(StringBuilder sb, GameObject prefab, string indent)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            sb.AppendLine($"{indent}renderer={GetHierarchyPath(renderer.transform)} type={renderer.GetType().Name}");

            Mesh mesh = null;
            if (renderer.TryGetComponent(out MeshFilter meshFilter))
            {
                mesh = meshFilter.sharedMesh;
            }
            else if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                mesh = skinnedMeshRenderer.sharedMesh;
            }

            sb.AppendLine($"{indent}  mesh={AssetPath(mesh)} meshName={ObjectName(mesh)}");

            foreach (Material material in renderer.sharedMaterials)
            {
                AppendMaterialSources(sb, material, indent + "  ");
            }
        }
    }

    private static void AppendMaterialSources(StringBuilder sb, Material material, string indent)
    {
        if (material == null)
        {
            sb.AppendLine($"{indent}material=<missing>");
            return;
        }

        sb.AppendLine($"{indent}material={AssetPath(material)} shader={(material.shader != null ? material.shader.name : "<missing>")} color={MaterialColor(material)}");

        foreach (string textureName in material.GetTexturePropertyNames())
        {
            Texture texture = material.GetTexture(textureName);
            if (texture != null)
            {
                sb.AppendLine($"{indent}  texture {textureName}={AssetPath(texture)}");
            }
        }
    }

    private static void AppendRendererMaterialSummary(StringBuilder sb)
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        var materialGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                string path = AssetPath(material);
                if (string.IsNullOrEmpty(path) || path == "<none>")
                {
                    continue;
                }

                materialGroups[path] = materialGroups.TryGetValue(path, out int count) ? count + 1 : 1;
            }
        }

        sb.AppendLine("Scene Mesh/Sprite Renderer material usage, top repeated materials:");
        foreach (KeyValuePair<string, int> group in materialGroups.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(20))
        {
            sb.AppendLine($"  count={group.Value} material={group.Key}");
        }
    }

    private void ClearDetailPrototypeInstances()
    {
        if (!ValidateTerrain(out TerrainData data))
        {
            return;
        }

        if (detailPrototypeIndex < 0 || detailPrototypeIndex >= data.detailPrototypes.Length)
        {
            EditorUtility.DisplayDialog("Invalid detail prototype", "The detail prototype index is out of range.", "OK");
            return;
        }

        int count = CountDetailInstances(data, detailPrototypeIndex);
        DetailPrototype prototype = data.detailPrototypes[detailPrototypeIndex];
        string source = !string.IsNullOrEmpty(AssetPath(prototype.prototype)) ? AssetPath(prototype.prototype) : AssetPath(prototype.prototypeTexture);

        if (!EditorUtility.DisplayDialog(
                "Clear painted detail instances?",
                $"This will clear {count} painted detail cells for detail prototype [{detailPrototypeIndex}].\n\nSource: {source}\n\nHeightmap and terrain layers will not be changed.",
                "Clear Only This Detail Prototype",
                "Cancel"))
        {
            return;
        }

        Undo.RegisterCompleteObjectUndo(data, "Clear terrain detail prototype instances");
        int[,] emptyLayer = new int[data.detailHeight, data.detailWidth];
        data.SetDetailLayer(0, 0, detailPrototypeIndex, emptyLayer);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"Cleared {count} painted detail cells for prototype [{detailPrototypeIndex}] on terrain '{targetTerrain.name}'.");
    }

    private void ClearTreePrototypeInstances()
    {
        if (!ValidateTerrain(out TerrainData data))
        {
            return;
        }

        if (treePrototypeIndex < 0 || treePrototypeIndex >= data.treePrototypes.Length)
        {
            EditorUtility.DisplayDialog("Invalid tree prototype", "The tree prototype index is out of range.", "OK");
            return;
        }

        TreePrototype prototype = data.treePrototypes[treePrototypeIndex];
        int count = data.treeInstances.Count(instance => instance.prototypeIndex == treePrototypeIndex);

        if (!EditorUtility.DisplayDialog(
                "Clear painted tree instances?",
                $"This will clear {count} painted tree instances for tree prototype [{treePrototypeIndex}].\n\nSource: {AssetPath(prototype.prefab)}\n\nHeightmap and terrain layers will not be changed.",
                "Clear Only This Tree Prototype",
                "Cancel"))
        {
            return;
        }

        Undo.RegisterCompleteObjectUndo(data, "Clear terrain tree prototype instances");
        data.treeInstances = data.treeInstances.Where(instance => instance.prototypeIndex != treePrototypeIndex).ToArray();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"Cleared {count} painted tree instances for prototype [{treePrototypeIndex}] on terrain '{targetTerrain.name}'.");
    }

    private bool ValidateTerrain(out TerrainData data)
    {
        data = null;
        if (targetTerrain == null)
        {
            EditorUtility.DisplayDialog("No Terrain selected", "Select a Terrain object first.", "OK");
            return false;
        }

        data = targetTerrain.terrainData;
        if (data == null)
        {
            EditorUtility.DisplayDialog("No TerrainData", "The selected Terrain has no TerrainData assigned.", "OK");
            return false;
        }

        return true;
    }

    private void SyncSelection()
    {
        if (targetTerrain != null)
        {
            return;
        }

        if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent(out Terrain selectedTerrain))
        {
            targetTerrain = selectedTerrain;
        }
        else if (Terrain.activeTerrain != null)
        {
            targetTerrain = Terrain.activeTerrain;
        }
    }

    private static int CountDetailInstances(TerrainData data, int layerIndex)
    {
        if (data == null || data.detailWidth <= 0 || data.detailHeight <= 0 || layerIndex < 0 || layerIndex >= data.detailPrototypes.Length)
        {
            return 0;
        }

        int[,] layer = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layerIndex);
        long total = 0;
        for (int y = 0; y < data.detailHeight; y++)
        {
            for (int x = 0; x < data.detailWidth; x++)
            {
                total += layer[y, x];
            }
        }

        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    private static string GetPrimaryScenePath()
    {
        string activePath = SceneManager.GetActiveScene().path;
        if (!string.IsNullOrWhiteSpace(activePath))
        {
            return activePath;
        }

        EditorBuildSettingsScene buildScene = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled);
        return buildScene != null ? buildScene.path : null;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<none>";
        }

        var names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static string AssetPath(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return "<none>";
        }

        string path = AssetDatabase.GetAssetPath(obj);
        return string.IsNullOrWhiteSpace(path) ? $"<scene object: {obj.name}>" : path;
    }

    private static string ObjectName(UnityEngine.Object obj)
    {
        return obj != null ? obj.name : "<none>";
    }

    private static Color MaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }

        return Color.white;
    }
}
