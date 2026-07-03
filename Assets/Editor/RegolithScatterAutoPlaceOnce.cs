using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RegolithScatterAutoPlaceOnce
{
    private const string EditorPrefsKey = "Regolith_RubbleScatterAutoPlaceOnce_20260702";

    static RegolithScatterAutoPlaceOnce()
    {
        EditorApplication.delayCall += RunOnce;
    }

    [MenuItem("Tools/Regolith/Run Pending Regolith Scatter Auto Placement")]
    public static void RunOnceFromMenu()
    {
        EditorPrefs.DeleteKey(EditorPrefsKey);
        RunOnce();
    }

    private static void RunOnce()
    {
        EditorApplication.delayCall -= RunOnce;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(EditorPrefsKey, false))
        {
            return;
        }

        RegolithScatterImportPlacementUtility.ImportAndScatterRegolithAlongRoad();
        EditorPrefs.SetBool(EditorPrefsKey, true);
    }
}
