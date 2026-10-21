using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class CustomLevelSceneAdapter
{
    public const string ActiveCustomStageKey = "ActiveCustomStageNumber";
    static bool suppressAutoOverride;

    public static void SetSuppressAutoOverride(bool suppress) => suppressAutoOverride = suppress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (suppressAutoOverride) return;
        if (!GameSession.IsGameplayScene(scene.name)) return;

        int activeCustomStage = PlayerPrefs.GetInt(ActiveCustomStageKey, 0);
        if (activeCustomStage >= 7)
        {
            string stagePath = GetCustomStageFilePath(activeCustomStage);
            if (File.Exists(stagePath))
            {
                CustomLevelDocument stageDoc = CustomLevelDocument.FromJson(File.ReadAllText(stagePath));
                if (stageDoc.Validate().Count == 0)
                {
                    ApplyDocumentToCurrentScene(stageDoc, true);
                    var session = UnityEngine.Object.FindObjectOfType<GameSession>();
                    if (session) session.OverrideActiveLevelId($"level-{activeCustomStage}");
                }
            }
            return;
        }

        int levelNum = ParseLevelNumber(scene.name);
        if (levelNum >= 1 && levelNum <= 6 && HasCampaignOverride(levelNum))
        {
            CustomLevelDocument overrideDoc = LoadCampaignOverride(levelNum);
            if (overrideDoc != null && overrideDoc.Validate().Count == 0)
            {
                ApplyDocumentToCurrentScene(overrideDoc, true);
            }
        }
    }

    public static string GetCustomLevelsFolder()
    {
        string folder = Path.Combine(Application.persistentDataPath, "CustomLevels");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string GetCampaignOverrideFilePath(int levelNumber)
    {
        return Path.Combine(GetCustomLevelsFolder(), $"campaign-override-level-{levelNumber}.json");
    }

    public static bool HasCampaignOverride(int levelNumber)
    {
        return File.Exists(GetCampaignOverrideFilePath(levelNumber));
    }

    public static bool SaveCampaignOverride(int levelNumber, CustomLevelDocument doc)
    {
        if (doc == null || doc.Validate().Count > 0) return false;
        doc.sourceCampaignLevel = levelNumber;
        doc.levelId = $"level-{levelNumber}";
        File.WriteAllText(GetCampaignOverrideFilePath(levelNumber), doc.ToJson());
        return true;
    }

    public static CustomLevelDocument LoadCampaignOverride(int levelNumber)
    {
        string path = GetCampaignOverrideFilePath(levelNumber);
        if (!File.Exists(path)) return null;
        return CustomLevelDocument.FromJson(File.ReadAllText(path));
    }

    public static bool DeleteCampaignOverride(int levelNumber)
    {
        string path = GetCampaignOverrideFilePath(levelNumber);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public static string GetCustomStageFilePath(int stageNumber)
    {
        return Path.Combine(GetCustomLevelsFolder(), $"custom-stage-{stageNumber}.json");
    }

    public static List<int> GetSavedCustomStageNumbers()
    {
        var numbers = new List<int>();
        string folder = GetCustomLevelsFolder();
        foreach (string file in Directory.GetFiles(folder, "custom-stage-*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith("custom-stage-", StringComparison.Ordinal) &&
                int.TryParse(name.Substring("custom-stage-".Length), out int num) && num >= 7)
            {
                numbers.Add(num);
            }
        }
        numbers.Sort();
        return numbers;
    }

    public static int SaveAsNewCustomStage(CustomLevelDocument doc, int explicitStageNumber = 0)
    {
        if (doc == null || doc.Validate().Count > 0) return 0;
        int stageNum = explicitStageNumber >= 7 ? explicitStageNumber : 7;
        if (explicitStageNumber < 7)
        {
            List<int> existing = GetSavedCustomStageNumbers();
            while (existing.Contains(stageNum)) stageNum++;
        }
        doc.levelId = $"level-{stageNum}";
        doc.title = $"Custom Stage {stageNum}";
        File.WriteAllText(GetCustomStageFilePath(stageNum), doc.ToJson());
        return stageNum;
    }

    public static CustomLevelDocument CaptureCurrentScene(int campaignLevelNumber = 4)
    {
        QuarryAssetPalette palette = QuarryAssetPalette.Load();
        var doc = new CustomLevelDocument
        {
            formatVersion = CustomLevelDocument.CurrentFormatVersion,
            levelId = $"level-{campaignLevelNumber}",
            title = $"Level {campaignLevelNumber} Remix",
            sourceCampaignLevel = campaignLevelNumber,
            width = 120,
            height = 60
        };

        PlayerMovement player = UnityEngine.Object.FindObjectOfType<PlayerMovement>(true);
        if (player)
        {
            doc.spawnX = Mathf.RoundToInt(player.transform.position.x);
            doc.spawnY = Mathf.RoundToInt(player.transform.position.y);
        }

        LevelExit exit = UnityEngine.Object.FindObjectOfType<LevelExit>(true);
        if (exit)
        {
            doc.exitX = Mathf.RoundToInt(exit.transform.position.x);
            doc.exitY = Mathf.RoundToInt(exit.transform.position.y);
        }

        if (doc.spawnX == doc.exitX && doc.spawnY == doc.exitY) doc.exitX += 10;

        foreach (Tilemap map in UnityEngine.Object.FindObjectsOfType<Tilemap>(true))
        {
            string layerName = LayerMask.LayerToName(map.gameObject.layer);
            foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
            {
                TileBase tile = map.GetTile(pos);
                if (!tile) continue;
                string id = palette ? palette.ClassifyTileAsset(tile, layerName) : ClassifyFallback(tile, layerName);
                if (!string.IsNullOrEmpty(id))
                    doc.SetTile(pos.x, pos.y, id);
            }
        }

        foreach (Collecting coin in UnityEngine.Object.FindObjectsOfType<Collecting>(true))
        {
            string id = coin.BaseOreValue >= 200 ? "coin_200" : coin.BaseOreValue >= 150 ? "coin_150" : "coin_100";
            doc.SetObject(Mathf.RoundToInt(coin.transform.position.x), Mathf.RoundToInt(coin.transform.position.y), id);
        }

        foreach (BreakableOreCrate crate in UnityEngine.Object.FindObjectsOfType<BreakableOreCrate>(true))
            doc.SetObject(Mathf.RoundToInt(crate.transform.position.x), Mathf.RoundToInt(crate.transform.position.y), "crate_250");

        foreach (OreBankCheckpoint bank in UnityEngine.Object.FindObjectsOfType<OreBankCheckpoint>(true))
            doc.SetObject(Mathf.RoundToInt(bank.transform.position.x), Mathf.RoundToInt(bank.transform.position.y), "bank");

        foreach (PressurePulseHazard vent in UnityEngine.Object.FindObjectsOfType<PressurePulseHazard>(true))
            doc.SetObject(Mathf.RoundToInt(vent.transform.position.x), Mathf.RoundToInt(vent.transform.position.y), "vent");

        foreach (PressureGate gate in UnityEngine.Object.FindObjectsOfType<PressureGate>(true))
        {
            string id = gate.RequiredTier >= 2 ? "gate_t2" : "gate_t1";
            doc.SetObject(Mathf.RoundToInt(gate.transform.position.x), Mathf.RoundToInt(gate.transform.position.y - 0.5f), id);
        }

        foreach (EnemyPatrol2D enemy in UnityEngine.Object.FindObjectsOfType<EnemyPatrol2D>(true))
        {
            SpriteRenderer sr = enemy.GetComponent<SpriteRenderer>();
            bool isRed = (sr && sr.color.r > 0.5f && sr.color.g < 0.4f) || enemy.name.Contains("Crveni") || enemy.name.Contains("Red");
            doc.SetObject(Mathf.RoundToInt(enemy.transform.position.x), Mathf.RoundToInt(enemy.transform.position.y),
                isRed ? "red_enemy" : "enemy");
        }

        foreach (FollowingPoint plat in UnityEngine.Object.FindObjectsOfType<FollowingPoint>(true))
        {
            Transform root = plat.transform.parent ? plat.transform.parent : plat.transform;
            doc.SetObject(Mathf.RoundToInt(root.position.x), Mathf.RoundToInt(root.position.y), "platform");
        }

        foreach (StabilizationPickup pickup in UnityEngine.Object.FindObjectsOfType<StabilizationPickup>(true))
            doc.SetObject(Mathf.RoundToInt(pickup.transform.position.x), Mathf.RoundToInt(pickup.transform.position.y), "stabilization");

        return doc;
    }

    public static void ApplyDocumentToCurrentScene(CustomLevelDocument doc, bool liveGameplay)
    {
        if (doc == null) return;
        QuarryAssetPalette palette = QuarryAssetPalette.Load();

        Tilemap groundMap = null;
        Tilemap climbingMap = null;
        Tilemap hazardMap = null;
        Tilemap jumpMap = null;
        Tilemap bgMap = null;

        foreach (Tilemap map in UnityEngine.Object.FindObjectsOfType<Tilemap>(true))
        {
            map.ClearAllTiles();
            string ln = LayerMask.LayerToName(map.gameObject.layer);
            if (ln == "Ground" && !groundMap) groundMap = map;
            else if (ln == "Climbing" && !climbingMap) climbingMap = map;
            else if (ln == "Hazard" && !hazardMap) hazardMap = map;
            else if (ln == "Jump" && !jumpMap) jumpMap = map;
            else if (ln == "Background" && !bgMap) bgMap = map;
        }
        if (!jumpMap) jumpMap = groundMap;
        if (!climbingMap) climbingMap = groundMap;
        if (!hazardMap) hazardMap = groundMap;

        if (palette)
        {
            for (int i = 0; i < doc.tiles.Count; i++)
            {
                CustomLevelCell cell = doc.tiles[i];
                TileBase tileAsset = palette.GetTileById(cell.id);
                if (!tileAsset) continue;
                Tilemap target = groundMap;
                if (cell.id == "ladder") target = climbingMap;
                else if (cell.id == "bounce") target = jumpMap;
                else if (cell.id == "bg") target = bgMap ? bgMap : groundMap;
                else if (cell.id == "hazard_spike" || cell.id == "water" || cell.id == "water_surface" ||
                         cell.id == "lava" || cell.id == "lava_surface")
                    target = hazardMap;

                if (target) target.SetTile(new Vector3Int(cell.x, cell.y, 0), tileAsset);
            }
        }

        foreach (Collecting c in UnityEngine.Object.FindObjectsOfType<Collecting>(true)) UnityEngine.Object.DestroyImmediate(c.gameObject);
        foreach (BreakableOreCrate c in UnityEngine.Object.FindObjectsOfType<BreakableOreCrate>(true)) UnityEngine.Object.DestroyImmediate(c.gameObject);
        foreach (OreBankCheckpoint b in UnityEngine.Object.FindObjectsOfType<OreBankCheckpoint>(true)) UnityEngine.Object.DestroyImmediate(b.gameObject);
        foreach (PressurePulseHazard v in UnityEngine.Object.FindObjectsOfType<PressurePulseHazard>(true)) UnityEngine.Object.DestroyImmediate(v.gameObject);
        foreach (PressureGate g in UnityEngine.Object.FindObjectsOfType<PressureGate>(true)) UnityEngine.Object.DestroyImmediate(g.gameObject);
        foreach (EnemyPatrol2D e in UnityEngine.Object.FindObjectsOfType<EnemyPatrol2D>(true)) UnityEngine.Object.DestroyImmediate(e.gameObject);
        foreach (StabilizationPickup s in UnityEngine.Object.FindObjectsOfType<StabilizationPickup>(true)) UnityEngine.Object.DestroyImmediate(s.gameObject);
        foreach (FollowingPoint p in UnityEngine.Object.FindObjectsOfType<FollowingPoint>(true))
        {
            GameObject root = p.transform.parent ? p.transform.parent.gameObject : p.gameObject;
            UnityEngine.Object.DestroyImmediate(root);
        }

        ScenePersist persist = UnityEngine.Object.FindObjectOfType<ScenePersist>(true);
        Transform persistParent = persist ? persist.transform : null;

        if (palette)
        {
            for (int i = 0; i < doc.objects.Count; i++)
            {
                CustomLevelCell obj = doc.objects[i];
                SpawnObjectFromPalette(palette, obj.x, obj.y, obj.id, persistParent, liveGameplay);
            }
        }

        PlayerMovement player = UnityEngine.Object.FindObjectOfType<PlayerMovement>(true);
        if (player)
        {
            player.gameObject.SetActive(liveGameplay);
            player.transform.position = new Vector3(doc.spawnX, doc.spawnY, 0f);
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.velocity = Vector2.zero;
                rb.position = new Vector2(doc.spawnX, doc.spawnY);
            }
        }

        LevelExit exit = UnityEngine.Object.FindObjectOfType<LevelExit>(true);
        if (exit)
        {
            exit.gameObject.SetActive(true);
            exit.transform.position = new Vector3(doc.exitX, doc.exitY, 0f);
        }

        Physics2D.SyncTransforms();
    }

    static void SpawnObjectFromPalette(QuarryAssetPalette palette, int x, int y, string id, Transform persistParent, bool live)
    {
        Vector3 pos = new Vector3(x, y, 0f);
        GameObject spawned = null;
        if ((id == "coin_100" || id == "coin_150" || id == "coin_200") && palette.coinPrefab)
        {
            spawned = UnityEngine.Object.Instantiate(palette.coinPrefab, pos, Quaternion.identity, persistParent);
            Collecting col = spawned.GetComponent<Collecting>();
            if (col) col.SetBaseOreValue(id == "coin_200" ? 200 : id == "coin_150" ? 150 : 100);
        }
        else if (id == "crate_250" && palette.breakableCratePrefab)
        {
            spawned = UnityEngine.Object.Instantiate(palette.breakableCratePrefab, pos, Quaternion.identity, persistParent);
        }
        else if (id == "bank" && palette.bankCheckpointPrefab)
        {
            spawned = UnityEngine.Object.Instantiate(palette.bankCheckpointPrefab, pos, Quaternion.identity);
        }
        else if (id == "vent" && palette.pulseVentPrefab)
        {
            spawned = UnityEngine.Object.Instantiate(palette.pulseVentPrefab, pos, Quaternion.identity);
        }
        else if ((id == "gate_t1" || id == "gate_t2") && palette.pressureGatePrefab)
        {
            spawned = UnityEngine.Object.Instantiate(palette.pressureGatePrefab, pos + Vector3.up * 0.5f, Quaternion.identity);
            PressureGate g = spawned.GetComponent<PressureGate>();
            if (g) g.SetRequiredTier(id == "gate_t2" ? 2 : 1);
        }
        else if (id == "enemy" && palette.enemyPrefab)
        {
            spawned = UnityEngine.Object.Instantiate(palette.enemyPrefab, pos, Quaternion.identity, persistParent);
        }
        else if (id == "red_enemy" && palette.redEnemyPrefab)
        {
            spawned = UnityEngine.Object.Instantiate(palette.redEnemyPrefab, pos, Quaternion.identity, persistParent);
        }
        else if (id == "platform" && palette.movingPlatformPrefab)
        {
            spawned = UnityEngine.Object.Instantiate(palette.movingPlatformPrefab, pos - new Vector3(8f, 0.399f, 0f), Quaternion.identity);
        }
        else if (id == "stabilization" && palette.stabilizationPickupPrefab)
        {
            spawned = UnityEngine.Object.Instantiate(palette.stabilizationPickupPrefab, pos, Quaternion.identity, persistParent);
        }

        if (spawned && !live)
        {
            foreach (EnemyPatrol2D patrol in spawned.GetComponentsInChildren<EnemyPatrol2D>(true)) patrol.enabled = false;
            foreach (FollowingPoint fp in spawned.GetComponentsInChildren<FollowingPoint>(true)) fp.enabled = false;
            foreach (Rigidbody2D rb in spawned.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;
        }
    }

    static string ClassifyFallback(TileBase tile, string layerName)
    {
        if (layerName == "Climbing" || tile.name == "Ljestve") return "ladder";
        if (layerName == "Jump" || tile.name == "Gljiva") return "bounce";
        if (tile.name == "Spikes") return "hazard_spike";
        if (tile.name == "LavaSurface") return "lava_surface";
        if (tile.name == "LavaBody") return "lava";
        if (tile.name == "SPA_Rock_Grass_Water_29") return "water_surface";
        if (tile.name == "SPA_Rock_Grass_Water_28") return "water";
        if (layerName == "Background") return "bg";
        return "ground";
    }

    static int ParseLevelNumber(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("Level ", StringComparison.Ordinal) &&
            int.TryParse(sceneName.Substring(6), out int n))
            return n;
        return 1;
    }
}
