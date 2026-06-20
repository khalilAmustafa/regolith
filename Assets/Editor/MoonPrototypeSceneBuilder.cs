using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MoonPrototypeSceneBuilder
{
    private const string ScenePath = "Assets/_Scenes/Main.unity";
    private const string MaterialFolder = "Assets/_Materials";

    [MenuItem("Tools/Regolith/Create Prototype Scene")]
    public static void CreatePrototypeScene()
    {
        EnsureFolders();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material moonGrey = CreateMaterial("Moon Grey", new Color(0.45f, 0.45f, 0.45f));
        Material regolithDarkGrey = CreateMaterial("Regolith Dark Grey", new Color(0.18f, 0.18f, 0.18f));
        Material machineBlue = CreateMaterial("Machine Blue", new Color(0.1f, 0.35f, 0.9f), true);
        Material entranceGreen = CreateMaterial("Entrance Green", new Color(0.15f, 0.75f, 0.35f), true);
        Material playerWhite = CreateMaterial("Player White", new Color(0.92f, 0.92f, 0.88f));

        CreateGround(moonGrey);
        CreateGreyboxEnvironment(moonGrey);

        GameObject player = CreatePlayer(playerWhite);
        Camera camera = CreateCamera(player.transform);
        SimpleHUD hud = CreateHud();
        GameManager gameManager = CreateGameManager(hud);

        PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
        interactor.SetCamera(camera.transform);

        CreateRegolithPickups(regolithDarkGrey);
        CreateOxygenProcessor(machineBlue);
        CreateBaseEntrance(entranceGreen);
        CreateLighting();

        gameManager.SetHud(hud);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = player;
        Debug.Log($"Prototype scene created at {ScenePath}");
    }

    private static void EnsureFolders()
    {
        string[] folders =
        {
            "Assets/_Scenes",
            "Assets/_Scripts",
            "Assets/_Scripts/Player",
            "Assets/_Scripts/Game",
            "Assets/_Scripts/Interaction",
            "Assets/_Scripts/UI",
            "Assets/_Environment",
            "Assets/_Materials",
            "Assets/_Prefabs",
            "Assets/_Editor"
        };

        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
                string name = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }

    private static Material CreateMaterial(string materialName, Color color, bool emission = false)
    {
        string path = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;

        if (emission && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.8f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateGround(Material material)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Moon Ground";
        ground.transform.localScale = new Vector3(5f, 1f, 5f);
        SetMaterial(ground, material);
    }

    private static void CreateGreyboxEnvironment(Material material)
    {
        CreateCube("Ramp Block", new Vector3(-7f, 0.25f, 6f), new Vector3(5f, 0.5f, 3f), new Vector3(0f, 0f, -15f), material);
        CreateCube("Crate Stack A", new Vector3(6f, 1f, 5f), new Vector3(2f, 2f, 2f), Vector3.zero, material);
        CreateCube("Crate Stack B", new Vector3(8f, 0.6f, -3f), new Vector3(2.5f, 1.2f, 2.5f), Vector3.zero, material);
        CreateCylinder("Hab Support Cylinder", new Vector3(-5f, 1.25f, -6f), new Vector3(1.25f, 1.25f, 1.25f), material);
        CreateCylinder("Signal Mast", new Vector3(0f, 2.5f, 9f), new Vector3(0.35f, 2.5f, 0.35f), material);
    }

    private static GameObject CreatePlayer(Material material)
    {
        GameObject player = new GameObject("Player");
        player.name = "Player";
        player.transform.position = new Vector3(0f, 0f, -8f);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Astronaut Visual";
        visual.transform.SetParent(player.transform);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        visual.transform.localRotation = Quaternion.identity;
        Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
        SetMaterial(visual, material);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.45f;
        controller.center = new Vector3(0f, 1f, 0f);
        controller.stepOffset = 0.3f;
        controller.slopeLimit = 45f;

        GameObject groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(player.transform);
        groundCheck.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        groundCheck.transform.localRotation = Quaternion.identity;

        player.AddComponent<ThirdPersonPlayerController>();
        player.AddComponent<PlayerInventory>();
        player.AddComponent<PlayerInteractor>();

        return player;
    }

    private static Camera CreateCamera(Transform target)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = target.position + new Vector3(0f, 3f, -6f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;

        ThirdPersonCameraFollow follow = cameraObject.AddComponent<ThirdPersonCameraFollow>();
        follow.SetTarget(target);

        AudioListener listener = cameraObject.AddComponent<AudioListener>();
        _ = listener;

        return camera;
    }

    private static void CreateRegolithPickups(Material material)
    {
        Vector3[] positions =
        {
            new Vector3(-4f, 0.5f, -2f),
            new Vector3(-1.5f, 0.5f, 1.5f),
            new Vector3(2f, 0.5f, -1f),
            new Vector3(4.5f, 0.5f, 3.5f),
            new Vector3(-6f, 0.5f, 5f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pickup.name = $"Regolith Pickup {i + 1}";
            pickup.transform.position = positions[i];
            pickup.transform.localScale = Vector3.one * 0.7f;
            SetMaterial(pickup, material);
            pickup.AddComponent<RegolithPickup>();
        }
    }

    private static void CreateOxygenProcessor(Material material)
    {
        GameObject processor = CreateCylinder("Oxygen Processor", new Vector3(0f, 1f, 5f), new Vector3(1.5f, 1f, 1.5f), material);
        processor.AddComponent<OxygenProcessor>();

        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "Processor Beacon";
        beacon.transform.SetParent(processor.transform);
        beacon.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        beacon.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);
        SetMaterial(beacon, material);
    }

    private static void CreateBaseEntrance(Material material)
    {
        GameObject frame = CreateCube("Base Entrance Frame", new Vector3(0f, 2f, 12f), new Vector3(5f, 4f, 0.6f), Vector3.zero, material);
        Object.DestroyImmediate(frame.GetComponent<BoxCollider>());

        GameObject door = CreateCube("Base Door", new Vector3(0f, 1.5f, 11.65f), new Vector3(2.6f, 3f, 0.35f), Vector3.zero, material);

        GameObject trigger = new GameObject("Base Entrance");
        trigger.transform.position = new Vector3(0f, 1.5f, 10.8f);
        BoxCollider collider = trigger.AddComponent<BoxCollider>();
        collider.size = new Vector3(3f, 3f, 1.5f);
        collider.isTrigger = true;

        BaseEntrance entrance = trigger.AddComponent<BaseEntrance>();
        entrance.SetDoor(door.transform);
    }

    private static GameManager CreateGameManager(SimpleHUD hud)
    {
        GameObject gameManagerObject = new GameObject("Game Manager");
        GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
        gameManager.SetHud(hud);
        return gameManager;
    }

    private static SimpleHUD CreateHud()
    {
        GameObject canvasObject = new GameObject("HUD Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        SimpleHUD hud = canvasObject.AddComponent<SimpleHUD>();

        Text objective = CreateHudText(canvasObject.transform, "Objective Text", new Vector2(20f, -20f), new Vector2(460f, 36f), 22, TextAnchor.UpperLeft);
        Text regolith = CreateHudText(canvasObject.transform, "Regolith Text", new Vector2(20f, -58f), new Vector2(220f, 30f), 18, TextAnchor.UpperLeft);
        Text oxygen = CreateHudText(canvasObject.transform, "Oxygen Text", new Vector2(20f, -88f), new Vector2(220f, 30f), 18, TextAnchor.UpperLeft);
        Text prompt = CreateHudText(canvasObject.transform, "Prompt Text", new Vector2(0f, 92f), new Vector2(520f, 44f), 22, TextAnchor.MiddleCenter);
        Text win = CreateHudText(canvasObject.transform, "Win Text", Vector2.zero, new Vector2(650f, 80f), 34, TextAnchor.MiddleCenter);

        AnchorTopLeft(objective.rectTransform);
        AnchorTopLeft(regolith.rectTransform);
        AnchorTopLeft(oxygen.rectTransform);
        AnchorBottomCenter(prompt.rectTransform);
        AnchorCenter(win.rectTransform);

        hud.Bind(regolith, oxygen, prompt, objective, win);
        return hud;
    }

    private static Text CreateHudText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = name;

        RectTransform rect = text.rectTransform;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        return text;
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Vector3 rotation, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.transform.eulerAngles = rotation;
        SetMaterial(cube, material);
        return cube;
    }

    private static GameObject CreateCylinder(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.position = position;
        cylinder.transform.localScale = scale;
        SetMaterial(cylinder, material);
        return cylinder;
    }

    private static void SetMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static void AnchorTopLeft(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
    }

    private static void AnchorBottomCenter(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
    }

    private static void AnchorCenter(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
