using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class QuarryLavaAuthoring
{
    public const string SurfacePath = "Assets/Tiles/LavaSurface.asset";
    public const string BodyPath = "Assets/Tiles/LavaBody.asset";
    const string ScenePath = "Assets/Levels/Level 6.unity";
    const string ArtFolder = "Assets/Sprites/QuarryLava";

    [MenuItem("Tools/QuantumQuarry/Lava/Create Level 6 Artwork")]
    public static void CreateLevel6Artwork()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author lava outside Play Mode.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (!opened && scene.isDirty)
            throw new InvalidOperationException("Save Level 6 before authoring lava.");
        Tile oldSurface = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tiles/SPA_Rock_Grass_Water_29.asset");
        Tile oldBody = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tiles/SPA_Rock_Grass_Water_28.asset");
        if (!oldSurface || !oldBody) throw new InvalidOperationException("Original liquid tiles are required.");
        LavaTile surface = CreateTile(SurfacePath, "LavaSurface", oldSurface, true);
        LavaTile body = CreateTile(BodyPath, "LavaBody", oldBody, false);
        ValidateTile(surface, oldSurface);
        ValidateTile(body, oldBody);
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            int changed = 0;
            int lavaCells = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
                    foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                    {
                        TileBase current = map.GetTile(cell);
                        if (current != oldSurface && current != oldBody && !(current is LavaTile)) continue;
                        lavaCells++;
                        TileBase above = map.GetTile(cell + Vector3Int.up);
                        LavaTile replacement = LiquidRules.ClassifyTile(above ? above.name : null, 6) == LiquidKind.Lava
                            ? body : surface;
                        ValidateTile(replacement, (Tile)current);
                        if (current != replacement)
                        {
                            Matrix4x4 transform = map.GetTransformMatrix(cell);
                            map.SetTile(cell, replacement);
                            map.SetTileFlags(cell, TileFlags.None);
                            map.SetColor(cell, Color.white);
                            map.SetTransformMatrix(cell, transform);
                            map.SetTileFlags(cell, replacement.flags);
                            changed++;
                        }
                    }
            if (lavaCells == 0) throw new InvalidOperationException("Level 6 has no lava cells to author.");
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save Level 6 lava.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Level 6 lava artwork ready: {lavaCells} cells, {changed} replacements.");
        }
        finally
        {
            if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static LavaTile CreateTile(string path, string name, Tile original, bool surface)
    {
        if (File.Exists(path))
        {
            LavaTile existing = AssetDatabase.LoadAssetAtPath<LavaTile>(path);
            if (!existing) throw new InvalidOperationException($"Existing asset is not a lava tile: {path}");
            return existing;
        }
        Sprite[] frames = CreateSprites(name, original.sprite, surface);
        LavaTile tile = ScriptableObject.CreateInstance<LavaTile>();
        tile.name = name;
        tile.frames = frames;
        tile.sprite = frames[0];
        tile.color = Color.white;
        tile.transform = original.transform;
        tile.flags = original.flags;
        tile.colliderType = original.colliderType;
        AssetDatabase.CreateAsset(tile, path);
        return tile;
    }

    static Sprite[] CreateSprites(string name, Sprite original, bool surface)
    {
        if (!original) throw new InvalidOperationException("Original liquid sprite is missing.");
        if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Sprites", "QuarryLava");
        string path = $"{ArtFolder}/{name}.png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(QuarryLavaArt.Size * QuarryLavaArt.FrameCount, QuarryLavaArt.Size,
                TextureFormat.RGBA32, false);
            try
            {
                for (int frame = 0; frame < QuarryLavaArt.FrameCount; frame++)
                    for (int y = 0; y < QuarryLavaArt.Size; y++)
                        for (int x = 0; x < QuarryLavaArt.Size; x++)
                            texture.SetPixel(frame * QuarryLavaArt.Size + x, y,
                                PixelColor(QuarryLavaArt.Pixel(x, y, frame, surface)));
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = original.pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = new SpriteRect[QuarryLavaArt.FrameCount];
            for (int frame = 0; frame < rects.Length; frame++)
                rects[frame] = new SpriteRect
                {
                    name = $"{name}_{frame}",
                    spriteID = GUID.Generate(),
                    rect = new Rect(frame * QuarryLavaArt.Size, 0, QuarryLavaArt.Size, QuarryLavaArt.Size),
                    alignment = SpriteAlignment.Custom,
                    pivot = original.pivot / original.rect.size
                };
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));

            var outlines = new List<Vector2[]>();
            for (int index = 0; index < original.GetPhysicsShapeCount(); index++)
            {
                var points = new List<Vector2>();
                original.GetPhysicsShape(index, points);
                // Importer outlines are centered pixel coordinates, unlike Sprite's local-space physics points.
                outlines.Add(points.Select(point => point * original.pixelsPerUnit +
                    original.pivot - original.rect.size * 0.5f).ToArray());
            }
            ISpritePhysicsOutlineDataProvider physics = provider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
            foreach (SpriteRect rect in rects) physics.SetOutlines(rect.spriteID, outlines);
            provider.Apply();
            importer.SaveAndReimport();
        }
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        if (sprites.Length != QuarryLavaArt.FrameCount)
            throw new InvalidOperationException($"Expected {QuarryLavaArt.FrameCount} imported lava frames in {path}.");
        return sprites;
    }

    public static void ValidateTile(LavaTile tile, Tile original)
    {
        if (!tile || !original || !original.sprite || tile.frames == null || tile.frames.Length != QuarryLavaArt.FrameCount ||
            tile.frames.Distinct().Count() != QuarryLavaArt.FrameCount ||
            tile.sprite != tile.frames[0] || tile.color != Color.white || tile.colliderType != original.colliderType ||
            tile.transform != original.transform || tile.flags != original.flags)
            throw new InvalidOperationException("Lava tile data is incomplete or differs from the original liquid collision setup.");
        foreach (Sprite frame in tile.frames)
        {
            if (!frame || frame.rect.size != new Vector2(QuarryLavaArt.Size, QuarryLavaArt.Size) ||
                frame.pixelsPerUnit != original.sprite.pixelsPerUnit ||
                frame.pivot != original.sprite.pivot || frame.GetPhysicsShapeCount() != original.sprite.GetPhysicsShapeCount())
                throw new InvalidOperationException($"Lava frame geometry differs from {original.name}.");
            for (int index = 0; index < frame.GetPhysicsShapeCount(); index++)
            {
                var expected = new List<Vector2>();
                var actual = new List<Vector2>();
                original.sprite.GetPhysicsShape(index, expected);
                frame.GetPhysicsShape(index, actual);
                if (actual.Count != expected.Count ||
                    actual.Where((point, i) => (point - expected[i]).sqrMagnitude > 0.000001f).Any())
                    throw new InvalidOperationException($"Lava frame {frame.name} changes the original collider outline.");
            }
        }
    }

    public static void CollectValidationErrors(List<string> errors)
    {
        LavaTile surface = AssetDatabase.LoadAssetAtPath<LavaTile>(SurfacePath);
        LavaTile body = AssetDatabase.LoadAssetAtPath<LavaTile>(BodyPath);
        try
        {
            ValidateTile(surface, AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tiles/SPA_Rock_Grass_Water_29.asset"));
            ValidateTile(body, AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tiles/SPA_Rock_Grass_Water_28.asset"));
        }
        catch (InvalidOperationException error)
        {
            errors.Add(error.Message);
            return;
        }
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
                    foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                    {
                        TileBase tile = map.GetTile(cell);
                        if (LiquidRules.ClassifyTile(tile ? tile.name : null, 6) != LiquidKind.Lava) continue;
                        count++;
                        TileBase above = map.GetTile(cell + Vector3Int.up);
                        LavaTile expected = LiquidRules.ClassifyTile(above ? above.name : null, 6) == LiquidKind.Lava
                            ? body : surface;
                        if (tile != expected || map.GetColor(cell) != Color.white)
                            errors.Add($"Level 6 lava at {map.name}/{cell} needs untinted, correctly surfaced lava artwork.");
                    }
            if (count == 0) errors.Add("Level 6 requires authored lava cells.");
        }
        finally
        {
            if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static Color32 PixelColor(char pixel)
    {
        switch (pixel)
        {
            case '#': return new Color32(92, 24, 24, 255);
            case 'r': return new Color32(194, 43, 17, 255);
            case 'o': return new Color32(246, 91, 16, 255);
            case 'y': return new Color32(255, 166, 34, 255);
            case 'w': return new Color32(255, 228, 112, 255);
            default: return new Color32(0, 0, 0, 0);
        }
    }
}
