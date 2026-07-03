using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class HeightmapStampBrushWindow : EditorWindow
{
    private enum StampMode
    {
        Add,
        Subtract,
        BlendToward
    }

    private Terrain targetTerrain;
    private Texture2D heightmapTexture;
    private Texture2D readableHeightmap;
    private Texture2D cachedSourceTexture;
    private float stampSizeMeters = 20f;
    private float strengthMeters = 0.8f;
    private float edgeSoftness = 0.85f;
    private bool invert;
    private bool smoothAfterStamp = true;
    private int smoothIterations = 1;
    private StampMode stampMode = StampMode.Subtract;
    private float blendTowardStrength = 0.5f;
    private bool preview = true;
    private Vector3 lastHitPoint;
    private bool hasValidPreviewHit;

    [MenuItem("Tools/Regolith/Heightmap Stamp Brush")]
    public static void Open()
    {
        GetWindow<HeightmapStampBrushWindow>("Heightmap Stamp Brush");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        RefreshTargetIfNeeded();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Heightmap Stamp Brush", EditorStyles.boldLabel);
        EditorGUILayout.Space(6f);

        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain Target", targetTerrain, typeof(Terrain), true);
        EditorGUI.BeginChangeCheck();
        heightmapTexture = (Texture2D)EditorGUILayout.ObjectField("Heightmap", heightmapTexture, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck())
        {
            readableHeightmap = null;
            cachedSourceTexture = null;
        }

        EditorGUILayout.Space(6f);
        stampSizeMeters = Mathf.Max(0.1f, EditorGUILayout.FloatField("Stamp Size (Meters)", stampSizeMeters));
        strengthMeters = EditorGUILayout.FloatField("Strength (Meters)", strengthMeters);
        edgeSoftness = EditorGUILayout.Slider("Falloff / Edge Softness", edgeSoftness, 0f, 1f);
        invert = EditorGUILayout.Toggle("Invert", invert);
        smoothAfterStamp = EditorGUILayout.Toggle("Smooth After Stamp", smoothAfterStamp);

        using (new EditorGUI.DisabledScope(!smoothAfterStamp))
        {
            smoothIterations = Mathf.Clamp(EditorGUILayout.IntField("Smooth Iterations", smoothIterations), 1, 5);
        }

        stampMode = (StampMode)EditorGUILayout.EnumPopup("Stamp Mode", stampMode);

        using (new EditorGUI.DisabledScope(stampMode != StampMode.BlendToward))
        {
            blendTowardStrength = EditorGUILayout.Slider("Blend Toward Strength", blendTowardStrength, 0f, 1f);
        }

        preview = EditorGUILayout.Toggle("Preview", preview);

        EditorGUILayout.Space(10f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear Selection"))
            {
                targetTerrain = null;
                heightmapTexture = null;
                readableHeightmap = null;
                cachedSourceTexture = null;
                hasValidPreviewHit = false;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Refresh Target"))
            {
                targetTerrain = null;
                RefreshTargetIfNeeded();
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.HelpBox("Scene View: left click stamps. Shift forces subtract. Ctrl toggles add/subtract, or inverts the sampled shape for BlendToward.", MessageType.Info);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (targetTerrain == null || heightmapTexture == null)
        {
            hasValidPreviewHit = false;
            return;
        }

        Event current = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
        bool hitSelectedTerrain = TryRaycastTargetTerrain(ray, out RaycastHit hit);
        hasValidPreviewHit = hitSelectedTerrain;

        if (hitSelectedTerrain)
        {
            lastHitPoint = hit.point;
            if (preview && current.type == EventType.Repaint)
            {
                DrawBrushPreview(lastHitPoint);
            }

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
        {
            if (!hitSelectedTerrain)
            {
                Debug.LogWarning("Heightmap Stamp Brush: mouse raycast did not hit the selected Terrain.");
                return;
            }

            ApplyStamp(hit.point, current.shift, current.control || current.command);
            current.Use();
        }

        if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
        {
            sceneView.Repaint();
        }
    }

    private void RefreshTargetIfNeeded()
    {
        if (targetTerrain != null)
        {
            return;
        }

        targetTerrain = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Terrain>() : null;
        if (targetTerrain == null)
        {
            targetTerrain = Terrain.activeTerrain;
        }

        if (targetTerrain == null)
        {
            targetTerrain = Object.FindAnyObjectByType<Terrain>();
        }
    }

    private bool TryRaycastTargetTerrain(Ray ray, out RaycastHit selectedHit)
    {
        selectedHit = default;
        if (targetTerrain == null)
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
        float nearest = float.PositiveInfinity;
        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            Terrain hitTerrain = hit.collider.GetComponent<Terrain>();
            TerrainCollider terrainCollider = hit.collider as TerrainCollider;
            bool isTargetTerrain = hitTerrain == targetTerrain
                || (terrainCollider != null && terrainCollider.terrainData == targetTerrain.terrainData);

            if (!isTargetTerrain || hit.distance >= nearest)
            {
                continue;
            }

            nearest = hit.distance;
            selectedHit = hit;
            found = true;
        }

        return found;
    }

    private void ApplyStamp(Vector3 worldPosition, bool forceSubtract, bool inverseControl)
    {
        if (targetTerrain == null || targetTerrain.terrainData == null)
        {
            Debug.LogWarning("Heightmap Stamp Brush: no terrain is assigned.");
            return;
        }

        if (heightmapTexture == null)
        {
            Debug.LogWarning("Heightmap Stamp Brush: no heightmap texture is assigned.");
            return;
        }

        Texture2D source = GetReadableHeightmap();
        if (source == null)
        {
            Debug.LogWarning("Heightmap Stamp Brush: assigned heightmap texture could not be made readable.");
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        Vector3 local = targetTerrain.transform.InverseTransformPoint(worldPosition);
        Vector3 terrainSize = terrainData.size;

        if (local.x < 0f || local.z < 0f || local.x > terrainSize.x || local.z > terrainSize.z)
        {
            Debug.LogWarning("Heightmap Stamp Brush: stamp point is outside the selected Terrain bounds.");
            return;
        }

        int resolution = terrainData.heightmapResolution;
        float centerX = local.x / terrainSize.x * (resolution - 1);
        float centerZ = local.z / terrainSize.z * (resolution - 1);
        float radiusMeters = stampSizeMeters * 0.5f;
        float radiusPixelsX = radiusMeters / terrainSize.x * (resolution - 1);
        float radiusPixelsZ = radiusMeters / terrainSize.z * (resolution - 1);

        int minX = Mathf.Clamp(Mathf.FloorToInt(centerX - radiusPixelsX) - 1, 0, resolution - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(centerX + radiusPixelsX) + 1, 0, resolution - 1);
        int minZ = Mathf.Clamp(Mathf.FloorToInt(centerZ - radiusPixelsZ) - 1, 0, resolution - 1);
        int maxZ = Mathf.Clamp(Mathf.CeilToInt(centerZ + radiusPixelsZ) + 1, 0, resolution - 1);
        int width = maxX - minX + 1;
        int height = maxZ - minZ + 1;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        Undo.RegisterCompleteObjectUndo(terrainData, "Heightmap Stamp Brush");
        float[,] heights = terrainData.GetHeights(minX, minZ, width, height);
        StampMode effectiveMode = GetEffectiveMode(forceSubtract, inverseControl);
        bool effectiveInvert = invert || (stampMode == StampMode.BlendToward && inverseControl);
        float normalizedStrength = terrainSize.y > 0f ? strengthMeters / terrainSize.y : 0f;

        for (int z = 0; z < height; z++)
        {
            int fullZ = minZ + z;
            float dzMeters = (fullZ - centerZ) / (resolution - 1) * terrainSize.z;

            for (int x = 0; x < width; x++)
            {
                int fullX = minX + x;
                float dxMeters = (fullX - centerX) / (resolution - 1) * terrainSize.x;
                float distance01 = Mathf.Sqrt(dxMeters * dxMeters + dzMeters * dzMeters) / Mathf.Max(0.001f, radiusMeters);

                if (distance01 > 1f)
                {
                    continue;
                }

                float falloff = EvaluateFalloff(distance01);
                if (falloff <= 0f)
                {
                    continue;
                }

                float sampleU = Mathf.Clamp01((dxMeters / radiusMeters) * 0.5f + 0.5f);
                float sampleV = Mathf.Clamp01((dzMeters / radiusMeters) * 0.5f + 0.5f);
                float sample = source.GetPixelBilinear(sampleU, sampleV).grayscale;
                if (effectiveInvert)
                {
                    sample = 1f - sample;
                }

                float current = heights[z, x];
                switch (effectiveMode)
                {
                    case StampMode.Add:
                        current += sample * normalizedStrength * falloff;
                        break;
                    case StampMode.Subtract:
                        current -= sample * normalizedStrength * falloff;
                        break;
                    case StampMode.BlendToward:
                        current = Mathf.Lerp(current, sample, blendTowardStrength * falloff);
                        break;
                }

                heights[z, x] = Mathf.Clamp01(current);
            }
        }

        if (smoothAfterStamp)
        {
            heights = SmoothStampArea(heights, smoothIterations);
        }

        terrainData.SetHeightsDelayLOD(minX, minZ, heights);
        targetTerrain.ApplyDelayedHeightmapModification();
        terrainData.SyncHeightmap();

        EditorUtility.SetDirty(terrainData);
        EditorSceneManager.MarkSceneDirty(targetTerrain.gameObject.scene);
        SceneView.RepaintAll();

        Debug.Log($"Heightmap Stamp Brush: applied '{heightmapTexture.name}' to Terrain '{targetTerrain.name}' using {effectiveMode}, size {stampSizeMeters:0.##}m, strength {strengthMeters:0.##}m.");
    }

    private StampMode GetEffectiveMode(bool forceSubtract, bool inverseControl)
    {
        if (forceSubtract)
        {
            return StampMode.Subtract;
        }

        if (!inverseControl)
        {
            return stampMode;
        }

        if (stampMode == StampMode.Add)
        {
            return StampMode.Subtract;
        }

        if (stampMode == StampMode.Subtract)
        {
            return StampMode.Add;
        }

        return StampMode.BlendToward;
    }

    private float EvaluateFalloff(float distance01)
    {
        float innerRadius = Mathf.Clamp01(1f - edgeSoftness);
        if (distance01 <= innerRadius)
        {
            return 1f;
        }

        float softRange = Mathf.Max(0.0001f, 1f - innerRadius);
        float edgeT = Mathf.Clamp01((distance01 - innerRadius) / softRange);
        return 1f - Smooth01(edgeT);
    }

    private Texture2D GetReadableHeightmap()
    {
        if (heightmapTexture == null)
        {
            return null;
        }

        if (readableHeightmap != null && cachedSourceTexture == heightmapTexture)
        {
            return readableHeightmap;
        }

        EnsureTextureReadable(heightmapTexture);
        heightmapTexture = ReloadTextureIfAsset(heightmapTexture);

        try
        {
            _ = heightmapTexture.GetPixelBilinear(0.5f, 0.5f);
            readableHeightmap = heightmapTexture;
            cachedSourceTexture = heightmapTexture;
            return readableHeightmap;
        }
        catch (UnityException)
        {
            readableHeightmap = CreateReadableCopy(heightmapTexture);
            cachedSourceTexture = heightmapTexture;
            return readableHeightmap;
        }
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

        if (importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
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

    private static Texture2D CreateReadableCopy(Texture2D source)
    {
        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;

        Graphics.Blit(source, temporary);
        RenderTexture.active = temporary;

        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, true)
        {
            name = source.name + "_ReadableCopy"
        };
        copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        copy.Apply(true, false);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        return copy;
    }

    private static float[,] SmoothStampArea(float[,] source, int iterations)
    {
        int height = source.GetLength(0);
        int width = source.GetLength(1);
        float[,] current = source;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            float[,] next = (float[,])current.Clone();

            for (int z = 1; z < height - 1; z++)
            {
                for (int x = 1; x < width - 1; x++)
                {
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

    private void DrawBrushPreview(Vector3 center)
    {
        if (targetTerrain == null || targetTerrain.terrainData == null)
        {
            return;
        }

        float radius = stampSizeMeters * 0.5f;
        DrawTerrainCircle(center, radius, new Color(1f, 0.92f, 0.25f, 0.95f), 4f);

        float innerRadius = radius * Mathf.Clamp01(1f - edgeSoftness);
        if (innerRadius > 0.05f)
        {
            DrawTerrainCircle(center, innerRadius, new Color(1f, 1f, 1f, 0.68f), 2f);
        }
    }

    private void DrawTerrainCircle(Vector3 center, float radius, Color color, float width)
    {
        const int segments = 80;
        Vector3[] points = new Vector3[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            point.y = SamplePreviewHeight(point) + 0.05f;
            points[i] = point;
        }

        Handles.color = color;
        Handles.DrawAAPolyLine(width, points);
    }

    private float SamplePreviewHeight(Vector3 worldPoint)
    {
        if (targetTerrain == null)
        {
            return worldPoint.y;
        }

        Vector3 local = targetTerrain.transform.InverseTransformPoint(worldPoint);
        Vector3 size = targetTerrain.terrainData.size;
        if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
        {
            return worldPoint.y;
        }

        return targetTerrain.transform.position.y + targetTerrain.SampleHeight(worldPoint);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
