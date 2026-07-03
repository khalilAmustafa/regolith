using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RegolithGatherAnimationSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Regolith Gathering Animation";
    private const string SourceFileName = "Gathering Objects.fbx";
    private const string TargetFolder = "Assets/_Animations/Regolith";
    private const string TargetAssetPath = TargetFolder + "/Gathering Objects.fbx";
    private const string GatherTrigger = "Gather";
    private const string GatherStateName = "Gathering Objects";
    private const float SafeFallbackGatherDuration = 1.2f;

    [MenuItem(MenuPath)]
    public static void SetupRegolithGatheringAnimation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Regolith gathering animation setup cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        bool copiedOrFound = EnsureAnimationAsset();
        if (!copiedOrFound)
        {
            Debug.LogError("Gathering Objects.fbx not found in Downloads. Please place it in Assets/_Animations/Regolith/");
        }

        ConfigureGatherFbxImportSettings();
        AssetDatabase.ImportAsset(TargetAssetPath, ImportAssetOptions.ForceUpdate);
        AnimationClip gatherClip = FindGatheringClip();
        bool humanoidUsable = IsHumanoidAnimationUsable();
        if (!humanoidUsable)
        {
            Debug.LogWarning("Gathering Objects.fbx did not report a valid Humanoid avatar. The setup will still wire the clip because playback was requested, but the locked fallback will be used if the Animator cannot enter the gather state.");
        }

        if (gatherClip == null)
        {
            Debug.LogWarning("Regolith gathering animation setup: no animation clip was found inside " + TargetAssetPath + ". Regolith collection will use fallbackGatherDuration.");
        }

        Animator playerAnimator = FindPlayerAnimator();
        AnimatorController controller = FindAnimatorController(playerAnimator);
        bool controllerWired = false;

        if (controller != null && gatherClip != null)
        {
            controllerWired = WireAnimatorController(controller, gatherClip);
            EditorUtility.SetDirty(controller);
        }
        else if (playerAnimator == null)
        {
            Debug.LogWarning("Regolith gathering animation setup: no Player Animator was found. Assign RegolithCollectionManager.playerAnimator manually.");
        }
        else if (controller == null)
        {
            Debug.LogWarning("Regolith gathering animation setup: Player Animator does not use a directly editable AnimatorController. Add Gather/Gathering Objects manually or assign an AnimatorController.");
        }

        RegolithCollectionManager manager = FindOrCreateManager();
        WireManager(manager, playerAnimator, gatherClip);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Regolith gathering animation setup complete:\n" +
            "- FBX project path: " + TargetAssetPath + "\n" +
            "- FBX copied/found in project: " + (File.Exists(TargetAssetPath) ? "yes" : "no") + "\n" +
            "- Animation clip found: " + (gatherClip != null ? gatherClip.name : "no") + "\n" +
            "- Humanoid compatible: " + (humanoidUsable ? "yes" : "no") + "\n" +
            "- Player Animator found: " + (playerAnimator != null ? "yes" : "no") + "\n" +
            "- AnimatorController wired: " + (controllerWired ? "yes" : "no") + "\n" +
            "- RegolithCollectionManager wired: " + (manager != null ? "yes" : "no") + "\n" +
            "- Root transform baked into pose: yes\n" +
            "- Imported gather animation playback: enabled");
    }

    private static bool EnsureAnimationAsset()
    {
        EnsureAssetFolder(TargetFolder);

        if (File.Exists(TargetAssetPath))
        {
            return true;
        }

        string downloadsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            SourceFileName);

        if (!File.Exists(downloadsPath))
        {
            return false;
        }

        File.Copy(downloadsPath, TargetAssetPath, true);
        AssetDatabase.ImportAsset(TargetAssetPath, ImportAssetOptions.ForceUpdate);
        return true;
    }

    private static AnimationClip FindGatheringClip()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TargetAssetPath);
        return assets
            .OfType<AnimationClip>()
            .Where(clip => clip != null && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(clip => clip.name.IndexOf("Gather", StringComparison.OrdinalIgnoreCase) >= 0)
            .ThenByDescending(clip => clip.length)
            .FirstOrDefault();
    }

    private static bool IsHumanoidAnimationUsable()
    {
        Avatar avatar = AssetDatabase
            .LoadAllAssetsAtPath(TargetAssetPath)
            .OfType<Avatar>()
            .FirstOrDefault();

        return avatar != null && avatar.isValid && avatar.isHuman;
    }

    private static void ConfigureGatherFbxImportSettings()
    {
        ModelImporter importer = AssetImporter.GetAtPath(TargetAssetPath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips != null)
        {
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = false;
                clip.loopPose = false;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            Debug.LogWarning("Gathering Objects.fbx could not be set to Humanoid cleanly. If the player twists or falls, RegolithCollectionManager will still use the locked fallback gather flow.");
        }
    }

    private static Animator FindPlayerAnimator()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Animator animator = player.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                return animator;
            }
        }

        return UnityEngine.Object.FindAnyObjectByType<Animator>();
    }

    private static AnimatorController FindAnimatorController(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Animations/Player/Astronaut_Player.controller");
        }

        if (animator.runtimeAnimatorController is AnimatorController controller)
        {
            return controller;
        }

        if (animator.runtimeAnimatorController is AnimatorOverrideController overrideController)
        {
            return overrideController.runtimeAnimatorController as AnimatorController;
        }

        return AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Animations/Player/Astronaut_Player.controller");
    }

    private static bool WireAnimatorController(AnimatorController controller, AnimationClip gatherClip)
    {
        EnsureParameter(controller, GatherTrigger, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState gatherState = FindState(stateMachine, GatherStateName);
        if (gatherState == null)
        {
            gatherState = stateMachine.AddState(GatherStateName, new Vector3(640f, 260f, 0f));
        }

        gatherState.motion = gatherClip;
        gatherState.writeDefaultValues = true;

        AnimatorState returnState = FindState(stateMachine, "Locomotion")
            ?? FindState(stateMachine, "Idle")
            ?? stateMachine.defaultState;

        EnsureAnyStateGatherTransition(stateMachine, gatherState);

        if (returnState != null && returnState != gatherState)
        {
            EnsureGatherExitTransition(gatherState, returnState);
        }
        else
        {
            Debug.LogWarning("Regolith gathering animation setup: no Locomotion/Idle/default return state found. Gathering state was added, but return transition needs manual review.");
        }

        return true;
    }

    private static void EnsureAnyStateGatherTransition(AnimatorStateMachine stateMachine, AnimatorState gatherState)
    {
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            if (transition.destinationState == gatherState && HasCondition(transition, GatherTrigger))
            {
                return;
            }
        }

        AnimatorStateTransition anyToGather = stateMachine.AddAnyStateTransition(gatherState);
        anyToGather.hasExitTime = false;
        anyToGather.exitTime = 0f;
        anyToGather.duration = 0.08f;
        anyToGather.canTransitionToSelf = false;
        anyToGather.AddCondition(AnimatorConditionMode.If, 0f, GatherTrigger);
    }

    private static void EnsureGatherExitTransition(AnimatorState gatherState, AnimatorState returnState)
    {
        foreach (AnimatorStateTransition transition in gatherState.transitions)
        {
            if (transition.destinationState == returnState && transition.hasExitTime)
            {
                return;
            }
        }

        AnimatorStateTransition exitTransition = gatherState.AddTransition(returnState);
        exitTransition.hasExitTime = true;
        exitTransition.exitTime = 0.95f;
        exitTransition.duration = 0.12f;
        exitTransition.hasFixedDuration = true;
    }

    private static bool HasCondition(AnimatorStateTransition transition, string parameterName)
    {
        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.parameter == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state != null && childState.state.name == stateName)
            {
                return childState.state;
            }
        }

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
        {
            AnimatorState nested = FindState(childStateMachine.stateMachine, stateName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void EnsureParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType parameterType)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == parameterName)
            {
                return;
            }
        }

        controller.AddParameter(parameterName, parameterType);
    }

    private static RegolithCollectionManager FindOrCreateManager()
    {
        RegolithCollectionManager manager = UnityEngine.Object.FindAnyObjectByType<RegolithCollectionManager>();
        if (manager != null)
        {
            return manager;
        }

        GameObject managerObject = new GameObject("RegolithCollectionManager");
        Undo.RegisterCreatedObjectUndo(managerObject, "Create regolith collection manager");
        return Undo.AddComponent<RegolithCollectionManager>(managerObject);
    }

    private static void WireManager(RegolithCollectionManager manager, Animator playerAnimator, AnimationClip gatherClip)
    {
        if (manager == null)
        {
            return;
        }

        Undo.RecordObject(manager, "Wire regolith gathering animation");

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (manager.player == null && playerObject != null)
        {
            manager.player = playerObject.transform;
        }

        if (manager.resources == null && manager.player != null)
        {
            manager.resources = manager.player.GetComponent<PlayerSurvivalResources>();
        }

        if (manager.regolithParent == null)
        {
            GameObject regolithRoad = GameObject.Find("Regolith_Road");
            manager.regolithParent = regolithRoad != null ? regolithRoad.transform : null;
        }

        manager.playerAnimator = playerAnimator;
        manager.gatheringAnimationClip = gatherClip;
        manager.gatherTriggerName = GatherTrigger;
        manager.collectAfterGatherAnimation = true;
        manager.useImportedGatherAnimation = gatherClip != null;
        manager.fallbackGatherDuration = SafeFallbackGatherDuration;
        manager.gatherAnimationController = EnsureGatherController(manager.player, playerAnimator, manager.fallbackGatherDuration);

        EditorUtility.SetDirty(manager);
    }

    private static PlayerGatherAnimationController EnsureGatherController(Transform player, Animator playerAnimator, float fallbackDuration)
    {
        if (player == null)
        {
            return UnityEngine.Object.FindAnyObjectByType<PlayerGatherAnimationController>();
        }

        PlayerGatherAnimationController controller = player.GetComponent<PlayerGatherAnimationController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<PlayerGatherAnimationController>(player.gameObject);
        }
        else
        {
            Undo.RecordObject(controller, "Configure player gather animation controller");
        }

        controller.animator = playerAnimator != null ? playerAnimator : player.GetComponentInChildren<Animator>();
        controller.playerControllerToDisable = player.GetComponent<ThirdPersonPlayerController>();
        controller.rootMotionRelayToDisable = player.GetComponentInChildren<RootMotionRelay>();
        controller.playerRigidbody = player.GetComponent<Rigidbody>();
        controller.gatherTriggerName = GatherTrigger;
        controller.gatherStateName = GatherStateName;
        controller.fallbackDuration = SafeFallbackGatherDuration;
        controller.useImportedGatherAnimation = true;
        controller.lockRootTransformDuringGather = true;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string normalized = folderPath.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        string[] parts = normalized.Split('/');
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
}
