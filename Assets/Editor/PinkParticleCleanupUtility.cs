using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Disables particle renderers whose material is missing or has a broken shader
/// (these draw as pink/magenta squares in URP). Only the renderer is turned off;
/// the particle systems, impact timing, and everything else stay untouched.
/// </summary>
[InitializeOnLoad]
public static class PinkParticleCleanupUtility
{
    private const string MenuPath = "Tools/Regolith/Remove Pink Particles";
    private const string TriggerPath = "Assets/Editor/PinkParticleCleanupTrigger.txt";

    static PinkParticleCleanupUtility()
    {
        EditorApplication.delayCall += RunTriggeredSetup;
    }

    [MenuItem(MenuPath)]
    public static void RemovePinkParticles()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Pink particle cleanup cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        ParticleSystemRenderer[] renderers =
            Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int disabledCount = 0;
        foreach (ParticleSystemRenderer renderer in renderers)
        {
            if (!renderer.enabled || !IsBroken(renderer.sharedMaterial))
            {
                continue;
            }

            Undo.RecordObject(renderer, "Disable pink particle renderer");
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
            disabledCount++;
            Debug.Log("Disabled pink particle renderer on: " + GetPath(renderer.transform), renderer);
        }

        if (disabledCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        Debug.Log("Pink particle cleanup complete. Renderers disabled: " + disabledCount);
    }

    private static bool IsBroken(Material material)
    {
        return material == null
            || material.shader == null
            || material.shader.name == "Hidden/InternalErrorShader";
    }

    private static void RunTriggeredSetup()
    {
        string absoluteTriggerPath = AssetPathToAbsolutePath(TriggerPath);
        if (!File.Exists(absoluteTriggerPath))
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            RemovePinkParticles();
        }
        finally
        {
            File.Delete(absoluteTriggerPath);
            AssetDatabase.Refresh();
        }
    }

    private static string GetPath(Transform target)
    {
        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
