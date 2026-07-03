using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AirlockClosedPoseFixUtility
{
    private const string ScenePath = "Assets/backup.unity";
    private const string OpenClipPath = "Assets/_Animations/Airlock/Airlock_Open_Combined.anim";
    private const string ClosedClipPath = "Assets/_Animations/Airlock/Airlock_Closed_Pose.anim";
    private const string ControllerPath = "Assets/_Animations/Airlock/Airlock.controller";

    public static void Run()
    {
        AnimationClip openClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(OpenClipPath);
        if (openClip == null)
        {
            throw new InvalidOperationException($"Missing open clip at {OpenClipPath}.");
        }

        AnimationClip closedClip = BuildClosedPoseClip(openClip);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException($"Missing airlock Animator Controller at {ControllerPath}.");
        }

        AssignClosedState(controller, closedClip);
        WireScene(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[AirlockClosedPoseFix] Complete\n" +
            $"Closed pose clip: {ClosedClipPath}\n" +
            $"Source open clip: {OpenClipPath}\n" +
            $"Closed float bindings: {AnimationUtility.GetCurveBindings(closedClip).Length}\n" +
            $"Closed object bindings: {AnimationUtility.GetObjectReferenceCurveBindings(closedClip).Length}\n" +
            $"Controller: {ControllerPath}\n" +
            $"Scene saved: {ScenePath}");
    }

    private static AnimationClip BuildClosedPoseClip(AnimationClip openClip)
    {
        AnimationClip closedClip = new AnimationClip
        {
            name = "Airlock_Closed_Pose",
            frameRate = openClip.frameRate > 0f ? openClip.frameRate : 30f,
            wrapMode = WrapMode.Once
        };

        float holdTime = 1f / Mathf.Max(1f, closedClip.frameRate);

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(openClip))
        {
            AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(openClip, binding);
            if (sourceCurve == null || sourceCurve.length == 0)
            {
                continue;
            }

            float value = sourceCurve.Evaluate(0f);
            AnimationCurve poseCurve = AnimationCurve.Constant(0f, holdTime, value);
            AnimationUtility.SetEditorCurve(closedClip, binding, poseCurve);
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(openClip))
        {
            ObjectReferenceKeyframe[] sourceCurve = AnimationUtility.GetObjectReferenceCurve(openClip, binding);
            if (sourceCurve == null || sourceCurve.Length == 0)
            {
                continue;
            }

            UnityEngine.Object value = sourceCurve[0].value;
            ObjectReferenceKeyframe[] poseCurve =
            {
                new ObjectReferenceKeyframe { time = 0f, value = value },
                new ObjectReferenceKeyframe { time = holdTime, value = value }
            };
            AnimationUtility.SetObjectReferenceCurve(closedClip, binding, poseCurve);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(closedClip);
        settings.loopTime = false;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(closedClip, settings);

        AssetDatabase.DeleteAsset(ClosedClipPath);
        AssetDatabase.CreateAsset(closedClip, ClosedClipPath);
        AssetDatabase.SaveAssets();

        AnimationClip savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClosedClipPath);
        if (savedClip == null || AnimationUtility.GetCurveBindings(savedClip).Length == 0)
        {
            throw new InvalidOperationException("Closed pose clip was not created correctly or has zero float bindings.");
        }

        return savedClip;
    }

    private static void AssignClosedState(AnimatorController controller, AnimationClip closedClip)
    {
        AnimatorState closedState = null;
        foreach (ChildAnimatorState childState in controller.layers[0].stateMachine.states)
        {
            if (string.Equals(childState.state.name, "Closed", StringComparison.Ordinal))
            {
                closedState = childState.state;
                break;
            }
        }

        if (closedState == null)
        {
            throw new InvalidOperationException("Airlock.controller does not contain a Closed state.");
        }

        closedState.motion = closedClip;
        closedState.speed = 1f;
        closedState.writeDefaultValues = true;
        controller.layers[0].stateMachine.defaultState = closedState;

        EditorUtility.SetDirty(closedState);
        EditorUtility.SetDirty(controller);
    }

    private static void WireScene(AnimatorController controller)
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
            throw new InvalidOperationException("Could not find AirlockController on root GameObject named 'Airlock'.");
        }

        airlockController.airlockAnimator = animator;
        airlockController.openTriggerName = "Open";
        airlockController.closeTriggerName = "Close";
        airlockController.isOpenBoolName = "IsOpen";
        airlockController.useBoolParameter = false;
        airlockController.isOpen = false;
        airlockController.isPressurizing = false;
        EditorUtility.SetDirty(airlockController);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject FindLockRoot()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
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
            if (string.Equals(root.name, "Airlock", StringComparison.Ordinal))
            {
                return root.GetComponent<AirlockController>();
            }
        }

        return null;
    }
}
