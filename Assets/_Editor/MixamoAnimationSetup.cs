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

        EnsureExactParameters(controller);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        AnimationClip idle = FindClipByFileName("Idle");
        AnimationClip walking = FindClipByFileName("Walking", "Walk");
        AnimationClip running = FindClip("Standard Run", "Running", "Run");
        AnimationClip jumpingUp = FindClipByFileName("Jumping Up");
        AnimationClip fallingIdle = FindClipByFileName("Falling Idle");
        AnimationClip fallingToLanding = FindClipByFileName("Falling To Landing");

        SetClipLooping(running, true);
        SetClipLooping(fallingIdle, true);
        SetClipLooping(jumpingUp, false);
        SetClipLooping(fallingToLanding, false);

        AnimatorState locomotion = stateMachine.AddState("Locomotion", new Vector3(260f, 120f, 0f));
        locomotion.motion = CreateLocomotionMotion(controller, idle, walking, running);
        stateMachine.defaultState = locomotion;

        AnimatorState jumpStart = AddStateIfClipExists(stateMachine, "JumpStart", jumpingUp, new Vector3(540f, 20f, 0f));
        AnimatorState falling = AddStateIfClipExists(stateMachine, "Falling", fallingIdle, new Vector3(540f, 120f, 0f));
        AnimatorState landing = AddStateIfClipExists(stateMachine, "Landing", fallingToLanding, new Vector3(540f, 220f, 0f));

        if (jumpStart != null)
        {
            AnimatorStateTransition transition = locomotion.AddTransition(jumpStart);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        }

        if (jumpStart != null && falling != null)
        {
            AnimatorStateTransition exitTransition = jumpStart.AddTransition(falling);
            exitTransition.hasExitTime = true;
            exitTransition.exitTime = 0.35f;
            exitTransition.duration = 0.1f;

            AnimatorStateTransition airborneTransition = jumpStart.AddTransition(falling);
            airborneTransition.hasExitTime = false;
            airborneTransition.duration = 0.1f;
            airborneTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
        }

        if (falling != null)
        {
            AnimatorStateTransition transition = locomotion.AddTransition(falling);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            transition.AddCondition(AnimatorConditionMode.Less, -0.3f, "VerticalSpeed");
        }

        if (falling != null && landing != null)
        {
            AnimatorStateTransition transition = falling.AddTransition(landing);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        }

        if (landing != null)
        {
            AnimatorStateTransition transition = landing.AddTransition(locomotion);
            transition.hasExitTime = true;
            transition.exitTime = 0.7f;
            transition.duration = 0.1f;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AssignControllerToAstronaut(controller);
        AuditAndFixPlayerSetup();

        Debug.Log($"Astronaut animator controller created or updated at {ControllerPath}");
    }

    [MenuItem("Tools/Regolith/Audit And Fix Player Setup")]
    public static void AuditAndFixPlayerSetup()
    {
        EnsureGroundLayer();
        SetupPlayerMoonPhysics();
        AssignControllerToAstronaut(AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath));
        AuditCameraSetup();
        Debug.Log("Player setup audit/fix complete. Check Player, GroundCheck, Astronaut Visual Animator, CharacterController, and Main Camera in the Inspector.");
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

        MonoBehaviour relay = visual.GetComponent("PlayerAnimationEventRelay") as MonoBehaviour;
        if (relay == null)
        {
            Type relayType = FindType("PlayerAnimationEventRelay");
            if (relayType != null)
            {
                relay = visual.AddComponent(relayType) as MonoBehaviour;
            }
        }

        EditorUtility.SetDirty(visual);

        ThirdPersonPlayerController playerController = visual.GetComponentInParent<ThirdPersonPlayerController>();
        if (playerController != null)
        {
            SerializedObject serializedController = new SerializedObject(playerController);
            serializedController.FindProperty("animator").objectReferenceValue = animator;
            serializedController.ApplyModifiedProperties();
            EditorUtility.SetDirty(playerController);

            if (relay != null)
            {
                SerializedObject serializedRelay = new SerializedObject(relay);
                serializedRelay.FindProperty("playerController").objectReferenceValue = playerController;
                serializedRelay.ApplyModifiedProperties();
                EditorUtility.SetDirty(relay);
            }
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

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            UnityEngine.Object.DestroyImmediate(body);
        }

        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            UnityEngine.Object.DestroyImmediate(capsule);
        }

        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = player.AddComponent<CharacterController>();
        }

        characterController.height = 2f;
        characterController.radius = 0.45f;
        characterController.center = new Vector3(0f, 1f, 0f);
        characterController.stepOffset = 0.3f;
        characterController.slopeLimit = 45f;

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
            serializedController.FindProperty("groundCheckRadius").floatValue = 0.22f;
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
            {
                serializedController.FindProperty("groundMask").intValue = 1 << groundLayer;
            }
            serializedController.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        MovePlayerAboveGroundIfNeeded(player, characterController);
        AssignGroundLayerToLikelyGroundObjects(player);

        EditorUtility.SetDirty(player);
    }

    private static void AuditCameraSetup()
    {
        GameObject player = GameObject.Find("Player");
        Camera camera = Camera.main;
        if (player == null || camera == null)
        {
            return;
        }

        ThirdPersonCameraFollow follow = camera.GetComponent<ThirdPersonCameraFollow>();
        if (follow != null)
        {
            follow.SetTarget(player.transform);
        }

        Vector3 desiredPosition = player.transform.position + new Vector3(0f, 2.4f, -5.5f);
        if (Vector3.Distance(camera.transform.position, player.transform.position) < 1.2f)
        {
            camera.transform.position = desiredPosition;
        }

        camera.transform.LookAt(player.transform.position + Vector3.up * 1.4f);
        EditorUtility.SetDirty(camera);
    }

    private static void MovePlayerAboveGroundIfNeeded(GameObject player, CharacterController characterController)
    {
        Vector3 capsuleBottom = GetCapsuleBottomWorldPosition(player.transform, characterController);
        if (Physics.Raycast(player.transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, 1 << LayerMask.NameToLayer("Ground")))
        {
            float desiredBottomY = hit.point.y + 0.02f;
            float delta = desiredBottomY - capsuleBottom.y;
            if (Mathf.Abs(delta) > 0.02f)
            {
                player.transform.position += Vector3.up * delta;
            }
        }
    }

    private static Vector3 GetCapsuleBottomWorldPosition(Transform player, CharacterController characterController)
    {
        float bottomOffset = (characterController.height * 0.5f) - characterController.radius;
        return player.TransformPoint(characterController.center + Vector3.down * bottomOffset);
    }

    private static void AssignGroundLayerToLikelyGroundObjects(GameObject player)
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
        {
            return;
        }

        foreach (Collider collider in UnityEngine.Object.FindObjectsOfType<Collider>())
        {
            if (collider.transform.IsChildOf(player.transform))
            {
                continue;
            }

            string lowerName = collider.name.ToLowerInvariant();
            if (lowerName.Contains("ground") || lowerName.Contains("floor") || lowerName.Contains("platform") || collider is TerrainCollider)
            {
                collider.gameObject.layer = groundLayer;
                EditorUtility.SetDirty(collider.gameObject);
            }
        }
    }

    private static void EnsureGroundLayer()
    {
        if (LayerMask.NameToLayer("Ground") >= 0)
        {
            return;
        }

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = "Ground";
                tagManager.ApplyModifiedProperties();
                return;
            }
        }

        Debug.LogWarning("No empty user layer slot is available for Ground. Create a Ground layer manually and rerun the audit.");
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

    private static Type FindType(string typeName)
    {
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
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

    private static AnimationClip FindClipByFileName(params string[] fileNames)
    {
        if (!Directory.Exists(MixamoFolder))
        {
            return null;
        }

        string[] fbxPaths = Directory.GetFiles(MixamoFolder, "*.fbx", SearchOption.AllDirectories).Select(ToAssetPath).ToArray();
        foreach (string fileName in fileNames)
        {
            string exactPath = fbxPaths.FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), fileName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exactPath))
            {
                AnimationClip clip = LoadFirstAnimationClip(exactPath);
                if (clip != null)
                {
                    return clip;
                }
            }
        }

        return FindClip(fileNames);
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

    private static void EnsureExactParameters(AnimatorController controller)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
        {
            if (parameter.name != "Speed" && parameter.name != "Grounded" && parameter.name != "VerticalSpeed" && parameter.name != "Jump")
            {
                controller.RemoveParameter(parameter);
            }
        }

        EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "VerticalSpeed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
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
