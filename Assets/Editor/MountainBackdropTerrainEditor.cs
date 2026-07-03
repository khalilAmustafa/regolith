using UnityEditor;
using UnityEngine;

public static class MountainBackdropTerrainEditor
{
    [MenuItem("Tools/Regolith/Create Mountain Backdrop Terrain")]
    public static void CreateMountainBackdropTerrain()
    {
        GameObject backdrop = MountainBackdropController.CreateOrUpdateBackdrop();
        Debug.Log($"Mountain backdrop menu command complete: {backdrop.name}");
    }
}
