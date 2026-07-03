using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MicrometeoriteOpeningCinematicSetupUtility
{
    private const string MenuPath = "Tools/Regolith/Setup Micrometeorite Opening Cinematic";
    private const string ControllerName = "MicrometeoriteOpeningCinematic";
    private const string RejectedRockPath = "Assets/_External/moon/Models/Rocks/rock-00-8-grey-white-rock-models-with-lods/source/rock_00/rock_00/low_poly_models/rock_00H_lp.fbx";
    private const string PreferredSingleRockPath = "Assets/_External/moon/Models/Rocks/rock-00-8-grey-white-rock-models-with-lods/source/rock_00/rock_00/models/2k_models/rock_00_2k.fbx";
    private const string PreferredRockTexturePath = "Assets/_External/moon/Models/Rocks/rock-00-8-grey-white-rock-models-with-lods/source/rock_00/rock_00/Textures/rock_00_2k.png";
    private const string RockMaterialPath = "Assets/_Materials/Damage/Micrometeorite_DarkRock_Mat.mat";
    private const string CenteredRockMeshPath = "Assets/_Generated/Micrometeorite_Visible_SingleRock.asset";

    [MenuItem(MenuPath)]
    public static void SetupMicrometeoriteOpeningCinematic()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Micrometeorite cinematic setup cannot run during Play Mode. Exit Play Mode and run it again.");
            return;
        }

        GameObject habitat = GameObject.Find("Habitat_01");
        if (habitat == null)
        {
            Debug.LogError("Micrometeorite cinematic setup stopped: Habitat_01 was not found.");
            return;
        }

        Bounds habitatBounds = CalculateRendererBounds(habitat);
        HabitatDamageVisualState damageState = EnsureDamageState(habitat);
        Transform impactPoint = GetOrCreateChildTransform("Asteroid_ImpactPoint");
        Transform startPoint = GetOrCreateChildTransform("Asteroid_StartPoint");

        PositionImpactAndStartPoints(habitat, habitatBounds, damageState, impactPoint, startPoint);

        Transform meteorite = CreateOrUpdateMeteorite(startPoint);
        Camera asteroidFollowCamera = CreateOrUpdateCamera("Camera_AsteroidFollow", startPoint.position, impactPoint.position, 36f);
        Camera impactCamera = CreateOrUpdateImpactCamera(habitatBounds, impactPoint.position);
        GameObject impactFX = CreateOrUpdateImpactFx(impactPoint);

        GameObject controllerObject = GameObject.Find(ControllerName);
        if (controllerObject == null)
        {
            controllerObject = new GameObject(ControllerName);
            Undo.RegisterCreatedObjectUndo(controllerObject, "Create micrometeorite cinematic controller");
        }

        MicrometeoriteOpeningCinematic cinematic = controllerObject.GetComponent<MicrometeoriteOpeningCinematic>();
        if (cinematic == null)
        {
            cinematic = Undo.AddComponent<MicrometeoriteOpeningCinematic>(controllerObject);
        }
        else
        {
            Undo.RecordObject(cinematic, "Configure micrometeorite cinematic");
        }

        cinematic.micrometeorite = meteorite;
        cinematic.asteroidStartPoint = startPoint;
        cinematic.asteroidImpactPoint = impactPoint;
        cinematic.asteroidFollowCamera = asteroidFollowCamera;
        cinematic.impactCamera = impactCamera;
        cinematic.mainGameplayCamera = Camera.main;
        cinematic.impactFX = impactFX;
        cinematic.impactAudio = impactPoint.GetComponent<AudioSource>();
        cinematic.habitatDamageVisualState = damageState;
        cinematic.startDelay = 1.5f;
        cinematic.travelDuration = 2.0f;
        cinematic.cameraSwitchDistance = 5f;
        cinematic.postImpactHoldTime = 1.5f;
        cinematic.followCameraLocalOffset = new Vector3(2.3f, 0.65f, -2.3f);
        cinematic.xAxisSpinDegreesPerSecond = 35f;
        cinematic.playOnStart = true;
        cinematic.debugKey = KeyCode.I;

        if (damageState != null)
        {
            damageState.ShowNormal();
            EditorUtility.SetDirty(damageState);
        }

        meteorite.gameObject.SetActive(false);
        asteroidFollowCamera.enabled = false;
        impactCamera.enabled = false;
        impactFX.SetActive(false);

        EditorUtility.SetDirty(cinematic);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Micrometeorite opening cinematic setup complete:\n" +
            "- Micrometeorite_01 created/assigned: " + (meteorite != null ? "yes" : "no") + "\n" +
            "- Asteroid_StartPoint assigned: yes\n" +
            "- Asteroid_ImpactPoint assigned: yes\n" +
            "- Camera_AsteroidFollow assigned: yes\n" +
            "- Camera_ImpactView assigned: yes\n" +
            "- ImpactFX_Dust assigned: yes\n" +
            "- HabitatDamageVisualState assigned: " + (damageState != null ? "yes" : "no") + "\n" +
            "- Gathering animation untouched: yes");
    }

    private static HabitatDamageVisualState EnsureDamageState(GameObject habitat)
    {
        HabitatDamageVisualState state = habitat.GetComponent<HabitatDamageVisualState>();
        if (state == null)
        {
            state = Undo.AddComponent<HabitatDamageVisualState>(habitat);
        }
        else
        {
            Undo.RecordObject(state, "Configure habitat damage visual state");
        }

        if (state.crackObject == null)
        {
            Transform crack = habitat.transform.Find("Crack_Impact_01") ?? FindSceneTransform("Crack_Impact_01");
            if (crack != null)
            {
                state.crackObject = crack.gameObject;
            }
        }

        if (state.concretePatchObject == null)
        {
            Transform patch = habitat.transform.Find("ConcretePatch_01") ?? FindSceneTransform("ConcretePatch_01");
            if (patch != null)
            {
                state.concretePatchObject = patch.gameObject;
            }
        }

        return state;
    }

    private static void PositionImpactAndStartPoints(
        GameObject habitat,
        Bounds habitatBounds,
        HabitatDamageVisualState damageState,
        Transform impactPoint,
        Transform startPoint)
    {
        Vector3 right = habitat.transform.right.normalized;
        Vector3 up = habitat.transform.up.normalized;
        Vector3 forward = habitat.transform.forward.normalized;

        Vector3 impactPosition;
        Quaternion impactRotation;

        if (damageState != null && damageState.crackObject != null)
        {
            impactPosition = damageState.crackObject.transform.position;
            impactRotation = damageState.crackObject.transform.rotation;
        }
        else
        {
            impactPosition = habitatBounds.center
                + right * (Mathf.Max(1f, habitatBounds.extents.x) + 0.06f)
                + up * Mathf.Clamp(habitatBounds.extents.y * 0.16f, 0.4f, 1.2f)
                - forward * Mathf.Clamp(habitatBounds.extents.z * 0.18f, 0.35f, 1.2f);
            impactRotation = Quaternion.LookRotation(right, up);
        }

        Undo.RecordObject(impactPoint, "Place asteroid impact point");
        impactPoint.position = impactPosition;
        impactPoint.rotation = impactRotation;

        Vector3 incoming = (-right * 18f) + (forward * 14f) + (up * 13f);
        Undo.RecordObject(startPoint, "Place asteroid start point");
        startPoint.position = impactPosition + incoming;
        startPoint.rotation = Quaternion.LookRotation((impactPosition - startPoint.position).normalized, Vector3.up);

        EditorUtility.SetDirty(impactPoint);
        EditorUtility.SetDirty(startPoint);
    }

    private static Transform CreateOrUpdateMeteorite(Transform startPoint)
    {
        GameObject existing = GameObject.Find("Micrometeorite_01");
        GameObject meteorite = existing;
        RockMeshSelection rockMesh = FindSingleRockMesh();

        if (meteorite != null)
        {
            Debug.Log("Rebuilding Micrometeorite_01 as one centered, visible rock mesh.");
            Undo.DestroyObjectImmediate(meteorite);
            meteorite = null;
        }

        if (meteorite == null)
        {
            meteorite = new GameObject("Micrometeorite_01");
            Undo.RegisterCreatedObjectUndo(meteorite, "Create micrometeorite");
            Undo.AddComponent<MeshFilter>(meteorite);
            Undo.AddComponent<MeshRenderer>(meteorite);
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(meteorite, "Update micrometeorite");
        }

        MeshFilter meshFilter = meteorite.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = Undo.AddComponent<MeshFilter>(meteorite);
        }

        MeshRenderer meshRenderer = meteorite.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = Undo.AddComponent<MeshRenderer>(meteorite);
        }

        Mesh visibleMesh;
        if (rockMesh.Mesh != null)
        {
            visibleMesh = CreateOrUpdateCenteredRockMesh(rockMesh.Mesh);
            Debug.Log("Micrometeorite visual uses one centered rock mesh: " + rockMesh.Mesh.name + " from " + rockMesh.AssetPath);
        }
        else
        {
            visibleMesh = CreateFallbackMeteoriteMesh();
            Debug.LogWarning("No suitable rock mesh found. Micrometeorite_01 uses one generated fallback rock mesh.");
        }

        meshFilter.sharedMesh = visibleMesh;
        meshRenderer.sharedMaterial = EnsureDarkRockMaterialAsset();
        meshRenderer.enabled = true;

        meteorite.transform.position = startPoint.position;
        meteorite.transform.rotation = startPoint.rotation;
        meteorite.transform.localScale = CalculateMeteoriteScale(meshFilter.sharedMesh, 1.6f);

        RemoveRuntimePhysics(meteorite);
        return meteorite.transform;
    }

    private static RockMeshSelection FindSingleRockMesh()
    {
        RockMeshSelection preferred = FindLargestMeshInAsset(PreferredSingleRockPath);
        if (preferred.Mesh != null)
        {
            return preferred;
        }

        Debug.LogWarning("Preferred single micrometeorite rock mesh missing at " + PreferredSingleRockPath + ". Searching other rock meshes.");

        return AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/_External/moon/Models/Rocks" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => IsRockPath(path))
            .Where(path => path.Replace("\\", "/") != RejectedRockPath)
            .Select(FindLargestMeshInAsset)
            .Where(selection => selection.Mesh != null)
            .OrderByDescending(selection => selection.Mesh.vertexCount)
            .FirstOrDefault();
    }

    private static RockMeshSelection FindLargestMeshInAsset(string assetPath)
    {
        string normalizedPath = assetPath.Replace("\\", "/");
        Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(normalizedPath)
            .OfType<Mesh>()
            .Where(candidate => candidate != null && candidate.vertexCount > 12)
            .OrderByDescending(candidate => candidate.bounds.size.sqrMagnitude)
            .FirstOrDefault();

        return new RockMeshSelection(mesh, normalizedPath);
    }

    private static bool IsRockPath(string path)
    {
        string lower = path.ToLowerInvariant();
        return lower.Contains("rock")
            || lower.Contains("asteroid")
            || lower.Contains("boulder")
            || lower.Contains("stone");
    }

    private static Material EnsureDarkRockMaterialAsset()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RockMaterialPath);
        if (material == null)
        {
            EnsureAssetFolder("Assets/_Materials");
            EnsureAssetFolder("Assets/_Materials/Damage");
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.name = "Micrometeorite_DarkRock_Mat";
            AssetDatabase.CreateAsset(material, RockMaterialPath);
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        Texture2D rockTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PreferredRockTexturePath);
        if (rockTexture != null)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", rockTexture);
            }
            else if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", rockTexture);
            }
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.18f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh CreateOrUpdateCenteredRockMesh(Mesh sourceMesh)
    {
        EnsureAssetFolder("Assets/_Generated");

        Mesh centeredMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CenteredRockMeshPath);
        if (centeredMesh == null)
        {
            centeredMesh = UnityEngine.Object.Instantiate(sourceMesh);
            centeredMesh.name = "Micrometeorite_Visible_SingleRock";
            AssetDatabase.CreateAsset(centeredMesh, CenteredRockMeshPath);
        }
        else
        {
            EditorUtility.CopySerialized(sourceMesh, centeredMesh);
            centeredMesh.name = "Micrometeorite_Visible_SingleRock";
        }

        Vector3 centerOffset = centeredMesh.bounds.center;
        Vector3[] vertices = centeredMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] -= centerOffset;
        }

        Vector3 pointiestDirection = FindPointiestDirection(vertices);
        if (pointiestDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion pointForwardRotation = Quaternion.FromToRotation(pointiestDirection.normalized, Vector3.forward);
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = pointForwardRotation * vertices[i];
            }
        }

        centeredMesh.vertices = vertices;
        centeredMesh.RecalculateBounds();
        centeredMesh.RecalculateNormals();
        EditorUtility.SetDirty(centeredMesh);
        AssetDatabase.SaveAssets();
        return centeredMesh;
    }

    private static Vector3 FindPointiestDirection(Vector3[] vertices)
    {
        Vector3 pointiest = Vector3.forward;
        float largestDistance = 0f;

        for (int i = 0; i < vertices.Length; i++)
        {
            float distance = vertices[i].sqrMagnitude;
            if (distance > largestDistance)
            {
                largestDistance = distance;
                pointiest = vertices[i];
            }
        }

        return pointiest;
    }

    private static Vector3 CalculateMeteoriteScale(Mesh mesh, float targetDiameter)
    {
        if (mesh == null)
        {
            return Vector3.one * targetDiameter;
        }

        float largestMeshDimension = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z);
        if (largestMeshDimension <= 0.0001f)
        {
            return Vector3.one * targetDiameter;
        }

        return Vector3.one * (targetDiameter / largestMeshDimension);
    }

    private static Mesh CreateFallbackMeteoriteMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh mesh = UnityEngine.Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
        mesh.name = "Generated_Micrometeorite_Rock";
        UnityEngine.Object.DestroyImmediate(temp);
        return mesh;
    }

    private static void EnsureAssetFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folderName = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static void RemoveRuntimePhysics(GameObject root)
    {
        foreach (Rigidbody rigidbody in root.GetComponentsInChildren<Rigidbody>(true))
        {
            UnityEngine.Object.DestroyImmediate(rigidbody);
        }

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
    }

    private static Camera CreateOrUpdateCamera(string name, Vector3 from, Vector3 lookAt, float fov)
    {
        GameObject cameraObject = GameObject.Find(name);
        if (cameraObject == null)
        {
            cameraObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create cinematic camera");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(cameraObject, "Update cinematic camera");
        }

        Camera camera = cameraObject.GetComponent<Camera>();
        if (camera == null)
        {
            camera = Undo.AddComponent<Camera>(cameraObject);
        }

        camera.fieldOfView = fov;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 500f;
        cameraObject.transform.position = from;
        cameraObject.transform.LookAt(lookAt);
        camera.enabled = false;
        return camera;
    }

    private static Camera CreateOrUpdateImpactCamera(Bounds habitatBounds, Vector3 impactPosition)
    {
        Vector3 viewDirection = (Camera.main != null ? Camera.main.transform.forward : Vector3.forward);
        viewDirection.y = 0f;
        if (viewDirection.sqrMagnitude < 0.01f)
        {
            viewDirection = Vector3.forward;
        }

        Vector3 position = habitatBounds.center - viewDirection.normalized * 12f + Vector3.up * 4f;
        return CreateOrUpdateCamera("Camera_ImpactView", position, impactPosition, 42f);
    }

    private static GameObject CreateOrUpdateImpactFx(Transform impactPoint)
    {
        GameObject fx = GameObject.Find("ImpactFX_Dust");
        if (fx == null)
        {
            fx = new GameObject("ImpactFX_Dust");
            Undo.RegisterCreatedObjectUndo(fx, "Create impact dust FX");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(fx, "Update impact dust FX");
        }

        fx.transform.position = impactPoint.position;
        fx.transform.rotation = impactPoint.rotation;

        ParticleSystem particles = fx.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = Undo.AddComponent<ParticleSystem>(fx);
        }

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
        main.startColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 48) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = 0.25f;

        AudioSource audio = impactPoint.GetComponent<AudioSource>();
        if (audio == null)
        {
            audio = Undo.AddComponent<AudioSource>(impactPoint.gameObject);
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.volume = 0.75f;
        }

        fx.SetActive(false);
        return fx;
    }

    private static Transform GetOrCreateChildTransform(string name)
    {
        GameObject found = GameObject.Find(name);
        if (found != null)
        {
            return found.transform;
        }

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, "Create cinematic transform");
        return created.transform;
    }

    private static Transform FindSceneTransform(string name)
    {
        GameObject obj = GameObject.Find(name);
        return obj != null ? obj.transform : null;
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(root.transform.position, Vector3.one);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer.gameObject.name == "Crack_Impact_01" || renderer.gameObject.name == "ConcretePatch_01")
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private readonly struct RockMeshSelection
    {
        public readonly Mesh Mesh;
        public readonly string AssetPath;

        public RockMeshSelection(Mesh mesh, string assetPath)
        {
            Mesh = mesh;
            AssetPath = assetPath;
        }
    }
}
