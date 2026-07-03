using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class LunarBasinSceneSetup
{
    private const string TargetScenePath = "Assets/saved.unity";
    private const string TerrainDataPath = "Assets/_Environment/LunarBasinTerrainData.asset";
    private const string RockParentName = "Generated_Edge_Rocks";

    [MenuItem("Tools/Regolith/Generate Lunar Basin In Game Scene")]
    public static void GenerateLunarBasinInGameScene()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        LunarBasinTerrainGenerator generator = EnsureGenerator();
        generator.GenerateTerrain();

        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain != null)
        {
            AssignGroundLayer(terrain.gameObject);
            SaveTerrainDataAsset(terrain);
        }

        DisableOldMoonGround();
        PositionPlayerOnTerrain(terrain);
        EnsureVaultEntrance(terrain);
        CreateOrPositionEdgeRocks(terrain);
        ApplyLunarNightLighting();
        MakeTargetSceneBuildScene();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Generated lunar basin terrain in Assets/saved.unity.");
    }

    private static LunarBasinTerrainGenerator EnsureGenerator()
    {
        LunarBasinTerrainGenerator generator = Object.FindAnyObjectByType<LunarBasinTerrainGenerator>();
        if (generator != null)
        {
            generator.transform.position = Vector3.zero;
            return generator;
        }

        GameObject generatorObject = new GameObject("Lunar Basin Generator");
        generatorObject.transform.position = Vector3.zero;
        return generatorObject.AddComponent<LunarBasinTerrainGenerator>();
    }

    private static void AssignGroundLayer(GameObject terrainObject)
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
        {
            Debug.LogWarning("Ground layer not found. Terrain was generated, but player ground checks may need a Ground layer.");
            return;
        }

        terrainObject.layer = groundLayer;
    }

    private static void SaveTerrainDataAsset(Terrain terrain)
    {
        if (terrain.terrainData == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(terrain.terrainData)))
        {
            return;
        }

        string folder = Path.GetDirectoryName(TerrainDataPath).Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "_Environment");
        }

        AssetDatabase.CreateAsset(terrain.terrainData, TerrainDataPath);
    }

    private static void DisableOldMoonGround()
    {
        GameObject oldGround = GameObject.Find("Moon Ground");
        if (oldGround != null)
        {
            oldGround.SetActive(false);
        }
    }

    private static void PositionPlayerOnTerrain(Terrain terrain)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            player = GameObject.Find("Player");
        }

        if (player == null)
        {
            return;
        }

        Vector3 position = new Vector3(0f, 0f, -8f);
        if (terrain != null)
        {
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.05f;
        }

        player.transform.position = position;
    }

    private static void EnsureVaultEntrance(Terrain terrain)
    {
        const float vaultZ = 24.8f;
        const float platformHeight = 2f;

        BaseEntrance entrance = Object.FindAnyObjectByType<BaseEntrance>();
        Transform door = entrance != null ? FindDoorForEntrance(entrance.transform) : null;

        if (entrance == null)
        {
            door = CreateVaultDoor(platformHeight, vaultZ);
            entrance = CreateVaultTrigger(platformHeight, vaultZ, door);
        }
        else
        {
            entrance.transform.position = new Vector3(0f, platformHeight + 1.5f, vaultZ - 2.1f);
            if (door != null)
            {
                door.position = new Vector3(0f, platformHeight + 1.6f, vaultZ + 0.2f);
                SetMaterial(door.gameObject, CreateOrUpdateMaterial("Vault Door Metal", new Color32(0x2a, 0x2d, 0x30, 0xff), 0.25f, 0.18f));
            }
        }

        CreateOrPositionVaultFrame(platformHeight, vaultZ);
        CreateOrPositionVaultGuideLights(platformHeight, vaultZ);

        if (door != null)
        {
            entrance.SetDoor(door);
        }

        if (terrain != null)
        {
            AssignGroundLayer(terrain.gameObject);
        }
    }

    private static Transform FindDoorForEntrance(Transform entranceTransform)
    {
        GameObject door = GameObject.Find("Vault Door");
        if (door == null)
        {
            door = GameObject.Find("Base Door");
        }

        return door != null ? door.transform : null;
    }

    private static Transform CreateVaultDoor(float platformHeight, float vaultZ)
    {
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Vault Door";
        door.transform.position = new Vector3(0f, platformHeight + 1.6f, vaultZ + 0.2f);
        door.transform.localScale = new Vector3(3.2f, 3.2f, 0.35f);
        SetMaterial(door, CreateOrUpdateMaterial("Vault Door Metal", new Color32(0x2a, 0x2d, 0x30, 0xff), 0.25f, 0.18f));
        return door.transform;
    }

    private static BaseEntrance CreateVaultTrigger(float platformHeight, float vaultZ, Transform door)
    {
        GameObject trigger = new GameObject("Base Entrance");
        trigger.transform.position = new Vector3(0f, platformHeight + 1.5f, vaultZ - 2.1f);

        BoxCollider collider = trigger.AddComponent<BoxCollider>();
        collider.size = new Vector3(4.5f, 3.5f, 2f);
        collider.isTrigger = true;

        BaseEntrance entrance = trigger.AddComponent<BaseEntrance>();
        entrance.SetDoor(door);
        return entrance;
    }

    private static void CreateOrPositionVaultFrame(float platformHeight, float vaultZ)
    {
        GameObject frame = GameObject.Find("Vault Entrance Frame");
        if (frame == null)
        {
            frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Vault Entrance Frame";
            Object.DestroyImmediate(frame.GetComponent<BoxCollider>());
        }

        frame.transform.position = new Vector3(0f, platformHeight + 2f, vaultZ + 0.45f);
        frame.transform.localScale = new Vector3(7.8f, 4.4f, 0.8f);
        SetMaterial(frame, CreateOrUpdateMaterial("Vault Frame Metal", new Color32(0x82, 0x86, 0x86, 0xff), 0.05f, 0.12f));
    }

    private static void CreateOrPositionVaultGuideLights(float platformHeight, float vaultZ)
    {
        Material lightMaterial = CreateOrUpdateMaterial("Vault Orange Guide Light", new Color32(0xff, 0x8a, 0x22, 0xff), 0f, 0f, new Color(1f, 0.38f, 0.06f) * 4f);

        CreateOrPositionGuideLight("Vault Guide Light L", new Vector3(-2.45f, platformHeight + 2f, vaultZ - 0.05f), lightMaterial);
        CreateOrPositionGuideLight("Vault Guide Light R", new Vector3(2.45f, platformHeight + 2f, vaultZ - 0.05f), lightMaterial);
        CreateOrPositionPointLight("Vault Warm Area Light", new Vector3(0f, platformHeight + 3.2f, vaultZ - 3.2f), new Color(1f, 0.62f, 0.28f), 3.2f, 9f);
    }

    private static void CreateOrPositionGuideLight(string name, Vector3 position, Material material)
    {
        GameObject guide = GameObject.Find(name);
        if (guide == null)
        {
            guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = name;
            Object.DestroyImmediate(guide.GetComponent<BoxCollider>());
        }

        guide.transform.position = position;
        guide.transform.localScale = new Vector3(0.38f, 1.6f, 0.18f);
        SetMaterial(guide, material);
    }

    private static void CreateOrPositionPointLight(string name, Vector3 position, Color color, float intensity, float range)
    {
        GameObject lightObject = GameObject.Find(name);
        if (lightObject == null)
        {
            lightObject = new GameObject(name);
            lightObject.AddComponent<Light>();
        }

        lightObject.transform.position = position;

        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
    }

    private static void CreateOrPositionEdgeRocks(Terrain terrain)
    {
        if (terrain == null)
        {
            return;
        }

        GameObject existing = GameObject.Find(RockParentName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject parent = new GameObject(RockParentName);
        Material rockMaterial = CreateOrUpdateMaterial("Lunar Rock Grey", new Color32(0x66, 0x66, 0x64, 0xff), 0f, 0.03f);
        System.Random random = new System.Random(86420);

        for (int i = 0; i < 18; i++)
        {
            float angle = Mathf.Lerp(0f, Mathf.PI * 2f, i / 18f) + Range(random, -0.12f, 0.12f);
            float radius = Range(random, 20.5f, 28f);
            Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            if (Vector3.Distance(position, new Vector3(0f, 0f, 24.8f)) < 7.5f)
            {
                position.x += Mathf.Sign(position.x == 0f ? 1f : position.x) * 7.5f;
            }

            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.15f;

            GameObject rock = new GameObject($"Lunar Edge Rock {i + 1:00}");
            rock.name = $"Lunar Edge Rock {i + 1:00}";
            rock.transform.SetParent(parent.transform);
            rock.transform.position = position;
            rock.transform.rotation = Quaternion.Euler(Range(random, -12f, 12f), Range(random, 0f, 360f), Range(random, -12f, 12f));
            rock.transform.localScale = new Vector3(Range(random, 0.8f, 2.2f), Range(random, 0.45f, 1.35f), Range(random, 0.8f, 2.4f));
            rock.AddComponent<MeshFilter>().sharedMesh = CreateLowPolyRockMesh(random);
            rock.AddComponent<MeshRenderer>();
            SetMaterial(rock, rockMaterial);
        }
    }

    private static Mesh CreateLowPolyRockMesh(System.Random random)
    {
        Vector3[] vertices =
        {
            new Vector3(0f, 0.62f, 0f) * Range(random, 0.8f, 1.25f),
            new Vector3(0f, -0.48f, 0f) * Range(random, 0.75f, 1.1f),
            new Vector3(0.72f, 0.05f, 0f) * Range(random, 0.75f, 1.3f),
            new Vector3(-0.66f, -0.02f, 0f) * Range(random, 0.75f, 1.25f),
            new Vector3(0f, 0.04f, 0.78f) * Range(random, 0.72f, 1.35f),
            new Vector3(0f, -0.04f, -0.7f) * Range(random, 0.72f, 1.28f),
            new Vector3(0.42f, 0.16f, 0.45f) * Range(random, 0.75f, 1.2f),
            new Vector3(-0.38f, 0.1f, -0.44f) * Range(random, 0.75f, 1.2f)
        };

        int[] triangles =
        {
            0, 2, 6,
            0, 6, 4,
            0, 4, 3,
            0, 3, 7,
            0, 7, 5,
            0, 5, 2,
            1, 6, 2,
            1, 4, 6,
            1, 3, 4,
            1, 7, 3,
            1, 5, 7,
            1, 2, 5,
            2, 4, 5,
            3, 5, 4
        };

        Mesh mesh = new Mesh
        {
            name = "Procedural Lunar Rock",
            vertices = vertices,
            triangles = triangles
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void ApplyLunarNightLighting()
    {
        Material skybox = CreateOrUpdateSkyboxMaterial();
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.025f, 0.027f, 0.03f);
        RenderSettings.reflectionIntensity = 0.05f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.005f, 0.005f, 0.007f);
        RenderSettings.fogDensity = 0.012f;

        Light directional = Object.FindAnyObjectByType<Light>();
        if (directional == null || directional.type != LightType.Directional)
        {
            GameObject lightObject = GameObject.Find("Directional Light");
            if (lightObject == null)
            {
                lightObject = new GameObject("Directional Light");
            }

            directional = lightObject.GetComponent<Light>();
            if (directional == null)
            {
                directional = lightObject.AddComponent<Light>();
            }
        }

        directional.type = LightType.Directional;
        directional.transform.rotation = Quaternion.Euler(28f, -34f, 0f);
        directional.color = new Color(0.86f, 0.92f, 1f);
        directional.intensity = 1.65f;
        directional.shadows = LightShadows.Soft;
    }

    private static Material CreateOrUpdateSkyboxMaterial()
    {
        const string path = "Assets/_Materials/Black Space Skybox.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_SkyTint"))
        {
            material.SetColor("_SkyTint", Color.black);
        }

        if (material.HasProperty("_GroundColor"))
        {
            material.SetColor("_GroundColor", Color.black);
        }

        if (material.HasProperty("_Exposure"))
        {
            material.SetFloat("_Exposure", 0f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateMaterial(string materialName, Color color, float metallic, float smoothness, Color? emission = null)
    {
        string path = $"Assets/_Materials/{materialName}.mat";
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

        SetMaterialColor(material, color);
        SetMaterialFloat(material, "_Metallic", metallic);
        SetMaterialFloat(material, "_Smoothness", smoothness);

        if (emission.HasValue && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value);
        }
        else if (material.HasProperty("_EmissionColor"))
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetMaterialFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static float Range(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private static void MakeTargetSceneBuildScene()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(TargetScenePath, true)
        };
    }
}
