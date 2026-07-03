using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-time fixer for the blocker issues found in the Player setup audit.
// Operates only on the "Player" GameObject and its "GroundCheck" child.
// It never touches "Astronaut Visual", the Animator, FBX import settings,
// or the Animator controller. Safe to delete after a successful run.
public static class FixPlayerBlockers
{
    [MenuItem("Tools/Regolith/Fix Player Blockers Only")]
    public static void Fix()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("Fix Player Blockers: No active GameObject named 'Player' found in the open scene. Nothing was changed.");
            return;
        }

        Undo.SetCurrentGroupName("Fix Player Blockers");
        int undoGroup = Undo.GetCurrentGroup();

        // 2. Remove conflicting / leftover components from Player only.
        bool removedRigidbody = RemoveIfPresent<Rigidbody>(player);
        bool removedCapsule = RemoveIfPresent<CapsuleCollider>(player);
        bool removedCapsuleMesh = RemoveCapsulePlaceholderMesh(player);

        // 4. Normalize the CharacterController (add only if somehow missing).
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<CharacterController>(player);
        }
        Undo.RecordObject(controller, "Fix CharacterController");
        controller.height = 2f;
        controller.radius = 0.45f;
        controller.center = new Vector3(0f, 1f, 0f);

        // 5. Place the Player so the capsule rests on the ground; reset scale.
        Undo.RecordObject(player.transform, "Fix Player Transform");
        Vector3 position = player.transform.position;
        position.y = 0f;
        player.transform.position = position;
        player.transform.localScale = Vector3.one;

        // 6. Ensure GroundCheck child exists and sits where grounding can see the floor.
        Transform groundCheck = player.transform.Find("GroundCheck");
        if (groundCheck == null)
        {
            GameObject groundCheckObject = new GameObject("GroundCheck");
            Undo.RegisterCreatedObjectUndo(groundCheckObject, "Create GroundCheck");
            Undo.SetTransformParent(groundCheckObject.transform, player.transform, "Parent GroundCheck");
            groundCheck = groundCheckObject.transform;
        }
        Undo.RecordObject(groundCheck, "Fix GroundCheck");
        groundCheck.localPosition = new Vector3(0f, 0.08f, 0f);
        groundCheck.localRotation = Quaternion.identity;

        // 7. Tag the Player.
        EnsureTag("Player");
        Undo.RecordObject(player, "Tag Player");
        player.tag = "Player";

        // 9. Persist.
        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);

        Undo.CollapseUndoOperations(undoGroup);

        // 10. Report.
        Debug.Log(
            "Fix Player Blockers complete:\n" +
            $"- Player position: {player.transform.position}\n" +
            $"- Player scale: {player.transform.localScale}\n" +
            $"- Player tag: {player.tag}\n" +
            $"- CharacterController center/height/radius: {controller.center} / {controller.height} / {controller.radius}\n" +
            $"- GroundCheck local position: {groundCheck.localPosition}\n" +
            $"- Removed Rigidbody: {removedRigidbody}\n" +
            $"- Removed CapsuleCollider: {removedCapsule}\n" +
            $"- Removed capsule MeshFilter+MeshRenderer: {removedCapsuleMesh}\n" +
            "- Astronaut Visual and its Animator were not touched.");
    }

    private static bool RemoveIfPresent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            return false;
        }

        Undo.DestroyObjectImmediate(component);
        return true;
    }

    // Removes the leftover capsule placeholder renderer/filter from Player, but only
    // when the MeshFilter is the built-in Unity "Capsule" mesh (never a real mesh).
    private static bool RemoveCapsulePlaceholderMesh(GameObject target)
    {
        MeshFilter filter = target.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        bool isCapsulePlaceholder = filter != null && (mesh == null || mesh.name == "Capsule");

        if (!isCapsulePlaceholder)
        {
            return false;
        }

        bool removed = false;
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Undo.DestroyObjectImmediate(renderer);
            removed = true;
        }

        Undo.DestroyObjectImmediate(filter);
        return removed || true;
    }

    // "Player" is a Unity built-in tag and normally already exists; this adds it
    // safely to the TagManager only if it is somehow missing.
    private static void EnsureTag(string tag)
    {
        foreach (string existing in UnityEditorInternal.InternalEditorUtility.tags)
        {
            if (existing == tag)
            {
                return;
            }
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets.Length == 0)
        {
            Debug.LogWarning($"Fix Player Blockers: could not open TagManager to add tag '{tag}'. Assign it manually if needed.");
            return;
        }

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
    }
}
