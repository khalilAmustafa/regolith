using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ThirdPersonCameraDistanceUtility
{
    [MenuItem("Tools/Regolith/Fix Third Person Camera Distance")]
    public static void FixThirdPersonCameraDistance()
    {
        Camera camera = Camera.main ?? Object.FindAnyObjectByType<Camera>();
        if (camera == null)
        {
            Debug.LogError("Third-person camera distance fix failed: no Camera found.");
            return;
        }

        ThirdPersonCameraFollow follow = camera.GetComponent<ThirdPersonCameraFollow>();
        if (follow == null)
        {
            follow = camera.gameObject.AddComponent<ThirdPersonCameraFollow>();
        }

        Undo.RecordObject(follow, "Fix third-person camera distance");

        SetPrivateFloat(follow, "distance", 8f);
        SetPrivateFloat(follow, "minDistance", 4.5f);
        SetPrivateFloat(follow, "maxDistance", 12f);
        SetPrivateFloat(follow, "height", 2.2f);
        SetPrivateFloat(follow, "comfortStartDistance", 8f);
        SetPrivateBool(follow, "enforceComfortDistanceOnStart", true);

        EditorUtility.SetDirty(follow);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("Third-person camera distance fixed: distance 8, min 4.5, max 12, height 2.2.");
    }

    private static void SetPrivateFloat(ThirdPersonCameraFollow follow, string fieldName, float value)
    {
        FieldInfo field = typeof(ThirdPersonCameraFollow).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(follow, value);
    }

    private static void SetPrivateBool(ThirdPersonCameraFollow follow, string fieldName, bool value)
    {
        FieldInfo field = typeof(ThirdPersonCameraFollow).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(follow, value);
    }
}
