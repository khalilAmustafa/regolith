using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SolarPanelCleaningSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Solar Panel Cleaning Minigame";
    private const string RepairMenuPath = "Tools/Regolith/Repair Solar Cleaning Setup";
    private const string ScriptFolder = "Assets/_Scripts/SolarCleaning";
    private const string MaterialFolder = "Assets/_Materials/SolarCleaning";
    private const string TextureFolder = "Assets/_Textures/SolarCleaning";
    private const string GeneratedFolder = "Assets/_Generated";
    private const string DustTexturePath = TextureFolder + "/Solar_Dirt_Overlay.png";
    private const string FallbackDustTexturePath = GeneratedFolder + "/Solar_Dirt_Overlay.png";
    private const string DirtMaterialPath = MaterialFolder + "/Solar_Dirt_Mat.mat";
    private const string DirtShaderPath = MaterialFolder + "/TransparentDirtMask.shader";
    private const string CleaningToolPrefabPath = "Assets/_Prefabs/Environment/Cleaning_Tool_01.prefab";

    private static readonly string[] DustKeywords =
    {
        "dust",
        "dirt",
        "regolith",
        "grime",
        "overlay",
        "lunar"
    };

    private static readonly string[] SolarNameKeywords =
    {
        "solar",
        "panel",
        "array",
        "لوح",
        "طاق",
        "طاقة",
        "طاقه"
    };

    private static readonly string[] ArabicSolarNameKeywords =
    {
        "\u0644\u0648\u062d",
        "\u0637\u0627\u0642",
        "\u0637\u0627\u0642\u0629",
        "\u0637\u0627\u0642\u0647"
    };

    [MenuItem(MenuPath)]
    public static void SetupSolarPanelCleaningMinigame()
    {
        EnsureFolders();

        GameObject solarPanel = FindLikelySolarPanel();
        if (solarPanel == null)
        {
            Debug.LogWarning("Solar cleaning setup: no likely solar panel object found. Select the solar panel and rerun, or create assignments manually.");
            return;
        }

        Bounds panelBounds = CalculateRendererBounds(solarPanel);
        if (panelBounds.size == Vector3.zero)
        {
            panelBounds = new Bounds(solarPanel.transform.position, new Vector3(5f, 0.4f, 3f));
            Debug.LogWarning("Solar cleaning setup: solar panel has no renderer bounds; using fallback overlay/collider size. Adjust DirtOverlay alignment and CleaningPanelCollider size manually.");
        }

        Texture2D dustTexture = ResolveDustTexture();
        Material dirtMaterial = CreateOrUpdateDirtMaterial(dustTexture);

        GameObject root = GetOrCreateChild(solarPanel.transform, "SolarPanelCleaningRoot");
        Undo.RegisterFullObjectHierarchyUndo(root, "Setup Solar Panel Cleaning");

        SolarPowerState powerState = EnsureComponent<SolarPowerState>(root);
        SolarPanelCleaningMinigame minigame = EnsureComponent<SolarPanelCleaningMinigame>(root);
        SolarPowerHUDAdapter hudAdapter = EnsureComponent<SolarPowerHUDAdapter>(root);
        SolarCleaningSurfaceAligner aligner = EnsureComponent<SolarCleaningSurfaceAligner>(root);

        GameObject dirtOverlay = CreateOrUpdateDirtOverlay(root.transform, panelBounds, dirtMaterial);
        GameObject cleaningCollider = CreateOrUpdateCleaningPanelCollider(root.transform, dirtOverlay.transform);
        Camera cleaningCamera = CreateOrUpdateCleaningCamera(root.transform, panelBounds, dirtOverlay.transform);
        GameObject promptUi = CreateOrUpdatePromptUi(root.transform);
        SolarCleaningUI cleaningUi = CreateOrUpdateCleaningUi(root.transform);
        SolarPanelInteractionTrigger trigger = CreateOrUpdateInteractionTrigger(root.transform, panelBounds);

        Camera mainCamera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        MonoBehaviour playerController = player != null ? player.GetComponent<ThirdPersonPlayerController>() : null;
        MonoBehaviour cameraController = mainCamera != null ? mainCamera.GetComponent<ThirdPersonCameraFollow>() : null;
        PlayerSurvivalResources playerResources = player != null ? player.GetComponent<PlayerSurvivalResources>() : UnityEngine.Object.FindAnyObjectByType<PlayerSurvivalResources>();

        minigame.mainCamera = mainCamera;
        minigame.cleaningCamera = cleaningCamera;
        minigame.playerControllerToDisable = playerController;
        minigame.dirtOverlayRenderer = dirtOverlay.GetComponent<Renderer>();
        minigame.cleaningPanelCollider = cleaningCollider.GetComponent<Collider>();
        minigame.cleaningSurfaceTransform = dirtOverlay.transform;
        minigame.surfaceAligner = aligner;
        minigame.powerState = powerState;
        minigame.cleaningUI = cleaningUi;
        minigame.fallbackPanelSize = aligner.panelSize;

        trigger.minigame = minigame;
        trigger.promptUI = promptUi;
        AssignExistingSolarInteractables(solarPanel, minigame, powerState);

        hudAdapter.powerState = powerState;
        AssignPowerLabel(hudAdapter);
        HookPlayerPowerState(powerState, playerResources);

        aligner.panelSurfaceReference = FindLikelyPanelSurfaceReference(solarPanel);
        aligner.dirtOverlay = dirtOverlay.transform;
        aligner.cleaningCamera = cleaningCamera.transform;
        aligner.cleaningPanelCollider = cleaningCollider.GetComponent<Collider>();
        aligner.panelSize = EstimatePanelSize(panelBounds);
        minigame.fallbackPanelSize = aligner.panelSize;

        if (aligner.panelSurfaceReference != null)
        {
            aligner.ApplyAlignment();
        }
        else
        {
            Renderer overlayRenderer = dirtOverlay.GetComponent<Renderer>();
            if (overlayRenderer != null)
            {
                overlayRenderer.enabled = false;
            }

            Debug.LogWarning("Assign the real solar panel glass/front transform to SolarCleaningSurfaceAligner.panelSurfaceReference, adjust panelSize, then click Apply Solar Cleaning Alignment.");
        }

        EditorUtility.SetDirty(powerState);
        EditorUtility.SetDirty(minigame);
        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(hudAdapter);
        EditorUtility.SetDirty(aligner);
        EditorUtility.SetDirty(root);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        AssetDatabase.SaveAssets();
        PrintSummary(dustTexture, solarPanel, root, minigame, trigger, hudAdapter, playerController, playerResources);
    }

    [MenuItem(RepairMenuPath)]
    public static void RepairSolarCleaningSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Solar cleaning repair cannot run during Play Mode. Exit Play Mode, then run Tools > Regolith > Repair Solar Cleaning Setup.");
            return;
        }

        EnsureFolders();
        EnsureRepairSceneLoaded();

        Renderer panelRenderer = FindLikelySolarPanelRenderer();
        GameObject solarPanel = panelRenderer != null ? panelRenderer.gameObject : FindLikelySolarPanel();
        if (solarPanel == null)
        {
            Debug.LogWarning("Solar cleaning repair: no likely solar panel found. Select the solar panel object and run Tools/Regolith/Repair Solar Cleaning Setup again.");
            return;
        }

        Bounds panelBounds = panelRenderer != null ? panelRenderer.bounds : CalculateRendererBounds(solarPanel);
        if (panelBounds.size == Vector3.zero)
        {
            panelBounds = new Bounds(solarPanel.transform.position, new Vector3(6f, 1f, 4f));
        }

        GameObject root = FindSolarCleaningRoot();
        if (root == null)
        {
            root = GetOrCreateChild(solarPanel.transform, "SolarPanelCleaningRoot");
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Repair Solar Cleaning Setup");

        Texture2D dustTexture = ResolveDustTexture();
        Material dirtMaterial = CreateOrUpdateDirtMaterial(dustTexture);

        SolarPowerState powerState = EnsureComponent<SolarPowerState>(root);
        SolarPanelCleaningMinigame minigame = EnsureComponent<SolarPanelCleaningMinigame>(root);
        SolarCleaningSurfaceAligner aligner = EnsureComponent<SolarCleaningSurfaceAligner>(root);
        SolarCleaningCameraFollower cameraFollower = EnsureComponent<SolarCleaningCameraFollower>(root);
        SolarPowerHUDAdapter hudAdapter = EnsureComponent<SolarPowerHUDAdapter>(root);

        GameObject dirtOverlay = CreateOrUpdateDirtOverlay(root.transform, panelBounds, dirtMaterial);
        dirtOverlay.SetActive(true);
        MeshRenderer dirtRenderer = dirtOverlay.GetComponent<MeshRenderer>();
        if (dirtRenderer != null)
        {
            dirtRenderer.enabled = true;
            dirtRenderer.sharedMaterial = dirtMaterial;
            dirtRenderer.shadowCastingMode = ShadowCastingMode.Off;
            dirtRenderer.receiveShadows = false;
        }

        GameObject cleaningColliderObject = CreateOrUpdateCleaningPanelCollider(root.transform, dirtOverlay.transform);
        Collider cleaningCollider = cleaningColliderObject.GetComponent<Collider>();
        if (cleaningCollider != null)
        {
            cleaningCollider.enabled = true;
        }

        Camera cleaningCamera = CreateOrUpdateCleaningCamera(root.transform, panelBounds, dirtOverlay.transform);
        if (cleaningCamera != null)
        {
            cleaningCamera.enabled = false;
        }

        GameObject cleanerTool = CreateOrUpdateCleanerTool(root.transform);

        GameObject promptUi = CreateOrUpdatePromptUi(root.transform);
        promptUi.SetActive(false);

        SolarCleaningUI cleaningUi = CreateOrUpdateCleaningUi(root.transform);
        if (cleaningUi != null)
        {
            cleaningUi.Hide();
        }

        SolarPanelInteractionTrigger trigger = CreateOrUpdateInteractionTrigger(root.transform, panelBounds);
        trigger.minigame = minigame;
        trigger.promptUI = promptUi;

        Camera mainCamera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        MonoBehaviour playerController = player != null ? player.GetComponent<ThirdPersonPlayerController>() : null;
        MonoBehaviour cameraController = mainCamera != null ? mainCamera.GetComponent<ThirdPersonCameraFollow>() : null;
        PlayerSurvivalResources playerResources = player != null ? player.GetComponent<PlayerSurvivalResources>() : UnityEngine.Object.FindAnyObjectByType<PlayerSurvivalResources>();

        Transform panelReference = CreateOrUpdatePanelFrontReference(panelRenderer, solarPanel, panelBounds);
        Vector2 panelSize = EstimatePanelSizeFromReference(panelBounds, panelReference);

        aligner.panelSurfaceReference = panelReference;
        aligner.dirtOverlay = dirtOverlay.transform;
        aligner.cleaningCamera = cleaningCamera != null ? cleaningCamera.transform : null;
        aligner.cleaningPanelCollider = cleaningCollider;
        aligner.panelSize = panelSize;
        aligner.overlayOffset = Mathf.Max(0.005f, aligner.overlayOffset);
        aligner.cameraDistance = Mathf.Max(0.25f, aligner.cameraDistance);
        aligner.useManualOverlayTransform = true;
        aligner.applyAlignmentOnBeginCleaning = false;
        aligner.applyAlignmentOnRepairMenu = false;

        cameraFollower.cleaningCamera = cleaningCamera;
        cameraFollower.dirtOverlay = dirtOverlay.transform;
        cameraFollower.cleaningPanelCollider = cleaningCollider;
        cameraFollower.cameraDistance = 1.2f;
        cameraFollower.orthographicSize = 1.25f;
        cameraFollower.useOrthographicCamera = true;
        cameraFollower.followSmoothTime = 0.18f;
        cameraFollower.verticalFollowSmoothMultiplier = 2.4f;
        cameraFollower.mouseInfluence = 0.65f;
        cameraFollower.borderPadding = -0.08f;

        minigame.mainCamera = mainCamera;
        minigame.cleaningCamera = cleaningCamera;
        minigame.playerControllerToDisable = playerController;
        minigame.cameraControllerToDisable = cameraController;
        minigame.rootMotionRelayToDisable = player != null ? player.GetComponentInChildren<RootMotionRelay>() : null;
        minigame.playerRigidbody = player != null ? player.GetComponent<Rigidbody>() : null;
        minigame.playerCharacterController = player != null ? player.GetComponent<CharacterController>() : null;
        minigame.playerAnimator = player != null ? player.GetComponentInChildren<Animator>() : null;
        minigame.dirtOverlayRenderer = dirtRenderer;
        minigame.cleaningPanelCollider = cleaningCollider;
        minigame.cleaningSurfaceTransform = dirtOverlay.transform;
        minigame.surfaceAligner = aligner;
        minigame.cameraFollower = cameraFollower;
        minigame.powerState = powerState;
        minigame.cleaningUI = cleaningUi;
        minigame.cleaningCanvas = cleaningUi != null && cleaningUi.root != null ? cleaningUi.root.GetComponent<Canvas>() : null;
        minigame.cleanerToolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CleaningToolPrefabPath);
        minigame.cleanerTool = cleanerTool != null ? cleanerTool.transform : null;
        minigame.use3DToolCleaner = cleanerTool != null || minigame.cleanerToolPrefab != null;
        minigame.cleanerToolVisibleScale = Vector3.one * 24f;
        minigame.cleanerToolSurfaceOffset = 0.08f;
        minigame.cleanerToolFollowSmoothTime = 0.12f;
        minigame.cleanerToolLocalEulerOffset = new Vector3(25f, 0f, -20f);
        minigame.cleanerToolMotionTilt = 6f;
        minigame.cleanerToolRotationSmoothTime = 0.08f;
        minigame.cleanerToolFaceMovementDirection = true;
        minigame.cleanerToolInvertMovementDirection = true;
        minigame.requireMouseButton = true;
        minigame.forceClickToClean = true;
        minigame.useCameraFollower = true;
        minigame.autoApplyAlignmentOnBegin = false;
        minigame.autoPositionCameraOnBegin = false;
        minigame.fallbackPanelSize = panelSize;

        hudAdapter.powerState = powerState;
        AssignPowerLabel(hudAdapter);
        HookPlayerPowerState(powerState, playerResources);
        AssignExistingSolarInteractables(solarPanel, minigame, powerState);

        if (aligner.panelSurfaceReference != null && aligner.applyAlignmentOnRepairMenu)
        {
            aligner.ApplyAlignment();
            Debug.Log("Panel reference assigned. If camera is behind panel, toggle flipNormal on SolarCleaningSurfaceAligner and click Apply Solar Cleaning Alignment.");
        }
        else if (aligner.panelSurfaceReference != null)
        {
            Debug.Log("Panel reference assigned. Repair menu preserved manual DirtOverlay transform; click Apply Solar Cleaning Alignment only if you want automatic placement.");
        }

        if (cleaningCamera != null)
        {
            cleaningCamera.enabled = false;
        }

        if (cleaningUi != null)
        {
            cleaningUi.Hide();
        }

        if (cleanerTool != null)
        {
            cleanerTool.SetActive(false);
        }

        promptUi.SetActive(false);

        EditorUtility.SetDirty(powerState);
        EditorUtility.SetDirty(minigame);
        EditorUtility.SetDirty(aligner);
        EditorUtility.SetDirty(cameraFollower);
        EditorUtility.SetDirty(hudAdapter);
        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(root);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        AssetDatabase.SaveAssets();

        PrintRepairSummary(root, aligner, dirtOverlay, dirtRenderer, cleaningCamera, cleaningCollider, minigame);
    }

    [MenuItem(RepairMenuPath, true)]
    private static bool ValidateRepairSolarCleaningSetup()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static Scene EnsureRepairSceneLoaded()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        string buildScenePath = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .FirstOrDefault(path => !string.IsNullOrEmpty(path));

        if (Application.isBatchMode && !string.IsNullOrEmpty(buildScenePath) && activeScene.path != buildScenePath)
        {
            return EditorSceneManager.OpenScene(buildScenePath, OpenSceneMode.Single);
        }

        if ((!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path)) && !string.IsNullOrEmpty(buildScenePath))
        {
            return EditorSceneManager.OpenScene(buildScenePath, OpenSceneMode.Single);
        }

        return activeScene;
    }

    private static void EnsureFolders()
    {
        EnsureAssetFolder(ScriptFolder);
        EnsureAssetFolder(MaterialFolder);
        EnsureAssetFolder(TextureFolder);
        EnsureAssetFolder(GeneratedFolder);
        EnsureAssetFolder("Assets/Editor");
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

    private static GameObject FindLikelySolarPanel()
    {
        if (Selection.activeGameObject != null && IsLikelySolarObject(Selection.activeGameObject))
        {
            return Selection.activeGameObject;
        }

        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        return allObjects
            .Where(IsLikelySolarObject)
            .Where(candidate => candidate.GetComponentInChildren<Renderer>() != null)
            .OrderBy(candidate => candidate.transform.parent == null ? 0 : 1)
            .ThenByDescending(candidate => CalculateRendererBounds(candidate).size.magnitude)
            .FirstOrDefault();
    }

    private static GameObject FindSolarCleaningRoot()
    {
        GameObject named = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
            .FirstOrDefault(candidate => candidate.name == "SolarPanelCleaningRoot");
        if (named != null)
        {
            return named;
        }

        SolarPanelCleaningMinigame minigame = UnityEngine.Object.FindObjectsByType<SolarPanelCleaningMinigame>(FindObjectsInactive.Include)
            .FirstOrDefault();
        return minigame != null ? minigame.gameObject : null;
    }

    private static Renderer FindLikelySolarPanelRenderer()
    {
        Renderer selectedRenderer = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInChildren<Renderer>() : null;
        if (selectedRenderer != null && IsLikelySolarObject(Selection.activeGameObject))
        {
            return selectedRenderer;
        }

        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        return renderers
            .Where(renderer => renderer != null && IsLikelySolarRenderer(renderer))
            .OrderByDescending(renderer => renderer.bounds.size.x * renderer.bounds.size.y * renderer.bounds.size.z)
            .ThenByDescending(renderer => renderer.bounds.size.magnitude)
            .FirstOrDefault();
    }

    private static bool IsLikelySolarRenderer(Renderer renderer)
    {
        Transform current = renderer.transform;
        while (current != null)
        {
            if (IsLikelySolarObject(current.gameObject))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsLikelySolarObject(GameObject candidate)
    {
        string name = candidate.name.ToLowerInvariant();
        foreach (string keyword in SolarNameKeywords)
        {
            if (name.Contains(keyword))
            {
                return true;
            }
        }

        foreach (string keyword in ArabicSolarNameKeywords)
        {
            if (name.Contains(keyword))
            {
                return true;
            }
        }

        return false;
    }

    private static Texture2D ResolveDustTexture()
    {
        if (File.Exists(AssetPathToAbsolutePath(DustTexturePath)))
        {
            bool existingLooksCheckerboard = LooksLikeCheckerboard(AssetPathToAbsolutePath(DustTexturePath));
            if (!existingLooksCheckerboard)
            {
                ConfigureDustTextureImporter(DustTexturePath);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(DustTexturePath);
            }

            Debug.LogWarning("Solar cleaning setup: existing Solar_Dirt_Overlay.png appears to contain a baked checkerboard pattern. Using generated procedural dust texture instead.");
            GenerateFallbackDustTexture();
            ConfigureDustTextureImporter(FallbackDustTexturePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(FallbackDustTexturePath);
        }

        string chosenSource = FindBestDownloadsDustPng(out bool chosenLooksCheckerboard);
        if (!string.IsNullOrEmpty(chosenSource))
        {
            File.Copy(chosenSource, AssetPathToAbsolutePath(DustTexturePath), true);
            AssetDatabase.ImportAsset(DustTexturePath, ImportAssetOptions.ForceUpdate);
            ConfigureDustTextureImporter(DustTexturePath);

            if (chosenLooksCheckerboard)
            {
                Debug.LogWarning("Solar cleaning setup: selected dust PNG appears to contain a baked checkerboard pattern. Using generated procedural dust texture instead.");
                GenerateFallbackDustTexture();
                ConfigureDustTextureImporter(FallbackDustTexturePath);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(FallbackDustTexturePath);
            }

            Debug.Log("Solar cleaning setup: copied dust texture from Downloads to " + DustTexturePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(DustTexturePath);
        }

        GenerateFallbackDustTexture();
        ConfigureDustTextureImporter(FallbackDustTexturePath);
        Debug.LogWarning("Solar cleaning setup: no PNG found in Downloads. Generated fallback procedural dust texture at " + FallbackDustTexturePath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(FallbackDustTexturePath);
    }

    private static string FindBestDownloadsDustPng(out bool chosenLooksCheckerboard)
    {
        chosenLooksCheckerboard = false;

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile))
        {
            return null;
        }

        string downloads = Path.Combine(userProfile, "Downloads");
        if (!Directory.Exists(downloads))
        {
            return null;
        }

        List<DustCandidate> candidates = Directory.EnumerateFiles(downloads, "*.png", SearchOption.TopDirectoryOnly)
            .Select(path => new DustCandidate
            {
                Path = path,
                LastWriteTimeUtc = File.GetLastWriteTimeUtc(path),
                KeywordScore = ScoreDustName(Path.GetFileNameWithoutExtension(path)),
                LooksCheckerboard = LooksLikeCheckerboard(path)
            })
            .OrderByDescending(candidate => candidate.KeywordScore)
            .ThenBy(candidate => candidate.LooksCheckerboard ? 1 : 0)
            .ThenByDescending(candidate => candidate.LastWriteTimeUtc)
            .ToList();

        DustCandidate chosen = candidates.FirstOrDefault();
        if (chosen == null)
        {
            return null;
        }

        DustCandidate nonChecker = candidates.FirstOrDefault(candidate => !candidate.LooksCheckerboard && candidate.KeywordScore == chosen.KeywordScore);
        if (nonChecker != null)
        {
            chosen = nonChecker;
        }

        chosenLooksCheckerboard = chosen.LooksCheckerboard;
        return chosen.Path;
    }

    private static int ScoreDustName(string fileName)
    {
        string lower = fileName.ToLowerInvariant();
        int score = 0;
        foreach (string keyword in DustKeywords)
        {
            if (lower.Contains(keyword))
            {
                score += 10;
            }
        }

        return score;
    }

    private static bool LooksLikeCheckerboard(string path)
    {
        try
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return false;
            }

            int opaqueSamples = 0;
            int checkerPairs = 0;
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    Color a = texture.GetPixelBilinear((x + 0.25f) / 8f, (y + 0.25f) / 8f);
                    Color b = texture.GetPixelBilinear((x + 0.75f) / 8f, (y + 0.75f) / 8f);
                    if (a.a > 0.95f && b.a > 0.95f)
                    {
                        opaqueSamples++;
                    }

                    float lumA = a.grayscale;
                    float lumB = b.grayscale;
                    bool lowSaturation = Mathf.Abs(a.r - a.g) < 0.04f && Mathf.Abs(a.g - a.b) < 0.04f && Mathf.Abs(b.r - b.g) < 0.04f && Mathf.Abs(b.g - b.b) < 0.04f;
                    if (lowSaturation && Mathf.Abs(lumA - lumB) > 0.18f && lumA > 0.45f && lumB > 0.45f)
                    {
                        checkerPairs++;
                    }
                }
            }

            UnityEngine.Object.DestroyImmediate(texture);
            return opaqueSamples > 48 && checkerPairs > 20;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Solar cleaning setup: could not inspect PNG for checkerboard pattern: " + path + "\n" + exception.Message);
            return false;
        }
    }

    private static void GenerateFallbackDustTexture()
    {
        const int size = 1024;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        System.Random random = new System.Random(7301);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)size;
                float ny = y / (float)size;
                float broad = Mathf.PerlinNoise(nx * 5.5f + 7.2f, ny * 5.5f + 1.3f);
                float fine = Mathf.PerlinNoise(nx * 33f + 3.1f, ny * 33f + 9.7f);
                float patch = Mathf.SmoothStep(0.46f, 0.82f, broad) * 0.55f + fine * 0.2f;
                float alpha = Mathf.Clamp01(patch * 0.58f);

                if (random.NextDouble() < 0.012)
                {
                    alpha = Mathf.Clamp01(alpha + 0.22f);
                }

                byte shade = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(125f, 210f, fine)), 0, 255);
                int index = y * size + x;
                pixels[index] = new Color32(shade, shade, shade, (byte)Mathf.RoundToInt(alpha * 210f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(AssetPathToAbsolutePath(FallbackDustTexturePath), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(FallbackDustTexturePath, ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureDustTextureImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("Solar cleaning setup: texture importer not found for " + assetPath);
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static Material CreateOrUpdateDirtMaterial(Texture2D dustTexture)
    {
        AssetDatabase.ImportAsset(DirtShaderPath, ImportAssetOptions.ForceUpdate);
        Shader shader = Shader.Find("Regolith/TransparentDirtMask");
        if (shader == null)
        {
            Debug.LogWarning("Solar cleaning setup: Regolith/TransparentDirtMask shader was not found yet. Material will be created with the Standard shader until Unity recompiles shaders.");
            shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(DirtMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, DirtMaterialPath);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        material.SetTexture("_MainTex", dustTexture);
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", new Color(0.62f, 0.64f, 0.64f, 1f));
        }

        if (material.HasProperty("_Opacity"))
        {
            material.SetFloat("_Opacity", 0.55f);
        }

        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static GameObject CreateOrUpdateDirtOverlay(Transform root, Bounds panelBounds, Material dirtMaterial)
    {
        bool existed = root.Find("DirtOverlay") != null;
        GameObject overlay = GetOrCreateChild(root, "DirtOverlay");
        MeshFilter filter = EnsureComponent<MeshFilter>(overlay);
        MeshRenderer renderer = EnsureComponent<MeshRenderer>(overlay);
        filter.sharedMesh = GetPrimitiveMesh(PrimitiveType.Quad);

        // Manual adjustment point: existing DirtOverlay transforms are preserved by the repair menu.
        overlay.SetActive(true);
        overlay.layer = 0;
        if (!existed)
        {
            overlay.transform.localScale = Vector3.one;
        }
        else
        {
            Debug.Log("Existing DirtOverlay preserved. Manual transform not overwritten.");
        }

        renderer.sharedMaterial = dirtMaterial;
        renderer.enabled = true;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        EditorUtility.SetDirty(filter);
        EditorUtility.SetDirty(renderer);
        return overlay;
    }

    private static GameObject CreateOrUpdateCleaningPanelCollider(Transform root, Transform overlay)
    {
        GameObject colliderObject = GetOrCreateChild(root, "CleaningPanelCollider");
        BoxCollider box = EnsureComponent<BoxCollider>(colliderObject);

        // Manual adjustment point: this collider is positioned/sized by SolarCleaningSurfaceAligner.
        colliderObject.SetActive(true);
        colliderObject.layer = 0;
        colliderObject.transform.localScale = Vector3.one;
        box.enabled = true;
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(6f, 4f, 0.08f);
        EditorUtility.SetDirty(box);
        return colliderObject;
    }

    private static Camera CreateOrUpdateCleaningCamera(Transform root, Bounds panelBounds, Transform overlay)
    {
        GameObject cameraObject = GetOrCreateChild(root, "CleaningCamera");
        Camera camera = EnsureComponent<Camera>(cameraObject);

        // Manual adjustment point: this camera is positioned by SolarCleaningSurfaceAligner.
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 100f;
        camera.fieldOfView = 40f;
        camera.enabled = false;
        EditorUtility.SetDirty(camera);
        return camera;
    }

    private static GameObject CreateOrUpdateCleanerTool(Transform root)
    {
        Transform existing = root.Find("CleaningToolCleaner");
        if (existing == null)
        {
            existing = root.Find("Cleaning_Tool_01");
        }

        GameObject cleaner = existing != null ? existing.gameObject : null;
        if (cleaner == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CleaningToolPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("Solar cleaning repair: cleaning tool prefab not found at " + CleaningToolPrefabPath + ". The 2D brush cursor will be used until the prefab is restored.");
                return null;
            }

            cleaner = PrefabUtility.InstantiatePrefab(prefab, root) as GameObject;
            if (cleaner == null)
            {
                cleaner = UnityEngine.Object.Instantiate(prefab, root);
            }

            Undo.RegisterCreatedObjectUndo(cleaner, "Create Solar Cleaning Tool Cleaner");
            cleaner.name = "CleaningToolCleaner";
            cleaner.transform.localPosition = Vector3.zero;
            cleaner.transform.localRotation = Quaternion.identity;
            cleaner.transform.localScale = Vector3.one;
        }

        cleaner.SetActive(false);
        foreach (Collider collider in cleaner.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            EditorUtility.SetDirty(collider);
        }

        foreach (Renderer renderer in cleaner.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            EditorUtility.SetDirty(renderer);
        }

        EditorUtility.SetDirty(cleaner);
        return cleaner;
    }

    private static SolarPanelInteractionTrigger CreateOrUpdateInteractionTrigger(Transform root, Bounds panelBounds)
    {
        GameObject triggerObject = GetOrCreateChild(root, "SolarCleaningInteractionTrigger");
        BoxCollider triggerCollider = EnsureComponent<BoxCollider>(triggerObject);
        SolarPanelInteractionTrigger trigger = EnsureComponent<SolarPanelInteractionTrigger>(triggerObject);

        triggerObject.transform.position = panelBounds.center;
        triggerObject.transform.rotation = Quaternion.identity;
        triggerObject.transform.localScale = Vector3.one;
        triggerCollider.isTrigger = true;
        triggerCollider.center = Vector3.up * 1.2f;
        triggerCollider.size = new Vector3(Mathf.Max(panelBounds.size.x + 4f, 6f), 2.8f, Mathf.Max(panelBounds.size.z + 4f, 5f));
        EditorUtility.SetDirty(triggerCollider);
        return trigger;
    }

    private static GameObject CreateOrUpdatePromptUi(Transform root)
    {
        GameObject canvasObject = GetOrCreateChild(root, "SolarCleaningPromptCanvas");
        Canvas canvas = EnsureComponent<Canvas>(canvasObject);
        EnsureComponent<CanvasScaler>(canvasObject);
        EnsureComponent<GraphicRaycaster>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        GameObject panel = GetOrCreateRectChild(canvasObject.transform, "PromptPanel", new Vector2(220f, 92f), new Vector2(0f, -110f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        Image image = EnsureComponent<Image>(panel);
        image.color = new Color(0.04f, 0.05f, 0.055f, 0.82f);

        TMP_Text text = CreateOrUpdateTmpText(panel.transform, "PromptText", "Press E\nUse cleaning tool", 24, TextAlignmentOptions.Center);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);

        canvasObject.SetActive(false);
        return canvasObject;
    }

    private static SolarCleaningUI CreateOrUpdateCleaningUi(Transform root)
    {
        GameObject canvasObject = GetOrCreateChild(root, "SolarCleaningUICanvas");
        Canvas canvas = EnsureComponent<Canvas>(canvasObject);
        EnsureComponent<CanvasScaler>(canvasObject);
        EnsureComponent<GraphicRaycaster>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        GameObject progressPanel = GetOrCreateRectChild(canvasObject.transform, "CleaningProgressPanel", new Vector2(280f, 92f), new Vector2(-24f, -24f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        Image progressPanelImage = EnsureComponent<Image>(progressPanel);
        progressPanelImage.color = new Color(0.035f, 0.04f, 0.045f, 0.86f);

        TMP_Text percentLabel = CreateOrUpdateTmpText(progressPanel.transform, "CleaningPercentLabel", "Cleaning 0%", 22, TextAlignmentOptions.Left);
        RectTransform percentRect = percentLabel.GetComponent<RectTransform>();
        percentRect.anchorMin = new Vector2(0f, 1f);
        percentRect.anchorMax = new Vector2(1f, 1f);
        percentRect.pivot = new Vector2(0.5f, 1f);
        percentRect.anchoredPosition = new Vector2(0f, -12f);
        percentRect.sizeDelta = new Vector2(-28f, 30f);

        GameObject barBack = GetOrCreateRectChild(progressPanel.transform, "ProgressBarBack", new Vector2(244f, 14f), new Vector2(0f, -54f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        Image barBackImage = EnsureComponent<Image>(barBack);
        barBackImage.color = new Color(0.15f, 0.17f, 0.18f, 0.95f);

        GameObject barFill = GetOrCreateRectChild(barBack.transform, "ProgressBarFill", new Vector2(244f, 14f), Vector2.zero, Vector2.zero, Vector2.one);
        Image fillImage = EnsureComponent<Image>(barFill);
        fillImage.color = new Color(0.74f, 0.9f, 1f, 0.98f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 0f;

        GameObject instructionPanel = GetOrCreateRectChild(canvasObject.transform, "CleaningInstructionPanel", new Vector2(260f, 78f), new Vector2(-24f, 24f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        Image instructionPanelImage = EnsureComponent<Image>(instructionPanel);
        instructionPanelImage.color = new Color(0.035f, 0.04f, 0.045f, 0.78f);

        TMP_Text instructionLabel = CreateOrUpdateTmpText(instructionPanel.transform, "InstructionLabel", "Hold click to clean", 20, TextAlignmentOptions.Right);
        RectTransform instructionRect = instructionLabel.GetComponent<RectTransform>();
        instructionRect.anchorMin = new Vector2(0f, 1f);
        instructionRect.anchorMax = new Vector2(1f, 1f);
        instructionRect.pivot = new Vector2(0.5f, 1f);
        instructionRect.anchoredPosition = new Vector2(0f, -10f);
        instructionRect.sizeDelta = new Vector2(-24f, 28f);

        TMP_Text exitLabel = CreateOrUpdateTmpText(instructionPanel.transform, "ExitLabel", "ESC exit", 18, TextAlignmentOptions.Right);
        RectTransform exitRect = exitLabel.GetComponent<RectTransform>();
        exitRect.anchorMin = new Vector2(0f, 1f);
        exitRect.anchorMax = new Vector2(1f, 1f);
        exitRect.pivot = new Vector2(0.5f, 1f);
        exitRect.anchoredPosition = new Vector2(0f, -42f);
        exitRect.sizeDelta = new Vector2(-24f, 24f);

        SolarCleaningUI ui = EnsureComponent<SolarCleaningUI>(canvasObject);
        ui.root = canvasObject;
        ui.cleaningPercentLabel = percentLabel;
        ui.progressFill = fillImage;
        ui.instructionLabel = instructionLabel;
        ui.exitLabel = exitLabel;
        ui.SetProgress(0f);
        canvasObject.SetActive(false);
        EditorUtility.SetDirty(ui);
        return ui;
    }

    private static Transform FindLikelyPanelSurfaceReference(GameObject solarPanel)
    {
        string[] surfaceTerms =
        {
            "glass",
            "front",
            "surface",
            "cell",
            "cells"
        };

        Transform[] children = solarPanel.GetComponentsInChildren<Transform>(true);
        return children
            .Where(child => child != solarPanel.transform)
            .Where(child =>
            {
                string lowerName = child.name.ToLowerInvariant();
                return surfaceTerms.Any(term => lowerName.Contains(term));
            })
            .Where(child => child.GetComponent<Renderer>() != null || child.GetComponentInChildren<Renderer>() != null)
            .OrderByDescending(child => CalculateRendererBounds(child.gameObject).size.magnitude)
            .FirstOrDefault();
    }

    private static Transform CreateOrUpdatePanelFrontReference(Renderer panelRenderer, GameObject solarPanel, Bounds panelBounds)
    {
        Transform parent = solarPanel != null ? solarPanel.transform : null;
        if (parent == null && panelRenderer != null)
        {
            parent = panelRenderer.transform;
        }

        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.Find("SolarPanelFrontReference");
        GameObject referenceObject;
        if (existing != null)
        {
            referenceObject = existing.gameObject;
        }
        else
        {
            referenceObject = new GameObject("SolarPanelFrontReference");
            Undo.RegisterCreatedObjectUndo(referenceObject, "Create Solar Panel Front Reference");
            referenceObject.transform.SetParent(parent, true);
        }

        Vector3 forward = panelRenderer != null ? panelRenderer.transform.forward : parent.forward;
        Vector3 up = panelRenderer != null ? panelRenderer.transform.up : parent.up;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        if (up.sqrMagnitude < 0.0001f || Mathf.Abs(Vector3.Dot(forward.normalized, up.normalized)) > 0.96f)
        {
            up = Vector3.up;
        }

        Vector3 viewerDirection = ResolveViewerDirection(panelBounds.center);
        if (viewerDirection.sqrMagnitude > 0.0001f && Vector3.Dot(forward.normalized, viewerDirection.normalized) < 0f)
        {
            forward = -forward;
        }

        referenceObject.transform.SetPositionAndRotation(
            panelBounds.center,
            Quaternion.LookRotation(forward.normalized, up.normalized));

        EditorUtility.SetDirty(referenceObject);
        return referenceObject.transform;
    }

    private static Vector3 ResolveViewerDirection(Vector3 panelCenter)
    {
        Camera camera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (camera != null)
        {
            return camera.transform.position - panelCenter;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            return player.transform.position - panelCenter;
        }

        return Vector3.zero;
    }

    private static Vector2 EstimatePanelSize(Bounds panelBounds)
    {
        float width = Mathf.Max(panelBounds.size.x, panelBounds.size.z, 1f);
        float height = Mathf.Min(
            Mathf.Max(panelBounds.size.x, 0.25f),
            Mathf.Max(panelBounds.size.z, 0.25f));

        if (Mathf.Approximately(width, height))
        {
            height = Mathf.Max(panelBounds.size.y, 1f);
        }

        return new Vector2(Mathf.Max(width, 1f), Mathf.Max(height, 1f));
    }

    private static Vector2 EstimatePanelSizeFromReference(Bounds panelBounds, Transform reference)
    {
        if (reference == null || panelBounds.size == Vector3.zero)
        {
            return EstimatePanelSize(panelBounds);
        }

        Vector3[] corners = GetBoundsCorners(panelBounds);
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (Vector3 corner in corners)
        {
            Vector3 local = reference.InverseTransformPoint(corner);
            minX = Mathf.Min(minX, local.x);
            maxX = Mathf.Max(maxX, local.x);
            minY = Mathf.Min(minY, local.y);
            maxY = Mathf.Max(maxY, local.y);
        }

        return new Vector2(Mathf.Max(maxX - minX, 1f), Mathf.Max(maxY - minY, 1f));
    }

    private static Vector3[] GetBoundsCorners(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        return new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };
    }

    private static void AssignPowerLabel(SolarPowerHUDAdapter adapter)
    {
        GameObject powerTextObject = GameObject.Find("PowerText");
        if (powerTextObject != null)
        {
            adapter.powerLabel = powerTextObject.GetComponent<TMP_Text>();
            adapter.powerFallbackLabel = powerTextObject.GetComponent<Text>();
            return;
        }

        TMP_Text tmp = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)
            .FirstOrDefault(text => text.name.IndexOf("power", StringComparison.OrdinalIgnoreCase) >= 0 || text.text.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0);
        if (tmp != null)
        {
            adapter.powerLabel = tmp;
            return;
        }

        Text fallback = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include)
            .FirstOrDefault(text => text.name.IndexOf("power", StringComparison.OrdinalIgnoreCase) >= 0 || text.text.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0);
        if (fallback != null)
        {
            adapter.powerFallbackLabel = fallback;
        }
    }

    private static void HookPlayerPowerState(SolarPowerState powerState, PlayerSurvivalResources playerResources)
    {
        if (playerResources == null)
        {
            Debug.LogWarning("Solar cleaning setup: no PlayerSurvivalResources found. SolarPowerState.IsOnline is ready, but existing survival power must be hooked manually if needed.");
            return;
        }

        if (!HasPersistentListener(powerState.OnPowerOnline, playerResources, nameof(PlayerSurvivalResources.SetPowerOnline)))
        {
            UnityAction<bool> call = playerResources.SetPowerOnline;
            UnityEventTools.AddBoolPersistentListener(powerState.OnPowerOnline, call, true);
            EditorUtility.SetDirty(powerState);
            Debug.Log("Solar cleaning setup: hooked SolarPowerState.OnPowerOnline to PlayerSurvivalResources.SetPowerOnline(true).");
        }
    }

    private static void AssignExistingSolarInteractables(GameObject solarPanel, SolarPanelCleaningMinigame minigame, SolarPowerState powerState)
    {
        SolarPowerInteractable[] interactables = solarPanel.GetComponentsInChildren<SolarPowerInteractable>(true);
        foreach (SolarPowerInteractable interactable in interactables)
        {
            interactable.cleaningMinigame = minigame;
            interactable.powerState = powerState;
            EditorUtility.SetDirty(interactable);
        }

        if (interactables.Length > 0)
        {
            Debug.Log("Solar cleaning setup: assigned existing SolarPowerInteractable component(s) to start the cleaning minigame instead of restoring power instantly.");
        }
    }

    private static bool HasPersistentListener(UnityEvent unityEvent, UnityEngine.Object target, string methodName)
    {
        for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
        {
            if (unityEvent.GetPersistentTarget(i) == target && unityEvent.GetPersistentMethodName(i) == methodName)
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, "Create " + name);
        created.transform.SetParent(parent, false);
        return created;
    }

    private static GameObject GetOrCreateRectChild(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject child = GetOrCreateChild(parent, name);
        RectTransform rect = child.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = child.AddComponent<RectTransform>();
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return child;
    }

    private static TMP_Text CreateOrUpdateTmpText(Transform parent, string name, string value, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = GetOrCreateRectChild(parent, name, new Vector2(200f, 40f), Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (text == null)
        {
            text = textObject.AddComponent<TextMeshProUGUI>();
        }

        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        component = Undo.AddComponent<T>(target);
        return component;
    }

    private static Mesh GetPrimitiveMesh(PrimitiveType primitiveType)
    {
        GameObject temporary = GameObject.CreatePrimitive(primitiveType);
        Mesh mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(temporary);
        return mesh;
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = default;
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.zero);
    }

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void PrintSummary(
        Texture2D dustTexture,
        GameObject solarPanel,
        GameObject root,
        SolarPanelCleaningMinigame minigame,
        SolarPanelInteractionTrigger trigger,
        SolarPowerHUDAdapter hudAdapter,
        MonoBehaviour playerController,
        PlayerSurvivalResources playerResources)
    {
        string summary =
            "Solar cleaning setup complete.\n" +
            "- Scripts created under Assets/_Scripts/SolarCleaning.\n" +
            "- Shader/material created or updated: " + DirtShaderPath + " / " + DirtMaterialPath + "\n" +
            "- Dust texture in use: " + (dustTexture != null ? AssetDatabase.GetAssetPath(dustTexture) : "none") + "\n" +
            "- Solar panel target: " + solarPanel.name + "\n" +
            "- Scene root created/assigned: " + root.name + "\n" +
            "- Objects created/assigned: DirtOverlay, CleaningPanelCollider, CleaningCamera, Prompt UI, Cleaning UI, interaction trigger, SolarCleaningSurfaceAligner.\n" +
            "- References assigned: " +
            "minigame=" + (minigame != null) +
            ", trigger=" + (trigger != null) +
            ", playerController=" + (playerController != null) +
            ", powerState=" + (minigame != null && minigame.powerState != null) +
            ", HUDAdapter=" + (hudAdapter != null) + "\n" +
            "- Existing survival power hookup: " + (playerResources != null ? "PlayerSurvivalResources.SetPowerOnline(true)" : "manual hookup may be needed") + "\n" +
            "- Assign the real solar panel glass/front transform to SolarCleaningSurfaceAligner.panelSurfaceReference, adjust panelSize, then click Apply Solar Cleaning Alignment.\n" +
            "- Manual checks: if the camera appears behind the panel, enable flipNormal on SolarCleaningSurfaceAligner and click Apply Solar Cleaning Alignment again.";

        Debug.Log(summary);
    }

    private static void PrintRepairSummary(
        GameObject root,
        SolarCleaningSurfaceAligner aligner,
        GameObject dirtOverlay,
        Renderer dirtRenderer,
        Camera cleaningCamera,
        Collider cleaningCollider,
        SolarPanelCleaningMinigame minigame)
    {
        bool rootReady = root != null;
        bool referenceReady = aligner != null && aligner.panelSurfaceReference != null;
        bool overlayActive = dirtOverlay != null && dirtOverlay.activeInHierarchy;
        bool overlayRendererEnabled = dirtRenderer != null && dirtRenderer.enabled;
        bool cameraReady = cleaningCamera != null;
        bool colliderReady = cleaningCollider != null && cleaningCollider.enabled;
        bool minigameReady = minigame != null
            && minigame.cleaningCamera != null
            && minigame.dirtOverlayRenderer != null
            && minigame.cleaningPanelCollider != null
            && minigame.powerState != null
            && referenceReady
            && overlayActive
            && overlayRendererEnabled;

        List<string> missing = new List<string>();
        if (!rootReady) missing.Add("SolarPanelCleaningRoot");
        if (!referenceReady) missing.Add("SolarCleaningSurfaceAligner.panelSurfaceReference");
        if (!overlayActive) missing.Add("DirtOverlay active GameObject");
        if (!overlayRendererEnabled) missing.Add("DirtOverlay enabled Renderer");
        if (!cameraReady) missing.Add("CleaningCamera");
        if (!colliderReady) missing.Add("CleaningPanelCollider");
        if (minigame == null) missing.Add("SolarPanelCleaningMinigame");
        else
        {
            if (minigame.powerState == null) missing.Add("SolarPowerState reference on minigame");
            if (minigame.cleaningUI == null) missing.Add("SolarCleaningUI reference on minigame");
        }

        string summary =
            "Solar cleaning repair complete:\n" +
            "- SolarPanelCleaningRoot found/created: " + ToYesNo(rootReady) + "\n" +
            "- PanelSurfaceReference assigned: " + ToYesNo(referenceReady) + "\n" +
            "- DirtOverlay active: " + ToYesNo(overlayActive) + "\n" +
            "- DirtOverlay renderer enabled: " + ToYesNo(overlayRendererEnabled) + "\n" +
            "- CleaningCamera assigned: " + ToYesNo(cameraReady) + "\n" +
            "- CleaningPanelCollider assigned: " + ToYesNo(colliderReady) + "\n" +
            "- Minigame ready: " + ToYesNo(minigameReady);

        if (missing.Count > 0)
        {
            summary += "\nMissing: " + string.Join(", ", missing);
        }

        Debug.Log(summary);
    }

    private static string ToYesNo(bool value)
    {
        return value ? "yes" : "no";
    }

    private sealed class DustCandidate
    {
        public string Path;
        public DateTime LastWriteTimeUtc;
        public int KeywordScore;
        public bool LooksCheckerboard;
    }
}
