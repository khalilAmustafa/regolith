using System.Linq;
using UnityEditor;
using UnityEngine;

// PHASE 1 - "measure first". This tool:
//   1. Sets the moving clips (Walking, Run, Standard Run, Forward Jump) to Humanoid +
//      Create-From-This-Model + Root Transform Position XZ "Bake Into Pose" OFF, so their
//      root translation is actually exposed (otherwise baked clips read ~0 m/s).
//   2. Measures and prints the exact root-motion numbers for every locomotion clip using
//      Unity's computed clip metadata (AnimationClip.averageSpeed / averageAngularSpeed /
//      length). These are read from the imported clips - not guessed.
//
// It does NOT enable root motion, change the Animator graph, or touch the controller.
public static class RootMotionMeasure
{
    private const string Folder = "Assets/_External/Animations/Mixamo";

    private static readonly string[] MovingClips =
    {
        "Walking", "Run", "Standard Run", "Forward Jump"
    };

    private static readonly string[] StationaryClips =
    {
        "Idle", "Jumping_hop", "Falling Idle"
    };

    private static readonly string[] TurnClips =
    {
        "Left Turn", "Right Turn", "Walking Turn 180"
    };

    private static readonly string[] OtherClips =
    {
        "Stop Walking", "Falling To Landing"
    };

    [MenuItem("Tools/Regolith/Phase 1 - Prepare And Measure Root Motion")]
    public static void PrepareAndMeasure()
    {
        // 1. Expose root XZ on the moving clips so their speed can be measured.
        foreach (string name in MovingClips)
        {
            PrepareMovingClip($"{Folder}/{name}.fbx");
        }

        // 2. Measure everything.
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        report.AppendLine("=== Regolith Phase 1 root-motion measurement ===");
        report.AppendLine("(speed = horizontal root displacement / clip length)\n");

        report.AppendLine("-- Moving clips (XZ Bake set OFF) --");
        foreach (string name in MovingClips)
        {
            report.AppendLine(MeasureLine(name));
        }

        report.AppendLine("\n-- Stationary clips (should have near-zero XZ drift) --");
        foreach (string name in StationaryClips)
        {
            report.AppendLine(MeasureLine(name));
        }

        report.AppendLine("\n-- Turn clips (root rotation) --");
        foreach (string name in TurnClips)
        {
            report.AppendLine(MeasureLine(name));
        }

        report.AppendLine("\n-- Stop / landing --");
        foreach (string name in OtherClips)
        {
            report.AppendLine(MeasureLine(name));
        }

        report.AppendLine("\nRun vs Standard Run: pick the one closer to 0.85-1.00 m/s with the");
        report.AppendLine("calmer cadence for the lunar lope (see the two m/s lines above).");

        Debug.Log(report.ToString());
    }

    private static void PrepareMovingClip(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"RootMotionMeasure: no ModelImporter at '{path}'.");
            return;
        }

        bool changed = false;

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            changed = true;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip.lockRootPositionXZ)
            {
                clip.lockRootPositionXZ = false; // Root Transform Position XZ "Bake Into Pose" OFF
                changed = true;
            }
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static string MeasureLine(string name)
    {
        string path = $"{Folder}/{name}.fbx";
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        if (clip == null)
        {
            return $"{name,-18}: CLIP NOT FOUND at {path}";
        }

        float length = clip.length;
        Vector3 avgVel = clip.averageSpeed;                      // m/s, root
        float horizontalSpeed = new Vector3(avgVel.x, 0f, avgVel.z).magnitude;
        float horizontalDisplacement = horizontalSpeed * length; // net XZ meters
        float verticalDisplacement = avgVel.y * length;
        float degPerSec = clip.averageAngularSpeed * Mathf.Rad2Deg;
        float netDegrees = degPerSec * length;

        return $"{name,-18}: len={length:F3}s  speedXZ={horizontalSpeed:F3} m/s  " +
               $"dispXZ={horizontalDisplacement:F3} m  dispY={verticalDisplacement:F3} m  " +
               $"rot={netDegrees:F1} deg ({degPerSec:F1} deg/s)";
    }
}
