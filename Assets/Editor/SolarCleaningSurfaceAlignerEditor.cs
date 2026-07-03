using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SolarCleaningSurfaceAligner))]
public class SolarCleaningSurfaceAlignerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        if (!GUILayout.Button("Apply Solar Cleaning Alignment", GUILayout.Height(32f)))
        {
            return;
        }

        SolarCleaningSurfaceAligner aligner = (SolarCleaningSurfaceAligner)target;
        Undo.RegisterFullObjectHierarchyUndo(aligner.gameObject, "Apply Solar Cleaning Alignment");
        aligner.ApplyAlignment();
        EditorUtility.SetDirty(aligner);

        if (aligner.dirtOverlay != null)
        {
            EditorUtility.SetDirty(aligner.dirtOverlay);
        }

        if (aligner.cleaningCamera != null)
        {
            EditorUtility.SetDirty(aligner.cleaningCamera);
        }

        if (aligner.cleaningPanelCollider != null)
        {
            EditorUtility.SetDirty(aligner.cleaningPanelCollider);
        }

        SolarPanelCleaningMinigame minigame = aligner.GetComponent<SolarPanelCleaningMinigame>();
        if (minigame != null)
        {
            EditorUtility.SetDirty(minigame);
        }
    }
}
