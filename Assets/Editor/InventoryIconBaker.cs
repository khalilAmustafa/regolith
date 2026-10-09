using System.IO;
using UnityEditor;
using UnityEngine;

public static class InventoryIconBaker
{
    private const string OutputFolder = "Assets/_Art/UI/InventoryIcons";

    [MenuItem("Tools/Regolith/Bake Selected Inventory Icons")]
    public static void BakeSelectedInventoryIcons()
    {
        Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("Inventory icon bake skipped: select one or more prefabs/models/assets first.");
            return;
        }

        EnsureOutputFolder();

        int bakedCount = 0;
        foreach (Object selectedObject in selectedObjects)
        {
            if (selectedObject == null)
            {
                continue;
            }

            Texture2D preview = GetPreviewTexture(selectedObject);
            if (preview == null)
            {
                Debug.LogWarning($"Inventory icon bake skipped for '{selectedObject.name}': Unity has not generated an asset preview yet. Select it and run the menu item again.", selectedObject);
                continue;
            }

            Texture2D readablePreview = CopyToReadableTexture(preview);
            byte[] pngBytes = readablePreview.EncodeToPNG();
            Object.DestroyImmediate(readablePreview);

            string fileName = SanitizeFileName(selectedObject.name) + "_Icon.png";
            string outputPath = $"{OutputFolder}/{fileName}";
            File.WriteAllBytes(outputPath, pngBytes);
            AssetDatabase.ImportAsset(outputPath);
            ConfigureAsSprite(outputPath);
            bakedCount++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"Inventory icon bake complete. Generated {bakedCount} icon(s) in {OutputFolder}.");
    }

    private static Texture2D GetPreviewTexture(Object target)
    {
        Texture2D preview = AssetPreview.GetAssetPreview(target);
        if (preview != null)
        {
            return preview;
        }

        preview = AssetPreview.GetMiniThumbnail(target);
        return preview != null && preview.width > 16 && preview.height > 16 ? preview : null;
    }

    private static Texture2D CopyToReadableTexture(Texture source)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, renderTexture);
        RenderTexture.active = renderTexture;

        Texture2D readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readableTexture.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);
        return readableTexture;
    }

    private static void ConfigureAsSprite(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private static void EnsureOutputFolder()
    {
        CreateFolderIfMissing("Assets", "_Art");
        CreateFolderIfMissing("Assets/_Art", "UI");
        CreateFolderIfMissing("Assets/_Art/UI", "InventoryIcons");
    }

    private static void CreateFolderIfMissing(string parent, string folder)
    {
        string path = $"{parent}/{folder}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return string.IsNullOrWhiteSpace(fileName) ? "InventoryIcon" : fileName;
    }
}
