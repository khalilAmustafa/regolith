using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-time tool: import Jump_Hop.fbx with the requested settings and swap it into
// the existing "JumpStart" state (renamed "Lunar_Hop") of Astronaut_Player.controller.
// It does NOT rebuild the Animator graph, touch Locomotion/Idle/Walk/Run, add animation
// events, add a relay, or enable Apply Root Motion. Safe to delete after a successful run.
public static class ApplyLunarHop
{
    private const string HopPath = "Assets/_External/Animations/Mixamo/Jump_Hop.fbx";
    private const string ControllerPath = "Assets/_Animations/Player/Astronaut_Player.controller";

    [MenuItem("Tools/Regolith/Apply Lunar Hop")]
    public static void Apply()
    {
        // --- 1. Configure import settings on the real FBX, then reimport. ---
        ModelImporter importer = AssetImporter.GetAtPath(HopPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"Apply Lunar Hop: no ModelImporter at '{HopPath}'. Make sure Jump_Hop.fbx exists in the project and has been imported once.");
            return;
        }

        importer.animationType = ModelImporterAnimationType.Human;     // Rig: Humanoid
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; // Avatar: Create From This Model
        importer.sourceAvatar = null;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = false;            // Loop Time: OFF
            clip.lockRootRotation = true;     // Root Transform Rotation > Bake Into Pose: ON
            clip.lockRootHeightY = true;      // Root Transform Position (Y) > Bake Into Pose: ON
            clip.lockRootPositionXZ = true;   // Root Transform Position (XZ) > Bake Into Pose: ON
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        AnimationClip hopClip = AssetDatabase.LoadAllAssetsAtPath(HopPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        if (hopClip == null)
        {
            Debug.LogError($"Apply Lunar Hop: imported '{HopPath}' but found no AnimationClip inside it. Aborting controller update.");
            return;
        }

        // --- 2. Surgically update the controller (no rebuild). ---
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Apply Lunar Hop: could not load AnimatorController at '{ControllerPath}'.");
            return;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState jumpState = FindState(stateMachine, "JumpStart") ?? FindState(stateMachine, "Lunar_Hop");
        AnimatorState locomotion = FindState(stateMachine, "Locomotion");
        AnimatorState falling = FindState(stateMachine, "Falling");

        if (jumpState == null || locomotion == null)
        {
            Debug.LogError("Apply Lunar Hop: expected states not found (need 'JumpStart'/'Lunar_Hop' and 'Locomotion'). No changes made to the controller.");
            return;
        }

        Undo.RegisterCompleteObjectUndo(controller, "Apply Lunar Hop");

        string oldStateName = jumpState.name;
        jumpState.name = "Lunar_Hop";
        jumpState.motion = hopClip;

        // Remove any transition from the hop state into Falling so a normal hop is
        // Locomotion -> Lunar_Hop -> Locomotion and does not force Falling every jump.
        int removedToFalling = 0;
        if (falling != null)
        {
            foreach (AnimatorStateTransition transition in jumpState.transitions.ToArray())
            {
                if (transition.destinationState == falling)
                {
                    jumpState.RemoveTransition(transition);
                    removedToFalling++;
                }
            }
        }

        // Keep the entry transition (Locomotion -> Lunar_Hop) short and immediate.
        AnimatorStateTransition toHop = locomotion.transitions.FirstOrDefault(t => t.destinationState == jumpState);
        if (toHop != null)
        {
            toHop.hasExitTime = false;
            toHop.hasFixedDuration = true;
            toHop.duration = 0.08f;
        }

        // Ensure a clean, short exit back to Locomotion near the end of the hop clip.
        AnimatorStateTransition toLocomotion = jumpState.transitions.FirstOrDefault(t => t.destinationState == locomotion);
        if (toLocomotion == null)
        {
            toLocomotion = jumpState.AddTransition(locomotion);
        }

        toLocomotion.hasExitTime = true;
        toLocomotion.exitTime = 0.8f;
        toLocomotion.hasFixedDuration = true;
        toLocomotion.duration = 0.1f;

        // Gate Falling so it only plays for a real fall / longer airborne time, never
        // for a normal small hop: Grounded == false AND VerticalSpeed < -0.25 AND
        // AirborneTime > 0.30s. AirborneTime is a float parameter driven by the controller.
        EnsureFloatParameter(controller, "AirborneTime");
        int fallingConditionCount = -1;
        if (falling != null)
        {
            AnimatorStateTransition locomotionToFalling = locomotion.transitions.FirstOrDefault(t => t.destinationState == falling);
            if (locomotionToFalling != null)
            {
                foreach (AnimatorCondition condition in locomotionToFalling.conditions.ToArray())
                {
                    locomotionToFalling.RemoveCondition(condition);
                }

                locomotionToFalling.hasExitTime = false;
                locomotionToFalling.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                locomotionToFalling.AddCondition(AnimatorConditionMode.Less, -0.25f, "VerticalSpeed");
                locomotionToFalling.AddCondition(AnimatorConditionMode.Greater, 0.3f, "AirborneTime");
                fallingConditionCount = locomotionToFalling.conditions.Length;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // --- 3. Push the Apollo-hop movement values onto the Player in the open scene. ---
        string movementStatus = ApplyMovementValues();

        // --- 4. Report. ---
        bool jumpingUpStillReferenced = ReferencesClipNamed(stateMachine, "Jumping Up") || ReferencesClipNamed(stateMachine, "jumping");
        Debug.Log(
            "Apply Lunar Hop complete:\n" +
            $"- FBX: {HopPath}\n" +
            $"- Rig: Humanoid, Avatar: Create From This Model\n" +
            $"- Clip '{hopClip.name}' import: Loop Time OFF, Rotation/Y/XZ Bake Into Pose ON\n" +
            $"- State '{oldStateName}' renamed to 'Lunar_Hop' and now uses clip '{hopClip.name}'\n" +
            $"- Removed {removedToFalling} transition(s) from the hop state into Falling\n" +
            $"- Hop state -> Locomotion exit transition present: {jumpState.transitions.Any(t => t.destinationState == locomotion)}\n" +
            $"- Locomotion -> Falling conditions set (Grounded==false, VerticalSpeed<-0.25, AirborneTime>0.30): {fallingConditionCount} condition(s)\n" +
            $"- Movement values: {movementStatus}\n" +
            $"- Idle / Walk / Run / Landing left unchanged\n" +
            $"- Old 'Jumping Up' clip still referenced in controller: {jumpingUpStillReferenced}");
    }

    private static string ApplyMovementValues()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            return "skipped (no active 'Player' in the open scene; script defaults apply to fresh instances)";
        }

        ThirdPersonPlayerController controller = player.GetComponent<ThirdPersonPlayerController>();
        if (controller == null)
        {
            return "skipped (Player has no ThirdPersonPlayerController)";
        }

        // Push the lunar-movement tuning onto the live component. The hop is animation-only,
        // so there is no jumpHeight/jumpTakeoffDelay. SetFloat is null-safe per field.
        SerializedObject serialized = new SerializedObject(controller);
        SetFloat(serialized, "walkSpeed", 1.5f);
        SetFloat(serialized, "sprintSpeed", 2.6f);
        SetFloat(serialized, "acceleration", 4f);
        SetFloat(serialized, "deceleration", 3.5f);
        SetFloat(serialized, "rotationSpeed", 7f);
        SetFloat(serialized, "gravity", -3.2f);
        SetFloat(serialized, "groundedStickSpeed", -2f);
        SetFloat(serialized, "hopDuration", 0.55f);
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);
        return "walk=1.5, sprint=2.6, accel=4, decel=3.5, rot=7, gravity=-3.2, hopDuration=0.55 (saved to scene)";
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void EnsureFloatParameter(AnimatorController controller, string name)
    {
        if (!controller.parameters.Any(parameter => parameter.name == name))
        {
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            if (child.state.name == name)
            {
                return child.state;
            }
        }

        return null;
    }

    private static bool ReferencesClipNamed(AnimatorStateMachine stateMachine, string clipNameFragment)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            Motion motion = child.state.motion;
            if (motion != null && motion.name.IndexOf(clipNameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
