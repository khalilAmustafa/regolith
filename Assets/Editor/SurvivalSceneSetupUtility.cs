using System;
using UnityEditor;
using UnityEngine;

public static class SurvivalSceneSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Survival MVP";

    [MenuItem(MenuPath)]
    public static void SetupSurvivalMVP()
    {
        GameObject player = FindPlayer();
        if (player == null)
        {
            Debug.LogWarning("Setup Survival MVP could not find a Player. Tag a player object as Player, select it, or ensure ThirdPersonPlayerController exists in the scene.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Setup Survival MVP Player");
        PlayerSurvivalResources resources = EnsureComponent<PlayerSurvivalResources>(player);
        EnsureComponent<PlayerInteractionController>(player);

        SurvivalHUD hud = UnityEngine.Object.FindAnyObjectByType<SurvivalHUD>();
        if (hud == null)
        {
            Undo.IncrementCurrentGroup();
            hud = SurvivalHUDBuilder.CreateSurvivalHUD();
            Undo.RegisterCreatedObjectUndo(hud.gameObject, "Create Survival HUD");
        }

        if (hud != null && hud.player == null)
        {
            Undo.RecordObject(hud, "Assign Survival HUD Player");
            hud.player = resources;
            EditorUtility.SetDirty(hud);
        }

        SetupNamedSceneObjects();

        Debug.Log("Survival MVP setup complete. Review logged candidates and assign Airlock Animator parameters if your controller uses custom names.");
    }

    private static GameObject FindPlayer()
    {
        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            return taggedPlayer;
        }

        ThirdPersonPlayerController controller = UnityEngine.Object.FindAnyObjectByType<ThirdPersonPlayerController>();
        if (controller != null)
        {
            return controller.gameObject;
        }

        if (Selection.activeGameObject != null)
        {
            Debug.LogWarning("No object tagged Player or ThirdPersonPlayerController found. Using selected object as Player: " + Selection.activeGameObject.name);
            return Selection.activeGameObject;
        }

        return null;
    }

    private static void SetupNamedSceneObjects()
    {
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
        foreach (GameObject sceneObject in objects)
        {
            if (sceneObject == null || string.IsNullOrWhiteSpace(sceneObject.name))
            {
                continue;
            }

            string lowerName = sceneObject.name.ToLowerInvariant();
            if (IsGeneratedTrigger(sceneObject))
            {
                continue;
            }

            if (lowerName.Contains("regolith"))
            {
                SetupTriggerChild<RegolithCollectionZone>(sceneObject, "Regolith_InteractTrigger", new Vector3(4f, 2f, 4f), new Vector3(0f, 1f, 0f));
            }
            else if (lowerName.Contains("solar"))
            {
                SetupTriggerChild<SolarPowerInteractable>(sceneObject, "Solar_InteractTrigger", new Vector3(4f, 2f, 3f), new Vector3(0f, 1f, 1.5f));
            }
            else if (lowerName.Contains("extractor") || lowerName.Contains("o2"))
            {
                SetupTriggerChild<O2ExtractorInteractable>(sceneObject, "O2Extractor_InteractTrigger", new Vector3(3f, 2f, 3f), new Vector3(0f, 1f, 1.5f));
            }
            else if (lowerName.Contains("airlock") || lowerName.Contains("door"))
            {
                AirlockController airlock = SetupTriggerChild<AirlockController>(sceneObject, "Airlock_InteractTrigger", new Vector3(3f, 2.5f, 3f), new Vector3(0f, 1.25f, 1.5f));
                if (airlock != null && airlock.airlockAnimator == null)
                {
                    Undo.RecordObject(airlock, "Assign Airlock Animator");
                    airlock.airlockAnimator = sceneObject.GetComponentInChildren<Animator>();
                    EditorUtility.SetDirty(airlock);
                }
            }
        }
    }

    private static T SetupTriggerChild<T>(GameObject parent, string childName, Vector3 size, Vector3 localPosition) where T : Component
    {
        if (!IsObviousCandidate(parent, childName))
        {
            Debug.LogWarning("Skipping uncertain setup candidate: " + parent.name);
            return null;
        }

        Transform existingChild = parent.transform.Find(childName);
        GameObject triggerObject;

        if (existingChild != null)
        {
            triggerObject = existingChild.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(triggerObject, "Update Survival Trigger");
        }
        else
        {
            triggerObject = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(triggerObject, "Create Survival Trigger");
            triggerObject.transform.SetParent(parent.transform, false);
            triggerObject.transform.localPosition = localPosition;
            triggerObject.transform.localRotation = Quaternion.identity;
            triggerObject.transform.localScale = Vector3.one;
            Debug.Log("Created survival trigger " + childName + " under " + parent.name);
        }

        BoxCollider trigger = triggerObject.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BoxCollider>(triggerObject);
        }

        Undo.RecordObject(trigger, "Configure Survival Trigger Collider");
        trigger.isTrigger = true;
        trigger.size = size;
        trigger.center = Vector3.zero;
        EditorUtility.SetDirty(trigger);

        T component = triggerObject.GetComponent<T>();
        if (component == null)
        {
            component = Undo.AddComponent<T>(triggerObject);
            Debug.Log("Added " + typeof(T).Name + " to " + triggerObject.name);
        }
        else
        {
            Debug.Log("Found existing " + typeof(T).Name + " on " + triggerObject.name);
        }

        EditorUtility.SetDirty(component);
        return component;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        component = Undo.AddComponent<T>(target);
        Debug.Log("Added " + typeof(T).Name + " to " + target.name);
        return component;
    }

    private static bool IsObviousCandidate(GameObject candidate, string childName)
    {
        if (candidate.transform.Find(childName) != null)
        {
            return true;
        }

        Renderer renderer = candidate.GetComponentInChildren<Renderer>();
        Collider collider = candidate.GetComponentInChildren<Collider>();
        return renderer != null || collider != null;
    }

    private static bool IsGeneratedTrigger(GameObject sceneObject)
    {
        return sceneObject.name.Equals("Regolith_InteractTrigger", StringComparison.Ordinal)
            || sceneObject.name.Equals("Solar_InteractTrigger", StringComparison.Ordinal)
            || sceneObject.name.Equals("O2Extractor_InteractTrigger", StringComparison.Ordinal)
            || sceneObject.name.Equals("Airlock_InteractTrigger", StringComparison.Ordinal);
    }
}
