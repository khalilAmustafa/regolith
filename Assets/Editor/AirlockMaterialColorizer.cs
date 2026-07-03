using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AirlockMaterialColorizer
{
    private const string MaterialFolder = "Assets/_Materials/Airlock";

    [MenuItem("Tools/Regolith/Color Selected Airlock")]
    public static void ColorSelectedAirlock()
    {
        GameObject root = ResolveAirlockRoot();
        if (root == null)
        {
            Debug.LogWarning("Airlock colorizer: select an airlock parent, or add a scene object named 'door1' or 'lock'.");
            return;
        }

        AirlockMaterials materials = CreateOrUpdateMaterials();
        Renderer[] renderers = GetAirlockRenderers(root);
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"Airlock colorizer: no MeshRenderer or SkinnedMeshRenderer children found under '{root.name}'.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Color Selected Airlock");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (Renderer renderer in renderers)
        {
            Material material = PickMaterial(renderer.gameObject.name, materials);
            Undo.RecordObject(renderer, "Assign Airlock Material");
            renderer.sharedMaterials = FillMaterialSlots(renderer.sharedMaterials, material);
            EditorUtility.SetDirty(renderer);
            Debug.Log($"Airlock colorizer: {GetTransformPath(renderer.transform, root.transform)} -> {material.name}");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Airlock colorizer: assigned materials to {renderers.Length} renderer(s) under '{root.name}'.");
    }

    [MenuItem("Tools/Regolith/Select Airlock Mesh Renderers")]
    public static void SelectAirlockMeshRenderers()
    {
        GameObject root = ResolveAirlockRoot();
        if (root == null)
        {
            Debug.LogWarning("Airlock renderer selection: select an airlock parent, or add a scene object named 'door1' or 'lock'.");
            return;
        }

        Renderer[] renderers = GetAirlockRenderers(root);
        Selection.objects = renderers.Select(renderer => renderer.gameObject).Cast<Object>().ToArray();
        EditorGUIUtility.PingObject(root);
        Debug.Log($"Airlock renderer selection: selected {renderers.Length} renderer object(s) under '{root.name}'.");
    }

    private static GameObject ResolveAirlockRoot()
    {
        if (Selection.activeGameObject != null)
        {
            return Selection.activeGameObject;
        }

        GameObject door = GameObject.Find("door1");
        if (door != null)
        {
            return door;
        }

        return GameObject.Find("lock");
    }

    private static Renderer[] GetAirlockRenderers(GameObject root)
    {
        List<Renderer> renderers = new List<Renderer>();
        renderers.AddRange(root.GetComponentsInChildren<MeshRenderer>(true));
        renderers.AddRange(root.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        return renderers
            .Where(renderer => renderer != null)
            .OrderBy(renderer => GetTransformPath(renderer.transform, root.transform))
            .ToArray();
    }

    private static Material PickMaterial(string objectName, AirlockMaterials materials)
    {
        string lower = objectName.ToLowerInvariant();

        if (ContainsAny(lower, "lamp", "light", "orange", "warning"))
        {
            return materials.OrangeLight;
        }

        if (ContainsAny(lower, "indicator", "button", "green", "screen"))
        {
            return materials.GreenIndicator;
        }

        if (ContainsAny(lower, "hydrolic", "hydraulic", "cylinder", "rod", "piston"))
        {
            return materials.HydraulicMetal;
        }

        if (ContainsAny(lower, "pipe", "cable", "hose", "rubber"))
        {
            return materials.BlackRubber;
        }

        if (ContainsAny(lower, "ring", "frame", "wall", "semi_wall", "upper", "lower", "support"))
        {
            return materials.OuterFrame;
        }

        if (ContainsAny(lower, "door", "left_door", "right_door", "center", "lock"))
        {
            return materials.MainPanel;
        }

        return materials.DarkFrame;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        foreach (string term in terms)
        {
            if (value.Contains(term))
            {
                return true;
            }
        }

        return false;
    }

    private static Material[] FillMaterialSlots(Material[] existingSlots, Material material)
    {
        int slotCount = Mathf.Max(1, existingSlots != null ? existingSlots.Length : 0);
        Material[] slots = new Material[slotCount];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = material;
        }

        return slots;
    }

    private static AirlockMaterials CreateOrUpdateMaterials()
    {
        EnsureMaterialFolder();

        return new AirlockMaterials
        {
            MainPanel = CreateOrUpdateMaterial("M_Airlock_MainPanel", "#6B6A64", 0.15f, 0.22f),
            DarkFrame = CreateOrUpdateMaterial("M_Airlock_DarkFrame", "#2F3030", 0.45f, 0.28f),
            OuterFrame = CreateOrUpdateMaterial("M_Airlock_OuterFrame", "#454641", 0.35f, 0.25f),
            HydraulicMetal = CreateOrUpdateMaterial("M_Airlock_HydraulicMetal", "#555753", 0.65f, 0.4f),
            BlackRubber = CreateOrUpdateMaterial("M_Airlock_BlackRubber", "#101010", 0f, 0.12f),
            OrangeLight = CreateOrUpdateMaterial("M_Airlock_OrangeLight", "#FF8A1F", 0f, 0.18f, "#FF8A1F", 2f),
            GreenIndicator = CreateOrUpdateMaterial("M_Airlock_GreenIndicator", "#44FF88", 0f, 0.18f, "#44FF88", 1.5f),
            DustyEdge = CreateOrUpdateMaterial("M_Airlock_DustyEdge", "#5A5852", 0f, 0.06f)
        };
    }

    private static Material CreateOrUpdateMaterial(
        string materialName,
        string baseColorHex,
        float metallic,
        float smoothness,
        string emissionHex = null,
        float emissionIntensity = 1f)
    {
        string path = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(FindLitShader());
            AssetDatabase.CreateAsset(material, path);
        }

        Color baseColor = ParseHtmlColor(baseColorHex);
        SetMaterialColor(material, baseColor);
        SetMaterialFloat(material, "_Metallic", metallic);
        SetMaterialFloat(material, "_Smoothness", smoothness);
        SetMaterialFloat(material, "_Glossiness", smoothness);

        if (!string.IsNullOrEmpty(emissionHex))
        {
            Color emission = ParseHtmlColor(emissionHex) * Mathf.Max(0f, emissionIntensity);
            material.EnableKeyword("_EMISSION");
            SetMaterialColorProperty(material, "_EmissionColor", emission);
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            SetMaterialColorProperty(material, "_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Shader FindLitShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }

    private static Color ParseHtmlColor(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        SetMaterialColorProperty(material, "_BaseColor", color);
        SetMaterialColorProperty(material, "_Color", color);
    }

    private static void SetMaterialColorProperty(Material material, string property, Color color)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, color);
        }
    }

    private static void SetMaterialFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "_Materials");
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/_Materials", "Airlock");
        }
    }

    private static string GetTransformPath(Transform transform, Transform root)
    {
        if (transform == root)
        {
            return root.name;
        }

        Stack<string> names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            if (current == root)
            {
                break;
            }

            current = current.parent;
        }

        return string.Join("/", names);
    }

    private sealed class AirlockMaterials
    {
        public Material MainPanel;
        public Material DarkFrame;
        public Material OuterFrame;
        public Material HydraulicMetal;
        public Material BlackRubber;
        public Material OrangeLight;
        public Material GreenIndicator;
        public Material DustyEdge;
    }
}
