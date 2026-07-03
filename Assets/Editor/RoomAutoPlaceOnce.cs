using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RoomAutoPlaceOnce
{
    private const string EditorPrefsKey = "Regolith_Room2Room3AutoPlaceOnce_20260702";

    static RoomAutoPlaceOnce()
    {
        EditorApplication.delayCall += RunOnce;
    }

    [MenuItem("Tools/Regolith/Run Pending Room 2 Room 3 Auto Placement")]
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
            Debug.LogWarning("Room auto placement skipped during Play Mode. Exit Play Mode and run Tools/Regolith/Run Pending Room 2 Room 3 Auto Placement.");
            return;
        }

        if (EditorPrefs.GetBool(EditorPrefsKey, false))
        {
            return;
        }

        RoomImportPlacementUtility.ImportAndPlaceRooms();
        if (RoomImportPlacementUtility.LastPlacementSucceeded)
        {
            EditorPrefs.SetBool(EditorPrefsKey, true);
            Debug.Log("Room auto placement finished. Room_02 and Room_03 should now be placed with their materials/textures.");
        }
        else
        {
            Debug.LogWarning("Room auto placement did not complete. Run Tools/Regolith/Run Pending Room 2 Room 3 Auto Placement after leaving Play Mode and opening Assets/saved.unity.");
        }
    }
}
