using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sets up the damaged solar panel visual flow:
/// - Impact shows the ORIGINAL crack (reverts any earlier patch-prefab swap).
/// - An inactive PatchPanelVisual child sits on the impact point, shown only
///   after the player repairs the panel with Regolith Panels.
/// - A ground-level repair trigger zone lets the player perform the repair.
/// Gameplay, cinematic, asteroid, and power systems are not modified.
/// </summary>
[InitializeOnLoad]
public static class PatchPanelVisualSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Solar Panel Repair Flow";
    private const string TriggerPath = "Assets/Editor/PatchPanelSetupTrigger.txt";
    private const string TexturePath = "Assets/_Art/Decals/PatchPanel.png";
    private const string MaterialPath = "Assets/_Materials/M_PatchPanel.mat";
    private const string PatchPrefabPath = "Assets/_Prefabs/PatchPanel.prefab";
    private const string CrackPrefabPath = "Assets/_Prefabs/ImpactCrack.prefab";
    private const string PatchVisualName = "PatchPanelVisual";
    private const string RepairZoneName = "SolarPanelRepairZone";

    // Patch faces the opposite side of the impact quad (flipped 180 on Y), with a
    // slight lift along its visible face so it sits flush without z-fighting.
    // Tweak on the PatchPanelVisual transform if needed.
    private static readonly Vector3 PatchLocalOffset = new Vector3(0f, 0f, 0.02f);
    private static readonly Quaternion PatchLocalRotation = Quaternion.Euler(0f, 180f, 0f);

    static PatchPanelVisualSetupUtility()
    {
        EditorApplication.delayCall += RunTriggeredSetup;
    }

    [MenuItem(MenuPath)]
    public static void SetupSolarPanelRepairFlow()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Solar panel repair setup cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        ConfigureTextureImporter();
        Material material = CreateOrUpdateMaterial();
        GameObject patchPrefab = CreateOrUpdatePatchPrefab(material);

        ImpactCrackController crackController = RestoreOriginalCrack();
        if (crackController == null)
        {
            Debug.LogError("Solar panel repair setup aborted: no ImpactCrackController found in the active scene.");
            return;
        }

        GameObject patchVisual = CreateOrUpdatePatchVisual(crackController, patchPrefab);
        SolarPanelRepairController repairController = WireRepairController(crackController, patchVisual);
        WireRepairZone(crackController, repairController);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log(
            "Solar panel repair flow setup complete:\n" +
            "- Impact now shows the ORIGINAL crack again.\n" +
            "- " + PatchVisualName + " (inactive) parented to " + crackController.gameObject.name + ".\n" +
            "- " + RepairZoneName + " trigger placed near the panel (adjust in Inspector if needed).\n" +
            "- Repair cost: SolarPanelRepairController.requiredRegolithPanelsToRepair.");
    }

    private static void RunTriggeredSetup()
    {
        string absoluteTriggerPath = AssetPathToAbsolutePath(TriggerPath);
        if (!File.Exists(absoluteTriggerPath))
        {
            return;
        }

        // Keep the trigger armed if setup cannot run right now (e.g. entering
        // Play Mode); it will fire on the next domain reload instead.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            SetupSolarPanelRepairFlow();
        }
        finally
        {
            File.Delete(absoluteTriggerPath);
            AssetDatabase.Refresh();
        }
    }

    private static ImpactCrackController RestoreOriginalCrack()
    {
        ImpactCrackController controller = Object.FindAnyObjectByType<ImpactCrackController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            return null;
        }

        GameObject crackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrackPrefabPath);
        if (crackPrefab == null)
        {
            Debug.LogError("Original crack prefab not found at " + CrackPrefabPath + ".");
            return controller;
        }

        Undo.RecordObject(controller, "Restore original crack visual");

        if (controller.crackInstance != null)
        {
            Undo.DestroyObjectImmediate(controller.crackInstance);
            controller.crackInstance = null;
        }

        controller.crackPrefab = crackPrefab;
        controller.positionOffset = Vector3.zero;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static GameObject CreateOrUpdatePatchVisual(ImpactCrackController crackController, GameObject patchPrefab)
    {
        Transform parent = crackController.impactPoint != null
            ? crackController.impactPoint
            : crackController.transform;

        Transform existing = parent.Find(PatchVisualName);
        GameObject patchVisual;
        if (existing != null)
        {
            patchVisual = existing.gameObject;
        }
        else
        {
            patchVisual = (GameObject)PrefabUtility.InstantiatePrefab(patchPrefab, parent);
            patchVisual.name = PatchVisualName;
            Undo.RegisterCreatedObjectUndo(patchVisual, "Create patch panel visual");
        }

        patchVisual.transform.localPosition = PatchLocalOffset;
        patchVisual.transform.localRotation = PatchLocalRotation;
        patchVisual.transform.localScale = crackController.crackScale;
        patchVisual.SetActive(false);
        EditorUtility.SetDirty(patchVisual);
        return patchVisual;
    }

    private static SolarPanelRepairController WireRepairController(
        ImpactCrackController crackController,
        GameObject patchVisual)
    {
        GameObject host = crackController.gameObject;
        SolarPanelRepairController repairController = host.GetComponent<SolarPanelRepairController>();
        if (repairController == null)
        {
            repairController = Undo.AddComponent<SolarPanelRepairController>(host);
        }
        else
        {
            Undo.RecordObject(repairController, "Configure solar panel repair controller");
        }

        repairController.impactCrackController = crackController;
        repairController.crackVisual = null; // use the crack spawned by the controller
        repairController.patchPanelVisual = patchVisual;
        if (repairController.requiredRegolithPanelsToRepair <= 0)
        {
            repairController.requiredRegolithPanelsToRepair = 1;
        }

        EditorUtility.SetDirty(repairController);
        return repairController;
    }

    private static void WireRepairZone(ImpactCrackController crackController, SolarPanelRepairController repairController)
    {
        GameObject zone = GameObject.Find(RepairZoneName);
        if (zone == null)
        {
            zone = new GameObject(RepairZoneName);
            Undo.RegisterCreatedObjectUndo(zone, "Create solar panel repair zone");

            Vector3 impactPosition = crackController.impactPoint != null
                ? crackController.impactPoint.position
                : crackController.transform.position;

            // Drop the zone to walkable height below the impact point so the
            // player can reach it. Position is freely adjustable in the Inspector.
            float groundY = 0f;
            if (Physics.Raycast(impactPosition, Vector3.down, out RaycastHit hit, 100f))
            {
                groundY = hit.point.y;
            }

            zone.transform.position = new Vector3(impactPosition.x, groundY, impactPosition.z);
        }

        BoxCollider zoneCollider = zone.GetComponent<BoxCollider>();
        if (zoneCollider == null)
        {
            zoneCollider = Undo.AddComponent<BoxCollider>(zone);
        }

        zoneCollider.isTrigger = true;
        zoneCollider.size = new Vector3(3.5f, 2.5f, 3.5f);
        zoneCollider.center = new Vector3(0f, 1.25f, 0f);

        SolarPanelRepairInteractable interactable = zone.GetComponent<SolarPanelRepairInteractable>();
        if (interactable == null)
        {
            interactable = Undo.AddComponent<SolarPanelRepairInteractable>(zone);
        }
        else
        {
            Undo.RecordObject(interactable, "Configure solar panel repair interactable");
        }

        interactable.repairController = repairController;
        EditorUtility.SetDirty(zone);
    }

    private static void ConfigureTextureImporter()
    {
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("Patch panel texture was not found at " + TexturePath + ".");
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static Material CreateOrUpdateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture != null)
        {
            SetTexture(material, "_BaseMap", "_MainTex", texture);
        }

        SetColor(material, "_BaseColor", "_Color", Color.white);

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.2f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.35f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        }

        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateOrUpdatePatchPrefab(Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = PatchVisualName;

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        quad.SetActive(false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(quad, PatchPrefabPath);
        Object.DestroyImmediate(quad);
        return prefab;
    }

    private static void SetTexture(Material material, string primaryProperty, string fallbackProperty, Texture texture)
    {
        if (material.HasProperty(primaryProperty))
        {
            material.SetTexture(primaryProperty, texture);
        }
        else if (material.HasProperty(fallbackProperty))
        {
            material.SetTexture(fallbackProperty, texture);
        }
    }

    private static void SetColor(Material material, string primaryProperty, string fallbackProperty, Color color)
    {
        if (material.HasProperty(primaryProperty))
        {
            material.SetColor(primaryProperty, color);
        }
        else if (material.HasProperty(fallbackProperty))
        {
            material.SetColor(fallbackProperty, color);
        }
    }

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
