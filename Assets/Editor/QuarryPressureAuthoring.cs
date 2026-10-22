using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class QuarryPressureAuthoring
{
    public const string CheckpointPath = "Assets/Prefabs/OreBankCheckpoint.prefab";
    public const string VentPath = "Assets/Prefabs/PressurePulseVent.prefab";
    public const string CratePath = "Assets/Prefabs/BreakableOreCrate.prefab";
    public const string GatePath = "Assets/Prefabs/PressureGate.prefab";
    public const string JumpPadPath = "Assets/Prefabs/JumpPad.prefab";
    public const string SentryPath = "Assets/Prefabs/SentryTurret.prefab";
    public const string ShieldedEnemyPath = "Assets/Prefabs/ShieldedEnemy.prefab";
    const string ArtFolder = "Assets/Sprites/QuarryPressure";

    [MenuItem("Tools/QuantumQuarry/Pressure/Create Custom Prefabs")]
    public static void CreatePrefabs()
    {
        CreatePrefab<OreBankCheckpoint>(CheckpointPath, "OreBankCheckpoint", "BANK",
            CreateSprite("OreBankCheckpoint", QuarryPressureArt.Checkpoint), new Vector2(1.4f, 1.2f));
        CreatePrefab<PressurePulseHazard>(VentPath, "PressurePulseVent", "VENT",
            CreateSprite("PressurePulseVent", QuarryPressureArt.Vent), new Vector2(0.9f, 0.45f));
        CreatePrefab<BreakableOreCrate>(CratePath, "BreakableOreCrate", "CRATE +250",
            CreateSprite("BreakableOreCrate", QuarryPressureArt.Crate), new Vector2(0.95f, 0.95f), false, "Ground");
        CreatePrefab<PressureGate>(GatePath, "PressureGate", "LOCK T1",
            CreateSprite("PressureGate", QuarryPressureArt.Gate), new Vector2(0.8f, 1.8f), false, "Ground", new Vector3(1f, 2f, 1f));
        CreatePrefab<JumpPad>(JumpPadPath, "JumpPad", "JUMP",
            CreateSprite("JumpPad", QuarryPressureArt.JumpPad), new Vector2(0.9f, 0.55f));
        CreatePrefab<SentryTurret>(SentryPath, "SentryTurret", "SENTRY",
            CreateSprite("SentryTurret", QuarryPressureArt.Sentry), new Vector2(0.85f, 0.85f), true, "Enemy", null, "Enemy");
        CreateShieldedEnemyPrefab();
        EnsureAssetPalette();
        AssetDatabase.SaveAssets();
        Debug.Log("Pressure prefabs ready. Place them in a level using Tools > QuantumQuarry > Pressure.");
    }

    static void CreateShieldedEnemyPrefab()
    {
        if (File.Exists(ShieldedEnemyPath)) return;
        GameObject baseEnemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
        if (!baseEnemy) throw new InvalidOperationException("Enemy.prefab is required to build ShieldedEnemy.prefab.");
        Sprite shieldSprite = CreateSprite("ShieldedEnemy", QuarryPressureArt.ShieldedEnemy);
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(baseEnemy);
        try
        {
            root.name = "ShieldedEnemy";
            Animator anim = root.GetComponent<Animator>();
            if (anim) UnityEngine.Object.DestroyImmediate(anim);
            SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
            if (sr) sr.sprite = shieldSprite;
            root.AddComponent<ShieldedEnemyGuard>();
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = "SHIELD";
            label.fontSize = 2.5f;
            label.alignment = TextAlignmentOptions.Center;
            if (sr) label.sortingLayerID = sr.sortingLayerID;
            label.sortingOrder = 100;
            label.rectTransform.sizeDelta = new Vector2(2.6f, 0.5f);
            PrefabUtility.SaveAsPrefabAsset(root, ShieldedEnemyPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [MenuItem("Tools/QuantumQuarry/Creator/Create Or Update Asset Palette")]
    public static void EnsureAssetPalette()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        const string palettePath = "Assets/Resources/QuarryAssetPalette.asset";
        QuarryAssetPalette palette = AssetDatabase.LoadAssetAtPath<QuarryAssetPalette>(palettePath);
        if (!palette)
        {
            palette = ScriptableObject.CreateInstance<QuarryAssetPalette>();
            AssetDatabase.CreateAsset(palette, palettePath);
        }
        palette.ruleGroundTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Sprites/Rule plocica.asset");
        palette.rockSlopeTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Tiles/SPA_Rock_Grass_Water_11.asset");
        palette.ladderTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Tiles/Ljestve.asset");
        palette.bounceMushroomTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Tiles/Gljiva.asset");
        palette.spikesTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Tiles/Spikes.asset");
        palette.waterSurfaceTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Tiles/SPA_Rock_Grass_Water_29.asset");
        palette.waterBodyTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Tiles/SPA_Rock_Grass_Water_28.asset");
        palette.lavaSurfaceTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Tiles/LavaSurface.asset");
        palette.lavaBodyTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Tiles/LavaBody.asset");
        palette.backgroundTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Tiles/Pozadina_5.asset");

        palette.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        palette.exitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Exit.prefab");
        palette.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
        palette.redEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Enemy.prefab");
        palette.movingPlatformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Platform.prefab");
        palette.coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");
        palette.breakableCratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CratePath);
        palette.bankCheckpointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPath);
        palette.pulseVentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VentPath);
        palette.pressureGatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePath);
        palette.stabilizationPickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StabilizationPickup.prefab");
        palette.jumpPadPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(JumpPadPath);
        palette.sentryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SentryPath);
        palette.shieldedEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShieldedEnemyPath);
        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();
        Debug.Log("QuarryAssetPalette updated with real project tiles and prefabs.");
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

    static void CreatePrefab<T>(string path, string objectName, string labelText, Sprite sprite, Vector2 triggerSize,
        bool isTrigger = true, string layerName = null, Vector3? rootScale = null, string tagName = null)
        where T : Component
    {
        if (File.Exists(path)) return;
        var root = new GameObject(objectName);
        try
        {
            GameObject reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StabilizationPickup.prefab");
            if (!reference) throw new InvalidOperationException("StabilizationPickup prefab is required for the collision layer.");
            int resolvedLayer = !string.IsNullOrEmpty(layerName) ? LayerMask.NameToLayer(layerName) : -1;
            root.layer = resolvedLayer >= 0 ? resolvedLayer : reference.layer;
            if (!string.IsNullOrEmpty(tagName)) root.tag = tagName;
            Vector3 scale = rootScale ?? Vector3.one;
            root.transform.localScale = scale;
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            SpriteRenderer referenceRenderer = reference.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = referenceRenderer.sortingLayerID;
            renderer.sortingOrder = referenceRenderer.sortingOrder + 1;
            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = isTrigger;
            trigger.size = new Vector2(triggerSize.x / scale.x, triggerSize.y / scale.y);
            trigger.offset = typeof(T) == typeof(PressurePulseHazard) ? new Vector2(0f, -0.15f) : Vector2.zero;
            root.AddComponent<T>();
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.3f / scale.y, 0f);
            labelObject.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = labelText;
            label.fontSize = 2.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.sortingLayerID = renderer.sortingLayerID;
            label.sortingOrder = 100;
            label.rectTransform.sizeDelta = new Vector2(2.6f, 0.5f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [MenuItem("Tools/QuantumQuarry/Pressure/Place Banking Checkpoint")]
    static void PlaceCheckpoint() => Place(CheckpointPath);

    [MenuItem("Tools/QuantumQuarry/Pressure/Place Pulse Vent")]
    static void PlaceVent() => Place(VentPath);

    [MenuItem("Tools/QuantumQuarry/Pressure/Place Breakable Ore Crate")]
    static void PlaceCrate() => Place(CratePath);

    [MenuItem("Tools/QuantumQuarry/Pressure/Place Pressure Gate")]
    static void PlaceGate() => Place(GatePath);

    [MenuItem("Tools/QuantumQuarry/Pressure/Update Prefab Label Sorting")]
    public static void UpdatePrefabLabelSorting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Update Pressure prefabs outside Play Mode.");
        foreach (string path in new[] { CheckpointPath, VentPath, CratePath, GatePath })
        {
            if (!File.Exists(path)) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                TextMeshPro label = root.GetComponentInChildren<TextMeshPro>();
                if (!renderer || !label) throw new InvalidOperationException($"Missing artwork or label in {path}.");
                label.sortingLayerID = renderer.sortingLayerID;
                label.sortingOrder = Mathf.Max(label.sortingOrder, renderer.sortingOrder + 1);
                label.transform.localPosition = new Vector3(0f, 1.3f / root.transform.localScale.y, 0f);
                if (!PrefabUtility.SaveAsPrefabAsset(root, path))
                    throw new InvalidOperationException($"Failed to update {path}.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("Pressure prefab label sorting updated.");
    }

    [MenuItem("Tools/QuantumQuarry/Pressure/Create Level 4 Pilot")]
    public static void CreateLevel4Pilot()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author the Pressure pilot outside Play Mode.");
        const string scenePath = "Assets/Levels/Level 4.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.isLoaded;
        if (!opened && scene.isDirty)
            throw new InvalidOperationException("Save Level 4 before authoring the Pressure pilot.");
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            CreatePrefabs();
            Physics2D.SyncTransforms();
            PlacePilotObject(scene, CheckpointPath, "Pressure Pilot Bank", -12f, 8f);
            PlacePilotObject(scene, VentPath, "Pressure Pilot Vent", 27f, 5f);
            PlacePilotObject(scene, GatePath, "Pressure Pilot Gate", 11.5f, 13f, 1f);
            PlacePilotObject(scene, JumpPadPath, "Pressure Pilot JumpPad", 9f, 13f);
            ScenePersist persistence = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                persistence = root.GetComponentInChildren<ScenePersist>();
                if (persistence) break;
            }
            if (!persistence) throw new InvalidOperationException("Level 4 needs ScenePersist to retain collected ore during Store visits.");
            PlacePilotParentedPrefab(scene, persistence.transform, CratePath, "Pressure Vault Crate 01", 15f, 13f);
            PlacePilotParentedPrefab(scene, persistence.transform, CratePath, "Pressure Vault Crate 02", 18f, 13f);
            PlacePilotParentedPrefab(scene, persistence.transform, SentryPath, "Pressure Pilot Sentry", 22f, 13f);
            PlacePilotParentedPrefab(scene, persistence.transform, ShieldedEnemyPath, "Pressure Pilot ShieldedEnemy", 2.5f, 9f);
            PlacePilotCoin(scene, persistence.transform, "Pressure Approach Ore", new Vector2(25.5f, 5.8f));
            Vector2[] route = {
                new Vector2(-10f, -4f), new Vector2(-6f, 1f), new Vector2(-4.5f, 1f), new Vector2(-3f, 1f),
                new Vector2(-0.5f, 9f), new Vector2(1.5f, 9f), new Vector2(3.5f, 9f), new Vector2(5.5f, 9f),
                new Vector2(8.5f, 7f), new Vector2(11.5f, 8f), new Vector2(16.5f, 7f),
                new Vector2(18f, 7f), new Vector2(20.5f, 8f)
            };
            for (int index = 0; index < route.Length; index++)
            {
                Vector2 surface = route[index];
                RaycastHit2D floor = FindPilotFloor(surface.x, surface.y);
                Vector2 position = new Vector2(surface.x, floor.point.y + 0.8f);
                if (Physics2D.OverlapBox(position, new Vector2(0.8f, 1.4f), 0f, LayerMask.GetMask("Ground", "Jump")))
                    throw new InvalidOperationException($"Insufficient ore pickup clearance at {position}.");
                PlacePilotCoin(scene, persistence.transform, $"Pressure Route Ore {index + 1:00}", position);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Failed to save the Level 4 Pressure pilot.");
            Debug.Log("Level 4 Pressure pilot authored; run runtime validation and manually complete the route.");
        }
        finally
        {
            if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static GameObject FindRoot(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == objectName) return root;
        return null;
    }

    static void PlacePilotObject(Scene scene, string prefabPath, string objectName, float x, float expectedFloor, float heightOffset = 0.5f)
    {
        if (FindRoot(scene, objectName)) return;
        int ground = LayerMask.GetMask("Ground", "Jump");
        RaycastHit2D hit = FindPilotFloor(x, expectedFloor);
        if (Physics2D.OverlapBox(new Vector2(x, hit.point.y + 1.2f), new Vector2(1.6f, 2f), 0f, ground))
            throw new InvalidOperationException($"Insufficient clearance for {objectName}.");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (!prefab) throw new InvalidOperationException($"Missing Pressure prefab: {prefabPath}");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = objectName;
        instance.transform.position = new Vector3(x, hit.point.y + heightOffset, 0f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
    }

    static void PlacePilotParentedPrefab(Scene scene, Transform parent, string prefabPath, string objectName, float x, float expectedFloor)
    {
        GameObject instance = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) instance = child.gameObject;
        if (!instance)
        {
            int ground = LayerMask.GetMask("Ground", "Jump");
            RaycastHit2D hit = FindPilotFloor(x, expectedFloor);
            if (Physics2D.OverlapBox(new Vector2(x, hit.point.y + 1.1f), new Vector2(1.2f, 1.6f), 0f, ground))
                throw new InvalidOperationException($"Insufficient clearance for {objectName}.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab) throw new InvalidOperationException($"Missing Pressure prefab: {prefabPath}");
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = objectName;
            instance.transform.position = new Vector3(x, hit.point.y + 0.5f, 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        }
        instance.transform.SetParent(parent, true);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
    }

    static RaycastHit2D FindPilotFloor(float x, float expectedFloor)
    {
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, expectedFloor + 1.5f), Vector2.down, 2.5f,
            LayerMask.GetMask("Ground", "Jump"));
        if (!hit || hit.normal.y < 0.9f || Mathf.Abs(hit.point.y - expectedFloor) > 0.1f)
            throw new InvalidOperationException($"Expected a safe pilot floor near ({x}, {expectedFloor}).");
        return hit;
    }

    static void PlacePilotCoin(Scene scene, Transform parent, string objectName, Vector2 position)
    {
        GameObject instance = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) instance = child.gameObject;
        if (!instance)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");
            if (!prefab) throw new InvalidOperationException("Coin prefab is required for the Pressure route.");
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = objectName;
            instance.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        }
        instance.transform.SetParent(parent, true);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
    }

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