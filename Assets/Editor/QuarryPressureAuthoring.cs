using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QuarryPressureAuthoring
{
    public const string CheckpointPath = "Assets/Prefabs/OreBankCheckpoint.prefab";
    public const string VentPath = "Assets/Prefabs/PressurePulseVent.prefab";
    const string ArtFolder = "Assets/Sprites/QuarryPressure";

    [MenuItem("Tools/QuantumQuarry/Pressure/Create Custom Prefabs")]
    public static void CreatePrefabs()
    {
        CreatePrefab<OreBankCheckpoint>(CheckpointPath, "OreBankCheckpoint", "BANK",
            CreateSprite("OreBankCheckpoint", QuarryPressureArt.Checkpoint), new Vector2(1.4f, 1.2f));
        CreatePrefab<PressurePulseHazard>(VentPath, "PressurePulseVent", "VENT",
            CreateSprite("PressurePulseVent", QuarryPressureArt.Vent), new Vector2(0.9f, 0.45f));
        AssetDatabase.SaveAssets();
        Debug.Log("Pressure prefabs ready. Place them in a level using Tools > QuantumQuarry > Pressure.");
    }

    static Sprite CreateSprite(string assetName, string[] pixels)
    {
        string path = $"{ArtFolder}/{assetName}.png";
        if (File.Exists(path))
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!existing) throw new InvalidOperationException($"Existing artwork is not imported as a sprite: {path}");
            return existing;
        }
        if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Sprites", "QuarryPressure");
        var texture = new Texture2D(QuarryPressureArt.Size, QuarryPressureArt.Size, TextureFormat.RGBA32, false);
        try
        {
            for (int row = 0; row < QuarryPressureArt.Size; row++)
                for (int column = 0; column < QuarryPressureArt.Size; column++)
                    texture.SetPixel(column, QuarryPressureArt.Size - row - 1, PixelColor(pixels[row][column]));
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = QuarryPressureArt.Size;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Color PixelColor(char pixel)
    {
        switch (pixel)
        {
            case '#': return new Color32(24, 29, 35, 255);
            case 's': return new Color32(110, 133, 145, 255);
            case 'c': return new Color32(74, 225, 240, 255);
            case 'g': return new Color32(86, 231, 139, 255);
            case 'y': return new Color32(255, 204, 76, 255);
            case 'w': return new Color32(231, 255, 246, 255);
            default: return Color.clear;
        }
    }

    static void CreatePrefab<T>(string path, string objectName, string labelText, Sprite sprite, Vector2 triggerSize)
        where T : Component
    {
        if (File.Exists(path)) return;
        var root = new GameObject(objectName);
        try
        {
            GameObject reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StabilizationPickup.prefab");
            if (!reference) throw new InvalidOperationException("StabilizationPickup prefab is required for the collision layer.");
            root.layer = reference.layer;
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            SpriteRenderer referenceRenderer = reference.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = referenceRenderer.sortingLayerID;
            renderer.sortingOrder = referenceRenderer.sortingOrder + 1;
            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = triggerSize;
            trigger.offset = typeof(T) == typeof(PressurePulseHazard) ? new Vector2(0f, -0.15f) : Vector2.zero;
            root.AddComponent<T>();
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = Vector3.up * 0.75f;
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = labelText;
            label.fontSize = 2.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.sortingOrder = 100;
            label.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [MenuItem("Tools/QuantumQuarry/Pressure/Place Banking Checkpoint")]
    static void PlaceCheckpoint() => Place(CheckpointPath);

    [MenuItem("Tools/QuantumQuarry/Pressure/Place Pulse Vent")]
    static void PlaceVent() => Place(VentPath);

    static void Place(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Place pressure objects outside Play Mode.");
        CreatePrefabs();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place pressure object");
        Vector3 position = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        instance.transform.position = new Vector3(Mathf.Round(position.x), Mathf.Round(position.y), 0f);
        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);
    }
}