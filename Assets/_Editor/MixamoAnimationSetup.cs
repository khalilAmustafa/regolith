using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MixamoAnimationSetup
{
    private const string MixamoFolder = "Assets/_External/Animations/Mixamo";
    private const string AstronautFolder = "Assets/_External/astronaut";
    private const string ControllerPath = "Assets/_Animations/Player/Astronaut_Player.controller";

    [MenuItem("Tools/Regolith/Setup Mixamo Animations")]
    public static void SetupMixamoAnimations()
    {
        EnsureFolders();

        string[] fbxPaths = Directory.Exists(MixamoFolder)
            ? Directory.GetFiles(MixamoFolder, "*.fbx", SearchOption.AllDirectories).Select(ToAssetPath).ToArray()
            : Array.Empty<string>();

        int changedCount = 0;
        int humanoidCount = 0;
        int createAvatarCount = 0;
        int loopChangedCount = 0;
        int rootBakeChangedCount = 0;
        List<string> manualFixes = new List<string>();

        foreach (string path in fbxPaths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                continue;
            }

            bool changed = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                humanoidCount++;
                changed = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                createAvatarCount++;
                changed = true;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            bool clipChanged = false;
            for (int i = 0; i < clips.Length; i++)
            {
                string clipName = $"{Path.GetFileNameWithoutExtension(path)} {clips[i].name}";
                bool targetLoop = ShouldLoop(clipName);

                if (clips[i].loopTime != targetLoop)
                {
                    clips[i].loopTime = targetLoop;
                    loopChangedCount++;
                    clipChanged = true;
                }

                if (!clips[i].lockRootRotation || !clips[i].lockRootHeightY || !clips[i].lockRootPositionXZ)
                {
                    clips[i].lockRootRotation = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].lockRootPositionXZ = true;
                    rootBakeChangedCount++;
                    clipChanged = true;
                }
            }

            if (clipChanged)
            {
                importer.clipAnimations = clips;
                changed = true;
            }

            if (changed)
            {
                changedCount++;
                importer.SaveAndReimport();
            }
        }

        if (fbxPaths.Length == 0)
        {
            manualFixes.Add($"No FBX files found in {MixamoFolder}. Place Mixamo animation FBX files there, then rerun this setup.");
        }

        string summary = $"Mixamo setup complete. FBX files: {fbxPaths.Length}. Reimported: {changedCount}. Set Humanoid: {humanoidCount}. Avatar Definition Create From This Model: {createAvatarCount}. Loop settings changed: {loopChangedCount}. Root bake settings changed: {rootBakeChangedCount}.";
        if (manualFixes.Count > 0)
        {
            summary += "\nManual fixes:\n- " + string.Join("\n- ", manualFixes);
        }

        Debug.Log(summary);
    }

    [MenuItem("Tools/Regolith/Create Astronaut Animator Controller")]
    public static void CreateAstronautAnimatorController()
    {
        EnsureFolders();

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "VerticalVelocity", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
        RemoveParameterIfExists(controller, "Grounded");

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        AnimationClip idle = FindClip("Idle");
        AnimationClip walking = FindClip("Walking", "Walk");
        AnimationClip running = FindClip("Standard Run", "Running", "Run");
        AnimationClip jumpingUp = FindClip("Jumping Up");
        AnimationClip fallingIdle = FindClip("Falling Idle");
        AnimationClip fallingToLanding = FindClip("Falling To Landing");

        SetClipLooping(running, true);
        SetClipLooping(fallingIdle, true);
        SetClipLooping(jumpingUp, false);
        SetClipLooping(fallingToLanding, false);

        AnimatorState locomotion = stateMachine.AddState("Locomotion", new Vector3(260f, 120f, 0f));
        locomotion.motion = CreateLocomotionMotion(controller, idle, walking, running);
        stateMachine.defaultState = locomotion;

        AnimatorState jumpUp = AddStateIfClipExists(stateMachine, "JumpUp", jumpingUp, new Vector3(540f, 20f, 0f));
        AnimatorState fallLoop = AddStateIfClipExists(stateMachine, "FallLoop", fallingIdle, new Vector3(540f, 120f, 0f));
        AnimatorState land = AddStateIfClipExists(stateMachine, "Land", fallingToLanding, new Vector3(540f, 220f, 0f));

        if (jumpUp != null)
        {
            AnimatorStateTransition transition = locomotion.AddTransition(jumpUp);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        }

        if (jumpUp != null && fallLoop != null)
        {
            AnimatorStateTransition transition = jumpUp.AddTransition(fallLoop);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.AddCondition(AnimatorConditionMode.Less, 0.1f, "VerticalVelocity");
        }

        if (fallLoop != null)
        {
            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(fallLoop);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            transition.AddCondition(AnimatorConditionMode.Less, -0.2f, "VerticalVelocity");
        }

        if (fallLoop != null && land != null)
        {
            AnimatorStateTransition transition = fallLoop.AddTransition(land);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
        }

        if (land != null)
        {
            AnimatorStateTransition transition = land.AddTransition(locomotion);
            transition.hasExitTime = true;
            transition.exitTime = 0.75f;
            transition.duration = 0.1f;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AssignControllerToAstronaut(controller);
        SetupPlayerMoonPhysics();

        Debug.Log($"Astronaut animator controller created or updated at {ControllerPath}");
    }

    private static Motion CreateLocomotionMotion(AnimatorController controller, AnimationClip idle, AnimationClip walking, AnimationClip running)
    {
        if (idle != null && walking != null)
        {
            BlendTree tree = new BlendTree
            {
                name = "Locomotion Blend Tree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0f);
            tree.AddChild(walking, running != null ? 0.5f : 1f);

            if (running != null)
            {
                tree.AddChild(running, 1f);
            }

            return tree;
        }

        if (idle != null)
        {
            return idle;
        }

        return walking != null ? walking : running;
    }

    private static AnimatorState AddStateIfClipExists(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip, Vector3 position)
    {
        if (clip == null)
        {
            Debug.LogWarning($"{stateName} was not created because its animation clip was not found.");
            return null;
        }

        AnimatorState state = stateMachine.AddState(stateName, position);
        state.motion = clip;
        state.writeDefaultValues = true;
        return state;
    }

    private static void AssignControllerToAstronaut(RuntimeAnimatorController controller)
    {
        GameObject visual = GameObject.Find("Player/Astronaut Visual");
        if (visual == null)
        {
            Debug.Log("Animator controller created. Scene object Player/Astronaut Visual was not found, so assignment is manual.");
            return;
        }

        Animator animator = visual.GetComponent<Animator>();
        if (animator == null)
        {
            animator = visual.AddComponent<Animator>();
        }

        animator.avatar = FindAstronautAvatar();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        EditorUtility.SetDirty(visual);

        ThirdPersonPlayerController playerController = visual.GetComponentInParent<ThirdPersonPlayerController>();
        if (playerController != null)
        {
            SerializedObject serializedController = new SerializedObject(playerController);
            serializedController.FindProperty("animator").objectReferenceValue = animator;
            serializedController.ApplyModifiedProperties();
            EditorUtility.SetDirty(playerController);
        }
    }

    private static void SetupPlayerMoonPhysics()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.Log("Player object was not found, so Rigidbody and GroundCheck setup is manual.");
            return;
        }

        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController != null)
        {
            UnityEngine.Object.DestroyImmediate(characterController);
        }

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = player.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotation;

        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = player.AddComponent<CapsuleCollider>();
        }

        capsule.height = 2f;
        capsule.radius = 0.45f;
        capsule.center = new Vector3(0f, 1f, 0f);

        ThirdPersonPlayerController controller = player.GetComponent<ThirdPersonPlayerController>();
        if (controller != null)
        {
            Transform groundCheck = player.transform.Find("GroundCheck");
            if (groundCheck == null)
            {
                GameObject groundCheckObject = new GameObject("GroundCheck");
                groundCheckObject.transform.SetParent(player.transform);
                groundCheckObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                groundCheckObject.transform.localRotation = Quaternion.identity;
                groundCheck = groundCheckObject.transform;
            }

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("groundCheck").objectReferenceValue = groundCheck;
            serializedController.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        EditorUtility.SetDirty(player);
    }

    private static void EnsureFolders()
    {
        CreateFolderIfMissing("Assets/_External");
        CreateFolderIfMissing("Assets/_External/Animations");
        CreateFolderIfMissing(MixamoFolder);
        CreateFolderIfMissing("Assets/_Animations");
        CreateFolderIfMissing("Assets/_Animations/Player");
        CreateFolderIfMissing("Assets/_Editor");
    }

    private static void CreateFolderIfMissing(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static Avatar FindAstronautAvatar()
    {
        if (!AssetDatabase.IsValidFolder(AstronautFolder))
        {
            return null;
        }

        string[] guids = AssetDatabase.FindAssets("t:Avatar", new[] { AstronautFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            if (avatar != null)
            {
                return avatar;
            }

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Avatar nestedAvatar)
                {
                    return nestedAvatar;
                }
            }
        }

        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { AstronautFolder });
        foreach (string guid in modelGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Avatar nestedAvatar)
                {
                    return nestedAvatar;
                }
            }
        }

        return null;
    }

    private static AnimationClip FindClip(params string[] names)
    {
        if (!Directory.Exists(MixamoFolder))
        {
            return null;
        }

        string[] fbxPaths = Directory.GetFiles(MixamoFolder, "*.fbx", SearchOption.AllDirectories).Select(ToAssetPath).ToArray();
        foreach (string name in names)
        {
            foreach (string path in fbxPaths)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                AnimationClip clip = LoadFirstAnimationClip(path);
                if (clip != null)
                {
                    return clip;
                }
            }
        }

        foreach (string path in fbxPaths)
        {
            foreach (AnimationClip clip in LoadAnimationClips(path))
            {
                if (names.Any(name => clip.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return clip;
                }
            }
        }

        return null;
    }

    private static AnimationClip LoadFirstAnimationClip(string path)
    {
        return LoadAnimationClips(path).FirstOrDefault();
    }

    private static IEnumerable<AnimationClip> LoadAnimationClips(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));
    }

    private static void SetClipLooping(AnimationClip clip, bool loop)
    {
        if (clip == null)
        {
            return;
        }

        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty loopTime = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopTime");
        if (loopTime != null)
        {
            loopTime.boolValue = loop;
            serializedClip.ApplyModifiedProperties();
            EditorUtility.SetDirty(clip);
        }
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
        {
            stateMachine.RemoveStateMachine(childStateMachine.stateMachine);
        }

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        if (controller.parameters.Any(parameter => parameter.name == name))
        {
            return;
        }

        controller.AddParameter(name, type);
    }

    private static void RemoveParameterIfExists(AnimatorController controller, string name)
    {
        AnimatorControllerParameter parameter = controller.parameters.FirstOrDefault(item => item.name == name);
        if (parameter != null)
        {
            controller.RemoveParameter(parameter);
        }
    }

    private static bool ShouldLoop(string name)
    {
        return ContainsAny(name, "Idle", "Walking", "Walk", "Running", "Run", "Standard Run", "Falling Idle")
            && !ContainsAny(name, "Jumping Up", "Falling To Landing", "Left Turn", "Right Turn", "Walking Turn 180", "Stop Walking", "Jump", "Land");
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string ToAssetPath(string path)
    {
        return path.Replace("\\", "/");
    }
}
