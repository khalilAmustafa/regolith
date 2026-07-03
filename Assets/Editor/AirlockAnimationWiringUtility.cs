using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AirlockAnimationWiringUtility
{
    private const string ScenePath = "Assets/backup.unity";
    private const string FbxPath = "Assets/_Prefabs/_ Airlock/lock.fbx";
    private const string OutputFolder = "Assets/_Animations/Airlock";
    private const string OpenClipPath = OutputFolder + "/Airlock_Open_Combined.anim";
    private const string CloseClipPath = OutputFolder + "/Airlock_Close_Combined.anim";
    private const string ControllerPath = OutputFolder + "/Airlock.controller";

    private static readonly string[] PreferredExactClipNames =
    {
        "Left_Door|Take 001",
        "Right_Door|Take 001",
        "Center_Lock|Take 001",
        "Left_Hydrolic_main|Take 001",
        "Right_hydrolic_main|Take 001",
        "Left_Hydrolic_02|Take 001",
        "Right_Hydrolic_02|Take 001",
        "Left_Lower_Lock|Take 001",
        "Right_Lower_lock|Take 001",
        "Left_upper_Lock|Take 001",
        "Right_upper_lock|Take 001"
    };

    private static readonly string[] FallbackTerms =
    {
        "door",
        "lock",
        "hydrolic",
        "hydraulic",
        "lower",
        "upper",
        "center"
    };

    public static void Run()
    {
        WiringReport report = RunInternal();
        Debug.Log(report.ToLogString());
    }

    private static WiringReport RunInternal()
    {
        WiringReport report = new WiringReport();

        EnsureFolder(OutputFolder);

        AnimationClip[] importedClips = LoadImportedClips(report);
        AnimationClip[] selectedClips = SelectClips(importedClips, report);
        if (selectedClips.Length == 0)
        {
            throw new InvalidOperationException("No suitable lock.fbx animation clips were selected. Aborting airlock animation wiring.");
        }

        AnimationClip openClip = BuildOpenClip(selectedClips, report);
        if (AnimationUtility.GetCurveBindings(openClip).Length == 0 &&
            AnimationUtility.GetObjectReferenceCurveBindings(openClip).Length == 0)
        {
            throw new InvalidOperationException("Merged Airlock_Open_Combined clip has zero bindings. Aborting instead of creating fake animation.");
        }

        AnimationClip closeClip = BuildCloseClip(openClip, report);
        AnimatorController controller = BuildAnimatorController(openClip, closeClip, report);

        WireScene(controller, report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return report;
    }

    private static AnimationClip[] LoadImportedClips(WiringReport report)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        AnimationClip[] clips = assets
            .OfType<AnimationClip>()
            .Where(clip => clip != null)
            .OrderBy(clip => clip.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (AnimationClip clip in clips)
        {
            int floatBindings = AnimationUtility.GetCurveBindings(clip).Length;
            int objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
            report.ImportedClips.Add(new ClipInfo(clip.name, clip.length, floatBindings, objectBindings));
            Debug.Log($"[AirlockAnimationWiring] Imported clip: {clip.name} | length={clip.length:0.###}s | floatBindings={floatBindings} | objectBindings={objectBindings}");
        }

        return clips;
    }

    private static AnimationClip[] SelectClips(AnimationClip[] importedClips, WiringReport report)
    {
        List<AnimationClip> selected = new List<AnimationClip>();

        foreach (string exactName in PreferredExactClipNames)
        {
            AnimationClip clip = importedClips.FirstOrDefault(candidate => string.Equals(candidate.name, exactName, StringComparison.Ordinal));
            if (clip != null && HasAnyBindings(clip))
            {
                selected.Add(clip);
            }
            else
            {
                report.Warnings.Add($"Preferred clip missing or has no bindings: {exactName}");
            }
        }

        if (selected.Count == 0)
        {
            foreach (AnimationClip clip in importedClips)
            {
                string lowerName = clip.name.ToLowerInvariant();
                bool preferredTake = lowerName.EndsWith("|take 001", StringComparison.Ordinal);
                bool matchesTerm = FallbackTerms.Any(term => lowerName.Contains(term));

                if (preferredTake && matchesTerm && HasAnyBindings(clip))
                {
                    selected.Add(clip);
                }
            }
        }

        report.SelectedClips.AddRange(selected.Select(clip => clip.name));
        return selected.ToArray();
    }

    private static bool HasAnyBindings(AnimationClip clip)
    {
        return AnimationUtility.GetCurveBindings(clip).Length > 0 ||
               AnimationUtility.GetObjectReferenceCurveBindings(clip).Length > 0;
    }

    private static AnimationClip BuildOpenClip(AnimationClip[] selectedClips, WiringReport report)
    {
        AnimationClip openClip = new AnimationClip
        {
            name = "Airlock_Open_Combined",
            frameRate = selectedClips.FirstOrDefault()?.frameRate > 0f ? selectedClips.First().frameRate : 30f,
            wrapMode = WrapMode.Once
        };

        Dictionary<EditorCurveBinding, AnimationCurve> floatCurves = new Dictionary<EditorCurveBinding, AnimationCurve>();
        Dictionary<EditorCurveBinding, ObjectReferenceKeyframe[]> objectCurves = new Dictionary<EditorCurveBinding, ObjectReferenceKeyframe[]>();

        foreach (AnimationClip sourceClip in selectedClips)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(sourceClip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                if (curve == null || curve.length == 0)
                {
                    continue;
                }

                if (floatCurves.TryGetValue(binding, out AnimationCurve existingCurve))
                {
                    if (curve.length > existingCurve.length)
                    {
                        report.Warnings.Add($"Duplicate float binding replaced with longer curve: {binding.path} / {binding.propertyName}");
                        floatCurves[binding] = CloneCurve(curve);
                    }
                    else
                    {
                        report.Warnings.Add($"Duplicate float binding kept first curve: {binding.path} / {binding.propertyName}");
                    }

                    continue;
                }

                floatCurves.Add(binding, CloneCurve(curve));
            }

            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(sourceClip))
            {
                ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(sourceClip, binding);
                if (curve == null || curve.Length == 0)
                {
                    continue;
                }

                if (objectCurves.TryGetValue(binding, out ObjectReferenceKeyframe[] existingCurve))
                {
                    if (curve.Length > existingCurve.Length)
                    {
                        report.Warnings.Add($"Duplicate object binding replaced with longer curve: {binding.path} / {binding.propertyName}");
                        objectCurves[binding] = CloneObjectCurve(curve);
                    }
                    else
                    {
                        report.Warnings.Add($"Duplicate object binding kept first curve: {binding.path} / {binding.propertyName}");
                    }

                    continue;
                }

                objectCurves.Add(binding, CloneObjectCurve(curve));
            }
        }

        foreach (KeyValuePair<EditorCurveBinding, AnimationCurve> pair in floatCurves)
        {
            AnimationUtility.SetEditorCurve(openClip, pair.Key, pair.Value);
        }

        foreach (KeyValuePair<EditorCurveBinding, ObjectReferenceKeyframe[]> pair in objectCurves)
        {
            AnimationUtility.SetObjectReferenceCurve(openClip, pair.Key, pair.Value);
        }

        SetLooping(openClip, false);

        AssetDatabase.DeleteAsset(OpenClipPath);
        AssetDatabase.CreateAsset(openClip, OpenClipPath);
        AssetDatabase.SaveAssets();

        report.OpenClipPath = OpenClipPath;
        report.OpenFloatBindings = floatCurves.Count;
        report.OpenObjectBindings = objectCurves.Count;
        report.OpenClipLength = openClip.length;

        return AssetDatabase.LoadAssetAtPath<AnimationClip>(OpenClipPath);
    }

    private static AnimationClip BuildCloseClip(AnimationClip openClip, WiringReport report)
    {
        AnimationClip closeClip = new AnimationClip
        {
            name = "Airlock_Close_Combined",
            frameRate = openClip.frameRate > 0f ? openClip.frameRate : 30f,
            wrapMode = WrapMode.Once
        };

        float clipLength = Mathf.Max(0.001f, openClip.length);

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(openClip))
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(openClip, binding);
            if (curve == null)
            {
                continue;
            }

            AnimationUtility.SetEditorCurve(closeClip, binding, ReverseCurve(curve, clipLength));
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(openClip))
        {
            ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(openClip, binding);
            if (curve == null)
            {
                continue;
            }

            AnimationUtility.SetObjectReferenceCurve(closeClip, binding, ReverseObjectCurve(curve, clipLength));
        }

        SetLooping(closeClip, false);

        AssetDatabase.DeleteAsset(CloseClipPath);
        AssetDatabase.CreateAsset(closeClip, CloseClipPath);
        AssetDatabase.SaveAssets();

        report.CloseClipPath = CloseClipPath;
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(CloseClipPath);
    }

    private static AnimatorController BuildAnimatorController(AnimationClip openClip, AnimationClip closeClip, WiringReport report)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Open", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Close", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsOpen", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        stateMachine.states = Array.Empty<ChildAnimatorState>();
        stateMachine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
        stateMachine.entryTransitions = Array.Empty<AnimatorTransition>();

        AnimatorState closed = stateMachine.AddState("Closed", new Vector3(250f, 100f, 0f));
        AnimatorState opening = stateMachine.AddState("Opening", new Vector3(500f, 100f, 0f));
        AnimatorState closing = stateMachine.AddState("Closing", new Vector3(500f, 250f, 0f));

        opening.motion = openClip;
        opening.speed = 1f;
        opening.writeDefaultValues = true;
        closing.motion = closeClip;
        closing.speed = 1f;
        closing.writeDefaultValues = true;

        stateMachine.defaultState = closed;

        AnimatorStateTransition openTransition = closed.AddTransition(opening);
        openTransition.hasExitTime = false;
        openTransition.duration = 0.05f;
        openTransition.AddCondition(AnimatorConditionMode.If, 0f, "Open");

        AnimatorStateTransition closeFromOpening = opening.AddTransition(closing);
        closeFromOpening.hasExitTime = false;
        closeFromOpening.duration = 0.05f;
        closeFromOpening.AddCondition(AnimatorConditionMode.If, 0f, "Close");

        AnimatorStateTransition closingToClosed = closing.AddTransition(closed);
        closingToClosed.hasExitTime = true;
        closingToClosed.exitTime = 1f;
        closingToClosed.duration = 0.05f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        report.ControllerPath = ControllerPath;
        return controller;
    }

    private static void WireScene(AnimatorController controller, WiringReport report)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject lockRoot = FindLockRoot();
        if (lockRoot == null)
        {
            throw new InvalidOperationException("Could not find scene instance root named 'lock'.");
        }

        Animator animator = lockRoot.GetComponent<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(lockRoot);
        }

        animator.runtimeAnimatorController = controller;
        EditorUtility.SetDirty(animator);

        AirlockController airlockController = FindCanonicalAirlockController();
        if (airlockController == null)
        {
            throw new InvalidOperationException("Could not find canonical AirlockController on GameObject path 'Airlock'.");
        }

        airlockController.airlockAnimator = animator;
        airlockController.openTriggerName = "Open";
        airlockController.closeTriggerName = "Close";
        airlockController.isOpenBoolName = "IsOpen";
        airlockController.useBoolParameter = false;
        EditorUtility.SetDirty(airlockController);

        report.LockSceneObjectPath = GetPath(lockRoot.transform);
        report.AnimatorAssigned = airlockController.airlockAnimator != null;
        report.BlockerAssigned = airlockController.blockerCollider != null;

        if (!report.BlockerAssigned)
        {
            report.Warnings.Add("No safe obvious blocker collider was assigned. AirlockController.blockerCollider remains missing.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject FindLockRoot()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (string.Equals(child.name, "lock", StringComparison.OrdinalIgnoreCase))
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    private static AirlockController FindCanonicalAirlockController()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!string.Equals(root.name, "Airlock", StringComparison.Ordinal))
            {
                continue;
            }

            return root.GetComponent<AirlockController>();
        }

        return null;
    }

    private static AnimationCurve CloneCurve(AnimationCurve curve)
    {
        AnimationCurve clone = new AnimationCurve(curve.keys)
        {
            preWrapMode = curve.preWrapMode,
            postWrapMode = curve.postWrapMode
        };
        return clone;
    }

    private static ObjectReferenceKeyframe[] CloneObjectCurve(ObjectReferenceKeyframe[] curve)
    {
        ObjectReferenceKeyframe[] clone = new ObjectReferenceKeyframe[curve.Length];
        Array.Copy(curve, clone, curve.Length);
        return clone;
    }

    private static AnimationCurve ReverseCurve(AnimationCurve curve, float clipLength)
    {
        Keyframe[] keys = curve.keys;
        Keyframe[] reversed = new Keyframe[keys.Length];

        for (int i = 0; i < keys.Length; i++)
        {
            Keyframe source = keys[keys.Length - 1 - i];
            Keyframe key = new Keyframe(clipLength - source.time, source.value)
            {
                inTangent = -source.outTangent,
                outTangent = -source.inTangent,
                inWeight = source.outWeight,
                outWeight = source.inWeight,
                weightedMode = source.weightedMode
            };
            reversed[i] = key;
        }

        AnimationCurve reversedCurve = new AnimationCurve(reversed)
        {
            preWrapMode = curve.preWrapMode,
            postWrapMode = curve.postWrapMode
        };
        return reversedCurve;
    }

    private static ObjectReferenceKeyframe[] ReverseObjectCurve(ObjectReferenceKeyframe[] curve, float clipLength)
    {
        ObjectReferenceKeyframe[] reversed = new ObjectReferenceKeyframe[curve.Length];
        for (int i = 0; i < curve.Length; i++)
        {
            ObjectReferenceKeyframe source = curve[curve.Length - 1 - i];
            reversed[i] = new ObjectReferenceKeyframe
            {
                time = clipLength - source.time,
                value = source.value
            };
        }

        return reversed;
    }

    private static void SetLooping(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static string GetPath(Transform transform)
    {
        Stack<string> names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private sealed class ClipInfo
    {
        public ClipInfo(string name, float length, int floatBindings, int objectBindings)
        {
            Name = name;
            Length = length;
            FloatBindings = floatBindings;
            ObjectBindings = objectBindings;
        }

        public string Name { get; }
        public float Length { get; }
        public int FloatBindings { get; }
        public int ObjectBindings { get; }
    }

    private sealed class WiringReport
    {
        public List<ClipInfo> ImportedClips { get; } = new List<ClipInfo>();
        public List<string> SelectedClips { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public string OpenClipPath { get; set; }
        public string CloseClipPath { get; set; }
        public string ControllerPath { get; set; }
        public string LockSceneObjectPath { get; set; }
        public int OpenFloatBindings { get; set; }
        public int OpenObjectBindings { get; set; }
        public float OpenClipLength { get; set; }
        public bool AnimatorAssigned { get; set; }
        public bool BlockerAssigned { get; set; }

        public string ToLogString()
        {
            return "[AirlockAnimationWiring] Complete\n" +
                   $"Imported clips found: {ImportedClips.Count}\n" +
                   $"Selected clips: {string.Join(", ", SelectedClips)}\n" +
                   $"Open clip: {OpenClipPath}\n" +
                   $"Close clip: {CloseClipPath}\n" +
                   $"Controller: {ControllerPath}\n" +
                   $"Merged float bindings: {OpenFloatBindings}\n" +
                   $"Merged object bindings: {OpenObjectBindings}\n" +
                   $"Open clip length: {OpenClipLength:0.###}s\n" +
                   $"Lock scene object: {LockSceneObjectPath}\n" +
                   $"Animator assigned: {AnimatorAssigned}\n" +
                   $"Blocker assigned: {BlockerAssigned}\n" +
                   $"Warnings: {string.Join(" | ", Warnings)}";
        }
    }
}
