using UnityEditor;
using UnityEngine;

// One-time helper for the case where the project is already open in Unity.
// It runs inside that editor after scripts compile, so Unity can create the prefab
// and assign the tunnel material/textures without launching a second editor.
[InitializeOnLoad]
public static class TunnelAutoPlaceOnce
{
    private const string EditorPrefsKey = "Regolith_TunnelAutoPlaceOnce_20260702";

    static TunnelAutoPlaceOnce()
    {
        EditorApplication.delayCall += RunOnce;
    }

    [MenuItem("Tools/Regolith/Run Pending Tunnel Auto Placement")]
    public static void RunOnceFromMenu()
    {
        EditorPrefs.DeleteKey(EditorPrefsKey);
        RunOnce();
    }

    private static void RunOnce()
    {
        EditorApplication.delayCall -= RunOnce;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Tunnel auto placement skipped during Play Mode. Exit Play Mode and run Tools/Regolith/Run Pending Tunnel Auto Placement.");
            return;
        }

        if (EditorPrefs.GetBool(EditorPrefsKey, false))
        {
            return;
        }

        TunnelImportPlacementUtility.ImportAndPlaceTunnelNearAirlock();
        if (TunnelImportPlacementUtility.LastPlacementSucceeded)
        {
            EditorPrefs.SetBool(EditorPrefsKey, true);
            Debug.Log("Tunnel auto placement finished. Tunnel_01 should now be placed near Airlock with its material/textures.");
        }
        else
        {
            Debug.LogWarning("Tunnel auto placement did not complete. Run Tools/Regolith/Run Pending Tunnel Auto Placement after leaving Play Mode and opening Assets/saved.unity.");
        }
    }
}
