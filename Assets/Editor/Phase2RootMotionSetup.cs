using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// PHASE 2 - builds the root-motion Animator graph from the measured/decided values and wires the
// scene (Apply Root Motion ON, RootMotionRelay on Astronaut Visual, controller tuning). State
// names match the hashes in ThirdPersonPlayerController exactly. Run once with saved.unity open.
public static class Phase2RootMotionSetup
{
    private const string Folder = "Assets/_External/Animations/Mixamo";
    private const string ControllerPath = "Assets/_Animations/Player/Astronaut_Player.controller";

    [MenuItem("Tools/Regolith/Phase 2 - Build Root Motion Setup")]
    public static void Build()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Phase 2: controller not found at {ControllerPath}");
            return;
        }

        // Clips (selected per measurement: Walking for walk, Run for lope, NOT Standard Run).
        AnimationClip idle = LoadClip("Idle");
        AnimationClip walking = LoadClip("Walking");
        AnimationClip lope = LoadClip("Run");
        AnimationClip forwardJump = LoadClip("Forward Jump");
        AnimationClip hop = LoadClip("Jumping_hop");
        AnimationClip stop = LoadClip("Stop Walking");
        AnimationClip leftTurn = LoadClip("Left Turn");
        AnimationClip rightTurn = LoadClip("Right Turn");
        AnimationClip turn180 = LoadClip("Walking Turn 180");
        AnimationClip fallingIdle = LoadClip("Falling Idle");
        AnimationClip landing = LoadClip("Falling To Landing");

        SetClipLoop("Idle", true);
        SetClipLoop("Walking", true);
        SetClipLoop("Run", true);
        SetClipLoop("Falling Idle", true);
        SetClipLoop("Forward Jump", false);
        SetClipLoop("Jumping_hop", false);
        SetClipLoop("Stop Walking", false);
        SetClipLoop("Falling To Landing", false);

        EnsureParameters(controller);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        ClearStateMachine(sm);

        // Locomotion blend tree: 0 = Idle, 0.45 = Walk, 1.0 = Lope, with per-clip time scales that
        // bring measured root speed down to the lunar targets (walk 0.60, lope 0.90 m/s).
        BlendTree tree = new BlendTree
        {
            name = "Locomotion Blend Tree",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(idle, 0f);
        tree.AddChild(walking, 0.45f);
        tree.AddChild(lope, 1.0f);
        ChildMotion[] children = tree.children;
        children[0].timeScale = 0.9f;   // Idle
        children[1].timeScale = 0.36f;  // Walk  (1.680 * 0.36 = 0.60 m/s)
        children[2].timeScale = 0.30f;  // Lope  (2.983 * 0.30 = 0.90 m/s)
        tree.children = children;

        AnimatorState locomotion = sm.AddState("Locomotion", new Vector3(300f, 0f, 0f));
        locomotion.motion = tree;
        locomotion.writeDefaultValues = true;
        sm.defaultState = locomotion;

        AnimatorState jumpHop = AddState(sm, "JumpHop", hop, 0.95f, new Vector3(600f, -120f, 0f));
        AnimatorState forwardJumpState = AddState(sm, "ForwardJump", forwardJump, 1.3f, new Vector3(600f, -60f, 0f));
        AnimatorState turnLeft = AddState(sm, "TurnLeft", leftTurn, 0.9f, new Vector3(600f, 0f, 0f));
        AnimatorState turnRight = AddState(sm, "TurnRight", rightTurn, 0.9f, new Vector3(600f, 60f, 0f));
        AnimatorState turn180State = AddState(sm, "Turn180", turn180, 0.7f, new Vector3(600f, 120f, 0f));
        AnimatorState stopWalk = AddState(sm, "StopWalk", stop, 2.0f, new Vector3(600f, 180f, 0f));
        AnimatorState falling = AddState(sm, "Falling", fallingIdle, 1.0f, new Vector3(300f, 200f, 0f));
        AnimatorState landingState = AddState(sm, "Landing", landing, 1.1f, new Vector3(600f, 260f, 0f));

        // Locomotion -> committed actions (triggers, no exit time, short blends).
        AddTrigger(locomotion, jumpHop, "Hop", 0.05f);
        AddTrigger(locomotion, forwardJumpState, "ForwardJump", 0.05f);
        AddTrigger(locomotion, turnLeft, "TurnLeft", 0.05f);
        AddTrigger(locomotion, turnRight, "TurnRight", 0.05f);
        AddTrigger(locomotion, turn180State, "Turn180", 0.05f);
        AddTrigger(locomotion, stopWalk, "Stop", 0.05f);

        // Committed actions -> Locomotion at 90% (controller ends committedAction at the same point).
        AddExit(jumpHop, locomotion, 0.9f, 0.08f);
        AddExit(forwardJumpState, locomotion, 0.9f, 0.08f);
        AddExit(turnLeft, locomotion, 0.9f, 0.08f);
        AddExit(turnRight, locomotion, 0.9f, 0.08f);
        AddExit(turn180State, locomotion, 0.9f, 0.08f);
        AddExit(stopWalk, locomotion, 0.9f, 0.08f);

        // Falling only from real airborne time (never from hops/jumps, which stay grounded).
        AnimatorStateTransition toFalling = locomotion.AddTransition(falling);
        toFalling.hasExitTime = false;
        toFalling.hasFixedDuration = true;
        toFalling.duration = 0.1f;
        toFalling.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
        toFalling.AddCondition(AnimatorConditionMode.Less, -0.25f, "VerticalSpeed");
        toFalling.AddCondition(AnimatorConditionMode.Greater, 0.30f, "AirborneTime");

        AnimatorStateTransition toLanding = falling.AddTransition(landingState);
        toLanding.hasExitTime = false;
        toLanding.hasFixedDuration = true;
        toLanding.duration = 0.05f;
        toLanding.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

        AddExit(landingState, locomotion, 0.8f, 0.1f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        string sceneStatus = WireScene(controller);

        Debug.Log(
            "Phase 2 root-motion setup complete:\n" +
            "- Blend tree: Idle(0, x0.9), Walking(0.45, x0.36 -> ~0.60 m/s), Run/Lope(1.0, x0.30 -> ~0.90 m/s)\n" +
            "- States: JumpHop(0.95), ForwardJump(1.3), TurnLeft(0.9), TurnRight(0.9), Turn180(0.7), StopWalk(2.0), Falling, Landing(1.1)\n" +
            "- Committed states exit to Locomotion at normalizedTime 0.90\n" +
            "- Falling gated by !Grounded & VerticalSpeed<-0.25 & AirborneTime>0.30\n" +
            $"- Scene: {sceneStatus}");
    }

    private static string WireScene(AnimatorController controller)
    {
        GameObject visual = GameObject.Find("Player/Astronaut Visual");
        if (visual == null)
        {
            return "Player/Astronaut Visual not found - assign Animator/relay manually.";
        }

        Animator animator = visual.GetComponent<Animator>();
        if (animator == null)
        {
            animator = visual.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = true; // ROOT MOTION ON

        RootMotionRelay relay = visual.GetComponent<RootMotionRelay>();
        if (relay == null)
        {
            relay = visual.AddComponent<RootMotionRelay>();
        }

        ThirdPersonPlayerController player = visual.GetComponentInParent<ThirdPersonPlayerController>();
        if (player != null)
        {
            SerializedObject so = new SerializedObject(player);
            SetBool(so, "useRootMotion", true);
            SetFloat(so, "gravity", -1.62f);
            SetFloat(so, "groundedStickSpeed", -0.75f);
            SetFloat(so, "forwardJumpRootScale", 0.66f);
            SetFloat(so, "hopRootScale", 1.0f);
            SetFloat(so, "accelerationRate", 1.5f);
            SetFloat(so, "decelerationRate", 1.2f);
            SetFloat(so, "walkRotationSpeed", 5f);
            SetFloat(so, "lopeRotationSpeed", 3f);
            SetFloat(so, "idleTurnMaxSpeed", 0.15f);
            SetFloat(so, "hardStopSpeedThreshold", 0.65f);
            SetFloat(so, "sideTurnDuration", 1.0f);
            SetFloat(so, "turn180Duration", 1.4f);
            SetObject(so, "animator", animator);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(player);

            SerializedObject relaySo = new SerializedObject(relay);
            SetObject(relaySo, "playerController", player);
            relaySo.ApplyModifiedProperties();
            EditorUtility.SetDirty(relay);
        }

        EditorUtility.SetDirty(visual);
        EditorSceneManager.MarkSceneDirty(visual.scene);
        EditorSceneManager.SaveScene(visual.scene);
        return "Apply Root Motion ON, RootMotionRelay attached, controller tuned & assigned.";
    }

    private static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip, float speed, Vector3 pos)
    {
        AnimatorState state = sm.AddState(name, pos);
        state.motion = clip;
        state.speed = speed;
        state.writeDefaultValues = true;
        if (clip == null)
        {
            Debug.LogWarning($"Phase 2: clip for state '{name}' was not found.");
        }
        return state;
    }

    private static void AddTrigger(AnimatorState from, AnimatorState to, string trigger, float duration)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = duration;
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void AddExit(AnimatorState from, AnimatorState to, float exitTime, float duration)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = duration;
    }

    private static void EnsureParameters(AnimatorController controller)
    {
        string[] floats = { "Speed", "VerticalSpeed", "AirborneTime" };
        string[] bools = { "Grounded" };
        string[] triggers = { "Hop", "ForwardJump", "TurnLeft", "TurnRight", "Turn180", "Stop" };

        foreach (AnimatorControllerParameter p in controller.parameters.ToArray())
        {
            bool keep = floats.Contains(p.name) || bools.Contains(p.name) || triggers.Contains(p.name);
            if (!keep)
            {
                controller.RemoveParameter(p);
            }
        }

        foreach (string n in floats) EnsureParam(controller, n, AnimatorControllerParameterType.Float);
        foreach (string n in bools) EnsureParam(controller, n, AnimatorControllerParameterType.Bool);
        foreach (string n in triggers) EnsureParam(controller, n, AnimatorControllerParameterType.Trigger);
    }

    private static void EnsureParam(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        if (!controller.parameters.Any(p => p.name == name))
        {
            controller.AddParameter(name, type);
        }
    }

    private static void ClearStateMachine(AnimatorStateMachine sm)
    {
        foreach (ChildAnimatorState s in sm.states.ToArray())
        {
            sm.RemoveState(s.state);
        }
        foreach (AnimatorStateTransition t in sm.anyStateTransitions.ToArray())
        {
            sm.RemoveAnyStateTransition(t);
        }
    }

    private static AnimationClip LoadClip(string fileName)
    {
        string path = $"{Folder}/{fileName}.fbx";
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    private static void SetClipLoop(string fileName, bool loop)
    {
        string path = $"{Folder}/{fileName}.fbx";
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip.loopTime != loop)
            {
                clip.loopTime = loop;
                changed = true;
            }
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
    }

    private static void SetObject(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = value;
    }
}
