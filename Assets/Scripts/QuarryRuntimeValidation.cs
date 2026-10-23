#if QUARRY_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public sealed class QuarryRuntimeValidation : MonoBehaviour
{
    [Serializable]
    sealed class Report
    {
        public string mode = "acceptance";
        public bool passed;
        public List<string> checks = new List<string>();
        public List<string> screenshots = new List<string>();
        public List<string> errors = new List<string>();
    }

    readonly Report report = new Report();
    string outputDirectory;
    bool finished;
    float deadline;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Begin()
    {
        var runner = new GameObject("QuarryRuntimeValidation").AddComponent<QuarryRuntimeValidation>();
        DontDestroyOnLoad(runner.gameObject);
    }

    void Awake()
    {
        if (Application.productName != "QuantumQuarry Validation")
            throw new InvalidOperationException("Validation must never use the campaign's PlayerPrefs namespace.");
        string[] arguments = Environment.GetCommandLineArgs();
        int option = Array.IndexOf(arguments, "-quarryValidationOutput");
        if (option < 0 || option + 1 >= arguments.Length)
            throw new ArgumentException("Missing -quarryValidationOutput directory.");
        outputDirectory = Path.GetFullPath(arguments[option + 1]);
        Directory.CreateDirectory(outputDirectory);
        Application.runInBackground = true;
        deadline = Time.realtimeSinceStartup + 150f;
        Application.logMessageReceived += OnLog;
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetInt(GameSession.CoinsKey, 1234);
        PlayerPrefs.SetInt(StartMenu.UnlockedLevelKey, 6);
        PlayerPrefs.Save();
    }

    IEnumerator Start()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-quarrySceneSurvey") >= 0)
        {
            report.mode = "scene-survey";
            yield return SurveyLevel();
            Finish();
            yield break;
        }
        Require(GameObject.Find("Button_FrontLevelCreator") != null,
            "Start menu displays the front-page LEVEL CREATOR button.");
        Require(GameObject.Find("Button_DailyChallenge") != null && GameObject.Find("Button_WipeProfile") != null,
            "Start menu displays the DAILY CHALLENGE and RESET PROFILE buttons.");
        yield return Capture("start-menu", 1280, 720);
        yield return SceneManager.LoadSceneAsync("Level 1");
        yield return new WaitForSecondsRealtime(0.5f);
        GameSession session = FindObjectOfType<GameSession>();
        Require(session && FindObjectOfType<PlayerMovement>() && Camera.main, "Level 1 has a session, player, and main camera.");
        Require(session.GetCoins() == 1234, "Existing banked coins survive scene entry.");
        yield return Capture("level-1", 1280, 720);
        ScenePersist persisted = FindObjectOfType<ScenePersist>();
        if (persisted) persisted.ResetScenePersist();
        yield return null;
        yield return SceneManager.LoadSceneAsync("Level 4");
        yield return new WaitForSecondsRealtime(0.5f);
        session = FindObjectOfType<GameSession>();
        Require(session, "Level 4 keeps a live GameSession.");
        yield return CheckEnemyAwareness(session);
        yield return CheckPressureEncounter(session);
        session.AddCoins(3000);
        while (session.GetStability() > 1f) session.TakeStabilityDamageUnits(1);
        Require(session.IsCriticalStability(), "HUD stress capture includes the critical-Stability multiplier.");
        session.SetBreathStatus(2.5f, 4.5f);
        Time.timeScale = 0f;
        yield return Capture("level-4-pressure", 800, 600);
        yield return Capture("level-4-pressure", 1280, 720);
        yield return Capture("level-4-pressure", 1920, 1080);
        Time.timeScale = 1f;
        PauseMenu pause = FindObjectOfType<PauseMenu>();
        Require(pause, "Gameplay has a pause menu.");
        pause.TogglePause();
        Require(PauseMenu.GameIsPaused && Time.timeScale == 0f, "Pause freezes gameplay.");
        Require(GameObject.Find("Button_AudioVol") != null && GameObject.Find("Button_ReducedFlash") != null &&
            GameObject.Find("Button_ShowSplits") != null,
            "Pause menu exposes Audio Volume, Reduced Flash, and Speedrun Splits option buttons.");
        Require(FindObjectOfType<GhostReplayRunner>() != null,
            "Gameplay scene spawns the GhostReplayRunner and Speedrun Splits HUD.");
        int expectedBalance = session.GetCoins() + session.Pressure.PendingCoins;
        int remainingCoins = FindObjectsOfType<Collecting>().Length;
        pause.LoadStore();
        yield return null;
        yield return new WaitForSecondsRealtime(0.3f);
        Require(!PauseMenu.GameIsPaused && Time.timeScale == 1f, "Entering the Store clears the pause state.");
        Require(session.GetCoins() == expectedBalance && session.Pressure.CarriedOre == 0 &&
            session.Pressure.PendingCoins == 0, "Store entry banks carried ore exactly once.");
        Require(FindObjectsOfType<Collecting>().Length == 0 && FindObjectsOfType<BreakableOreCrate>().Length == 0 &&
            FindObjectsOfType<EnemyPatrol2D>().Length == 0,
            "Persisted coins, crates, and enemies are suspended, not visible or simulated in the Store.");
        yield return Capture("store", 800, 600);
        yield return Capture("store", 1280, 720);
        StoreManager store = FindObjectOfType<StoreManager>();
        Require(store, "Store has a manager.");
        store.ReturnToGame();
        yield return null;
        yield return new WaitForSecondsRealtime(0.3f);
        Require(SceneManager.GetActiveScene().name == "Level 4", "Store returns to the originating level.");
        Require(session.GetCoins() == expectedBalance, "Store return does not duplicate the deposit.");
        Require(FindObjectsOfType<Collecting>().Length == remainingCoins && FindObjectsOfType<BreakableOreCrate>().Length == 1,
            "Store return restores remaining coins and crates without respawning collected ore or broken crates.");
        pause = FindObjectOfType<PauseMenu>();
        Require(pause, "Store return restores the gameplay pause menu.");
        LevelResult level4Result = session.CompleteCurrentLevel("Level 4");
        Require(level4Result.LevelId == "level-4" && level4Result.BankedOre >= 1500 &&
            level4Result.OreMedal == MedalTier.Gold,
            "Completing Level 4 evaluates medals and personal bests by stable level ID.");
        LevelExit.ShowResultsBannerForTest(level4Result);
        yield return Capture("level-4-results-banner", 1280, 720);
        GameObject bannerObj = GameObject.Find("LevelResultsBanner");
        if (bannerObj) Destroy(bannerObj);
        pause.TogglePause();
        Require(PauseMenu.GameIsPaused && Time.timeScale == 0f, "Pause works on the first click after Store return.");
        pause.LoadMenu();
        yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        Require(!PauseMenu.GameIsPaused && Time.timeScale == 1f, "Returning to level selection clears the pause state.");
        LevelSelector level4Button = Array.Find(FindObjectsOfType<LevelSelector>(), s => s.level == 4);
        Require(level4Button && level4Button.transform.Find("MedalBadge"),
            "Level selector displays the earned medal badge on the Level 4 button.");
        Require(GameObject.Find("Button_LevelCreator"),
            "Level selector creates the LEVEL CREATOR entry button.");
        yield return Capture("level-selector-after-gameplay", 1280, 720);
        Require(!FindObjectOfType<QuarryPressureHUD>(), "Level selector does not display the gameplay Pressure HUD.");
        Require(FindObjectsOfType<Collecting>().Length == 0 && FindObjectsOfType<EnemyPatrol2D>().Length == 0,
            "Level selection does not show or simulate persisted gameplay objects.");
        yield return SceneManager.LoadSceneAsync("Level 4");
        yield return new WaitForSecondsRealtime(0.2f);
        Require(FindObjectsOfType<Collecting>().Length == remainingCoins && FindObjectsOfType<ScenePersist>().Length == 1,
            "Selecting the same level restores its remaining pickups and keeps one persistent group.");
        FindObjectOfType<PauseMenu>().LoadMenu();
        yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        yield return CheckCustomLevelCreator(session);
        foreach (int level in new[] { 2, 3, 5, 6 })
        {
            persisted = FindObjectOfType<ScenePersist>();
            if (level != 2 && level != 3)
            {
                if (persisted) persisted.ResetScenePersist();
                yield return null;
            }
            session.HealStability(3);
            yield return SceneManager.LoadSceneAsync($"Level {level}");
            yield return new WaitForSecondsRealtime(0.3f);
            if (level == 2)
                Require(FindObjectOfType<ScenePersist>() && FindObjectOfType<ScenePersist>() != persisted,
                    "Selecting a different level replaces the previous level's persistent objects.");
            if (level == 3)
                Require(!persisted, "Leaving for a level without its own persistent group still removes the old group.");
            Require(FindObjectOfType<PlayerMovement>() && Camera.main && FindObjectOfType<QuarryPressureHUD>(),
                $"Level {level} initializes its player, camera, and Pressure HUD.");
            yield return Capture($"level-{level}", 1280, 720);
            if (level == 5) yield return CheckWater(session);
            if (level == 6) yield return CheckLava(session);
        }
        session.SaveFinalScoreForSummary();
        foreach (string scene in new[] { "Victory", "GameOver", "Start" })
        {
            yield return SceneManager.LoadSceneAsync(scene);
            yield return new WaitForSecondsRealtime(0.2f);
            Require(!FindObjectOfType<QuarryPressureHUD>(), $"{scene} has no gameplay Pressure HUD.");
            yield return Capture(scene.ToLowerInvariant(), 1280, 720);
        }
        Finish();
    }

    IEnumerator SurveyLevel()
    {
        foreach (ScenePersist persisted in FindObjectsOfType<ScenePersist>())
            Debug.Log($"SURVEY START PERSIST {persisted.name} in {persisted.gameObject.scene.name}");
        yield return SceneManager.LoadSceneAsync("Level 4");
        yield return new WaitForSecondsRealtime(0.3f);
        Time.timeScale = 0f;
        Camera camera = Camera.main;
        Require(camera, "Survey has an active gameplay camera.");
        camera.GetComponent<Cinemachine.CinemachineBrain>().enabled = false;
        camera.orthographicSize = 15f;
        camera.transform.position = new Vector3(7.5f, 3.5f, -10f);
        foreach (Canvas canvas in FindObjectsOfType<Canvas>()) canvas.enabled = false;
        yield return Capture("level-4-overview", 1920, 1080);
        foreach (Collecting coin in FindObjectsOfType<Collecting>(true))
            Debug.Log($"SURVEY COIN {coin.name}: {coin.transform.position}, active={coin.gameObject.activeInHierarchy}, ore={coin.BaseOreValue}");
        for (int x = -13; x <= 29; x++)
        {
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(new Vector2(x, 20f), Vector2.down, 40f,
                LayerMask.GetMask("Ground", "Jump")))
                Debug.Log($"SURVEY FLOOR x={x} y={hit.point.y} normal={hit.normal} object={hit.collider.name}");
        }
    }

    IEnumerator CheckWater(GameSession session)
    {
        Tilemap waterMap = null;
        Vector3Int waterCell = default;
        foreach (Tilemap map in FindObjectsOfType<Tilemap>())
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                TileBase tile = map.GetTile(cell);
                if (LiquidRules.ClassifyTile(tile ? tile.name : null, 5) != LiquidKind.Water) continue;
                Require(!(tile is LavaTile) && map.GetColor(cell) == Color.white,
                    "Level 5 water retains its original untinted tiles.");
                waterMap = map;
                waterCell = cell;
            }
        Require(waterMap, "Level 5 contains water for the compatibility test.");
        PlayerMovement player = FindObjectOfType<PlayerMovement>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 originalPosition = body.position;
        RigidbodyConstraints2D originalConstraints = body.constraints;
        int lives = session.GetLives();
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        MovePlayer(body, waterMap.GetCellCenterWorld(waterCell));
        yield return null;
        yield return null;
        Require(player.IsSwimming && !player.IsInLava && session.GetLives() == lives,
            "Real Level 5 water contact still swims rather than killing the player.");
        MovePlayer(body, originalPosition);
        body.constraints = originalConstraints;
        yield return null;
    }

    IEnumerator CheckLava(GameSession session)
    {
        Tilemap lavaMap = null;
        Vector3Int surfaceCell = default;
        int lavaCount = 0;
        int bestNeighbors = -1;
        foreach (Tilemap map in FindObjectsOfType<Tilemap>())
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                TileBase tile = map.GetTile(cell);
                if (LiquidRules.ClassifyTile(tile ? tile.name : null, 6) != LiquidKind.Lava) continue;
                lavaCount++;
                Require(tile is LavaTile && map.GetColor(cell) == Color.white &&
                    map.GetAnimationFrameCount(cell) == 4, "Level 6 lava has four animated, untinted frames.");
                if (tile.name != "LavaSurface") continue;
                int neighbors = (map.GetTile(cell + Vector3Int.left) == tile ? 1 : 0) +
                    (map.GetTile(cell + Vector3Int.right) == tile ? 1 : 0);
                if (neighbors <= bestNeighbors) continue;
                bestNeighbors = neighbors;
                lavaMap = map;
                surfaceCell = cell;
            }
        Require(lavaCount == 25 && lavaMap, "All 25 original Level 6 liquid cells have lava artwork and a visible surface.");
        int frameBefore = lavaMap.GetAnimationFrame(surfaceCell);
        yield return new WaitForSeconds(0.3f);
        Require(lavaMap.GetAnimationFrame(surfaceCell) != frameBefore, "Lava animation advances in the real player.");
        PlayerMovement player = FindObjectOfType<PlayerMovement>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        player.enabled = false;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        Camera camera = Camera.main;
        var brain = camera.GetComponent<Cinemachine.CinemachineBrain>();
        Vector3 originalCameraPosition = camera.transform.position;
        float originalSize = camera.orthographicSize;
        bool brainEnabled = brain.enabled;
        brain.enabled = false;
        Vector3 surface = lavaMap.GetCellCenterWorld(surfaceCell);
        camera.transform.position = new Vector3(surface.x, surface.y - 0.5f, -10f);
        camera.orthographicSize = 4f;
        yield return Capture("level-6-lava-closeup", 1280, 720);
        camera.transform.position = originalCameraPosition;
        camera.orthographicSize = originalSize;
        brain.enabled = brainEnabled;
        int lives = session.GetLives();
        int banked = session.GetCoins();
        session.AddCoins(100);
        player.ActivateInvisibility(10f);
        MovePlayer(body, surface);
        yield return new WaitForSeconds(0.25f);
        Require(player.IsInvisible && player.IsAlive && session.GetLives() == lives,
            "Invisibility still protects the player while inside lava.");
        player.CancelAllPowerupsImmediately();
        MovePlayer(body, surface);
        Require(player.GetLiquidAtPlayer() == LiquidKind.Lava, "Authored lava is detected by the actual player bounds query.");
        player.enabled = true;
        yield return Until(() => session.GetLives() == lives - 1, 2f,
            "Real lava contact remains immediately lethal after invisibility ends.");
        yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        Require(SceneManager.GetActiveScene().name == "Level 6" && FindObjectOfType<PlayerMovement>().IsAlive &&
            session.GetCoins() == banked && session.Pressure.PendingCoins == 0,
            "Lava death reloads Level 6, preserves banked coins, and loses only carried reward.");
    }

    IEnumerator CheckCustomLevelCreator(GameSession session)
    {
        foreach (string stale in Directory.GetFiles(CustomLevelSceneAdapter.GetCustomLevelsFolder(), "*.json"))
            File.Delete(stale);
        int campaignCoinsBefore = session.GetCoins();
        CustomLevelCreatorController.OpenCreatorForCampaignLevel(4);
        yield return null;
        yield return new WaitForSecondsRealtime(0.35f);
        CustomLevelCreatorController creator = FindObjectOfType<CustomLevelCreatorController>();
        Require(creator && creator.IsEditing && creator.CurrentCampaignLevel == 4 && creator.Document.Validate().Count == 0,
            "Level Creator opens in Edit Mode with a valid captured Level 4 document.");
        Require(creator.Document.tiles.Count > 100 && creator.Document.objects.Count >= 15,
            "Creator captures the real Level 4 Tilemaps and all authored objects.");
        Require(creator.ApplyToolAtCell(-5, 3, "bounce") && creator.Document.GetTile(-5, 3) == "bounce",
            "Creator paints a real bounce mushroom tile onto the Tilemap.");
        Require(creator.Undo() && creator.Document.GetTile(-5, 3) != "bounce",
            "Creator Undo removes the painted tile.");
        Require(creator.Redo() && creator.Document.GetTile(-5, 3) == "bounce",
            "Creator Redo restores the painted tile.");
        Require(!creator.ApplyToolAtCell(0, 25, "enemy"),
            "Creator rejects placing a grounded enemy high in mid-air with no floor support.");
        Require(creator.ApplyToolAtCell(-5, 5, "red_enemy") && creator.Document.GetObject(-5, 4) == "red_enemy",
            "Creator snaps a placed Red Enemy down onto the solid tile at (-5,3).");
        Require(creator.ApplyToolAtCell(-6, 3, "ground") && creator.ApplyToolAtCell(-6, 5, "jump_pad") &&
            creator.Document.GetObject(-6, 4) == "jump_pad",
            "Creator snaps a placed JumpPad down onto the solid tile at (-6,3).");
        Require(creator.ApplyToolAtCell(-2, 2, "coin_200") && creator.Document.GetObject(-2, 2) == "coin_200",
            "Creator places a 200-ore coin prefab onto the level.");
        string draftPath = Path.Combine(outputDirectory, "creator-draft.json");
        Require(creator.SaveDraft(draftPath) && File.Exists(draftPath),
            "Creator saves a validated JSON draft to disk.");
        Require(creator.SaveCurrentLevelCampaignOverride() && CustomLevelSceneAdapter.HasCampaignOverride(4),
            "Creator saves a live Campaign Override for Level 4.");
        int savedStageNum = creator.SaveAsPlayableCustomStage(7);
        Require(savedStageNum == 7 && CustomLevelSceneAdapter.GetSavedCustomStageNumbers().Contains(7),
            "Creator saves a playable Custom Stage 7 for the Level Selector.");
        yield return Capture("level-creator-edit", 1280, 720);
        creator.StartPlaytest();
        yield return null;
        yield return new WaitForSecondsRealtime(0.25f);
        Require(!creator.IsEditing && session.IsPlaytestMode && session.GetCoins() == 0,
            "Creator Playtest enters isolated playtest mode with zero disposable coins.");
        PlayerMovement playtestPlayer = FindObjectOfType<PlayerMovement>();
        Require(playtestPlayer && playtestPlayer.gameObject.activeInHierarchy,
            "Creator Playtest activates the playable character at the spawn point.");
        yield return Capture("level-creator-playtest", 1280, 720);
        session.AddCoins(300);
        session.BankOre();
        Require(session.GetCoins() == 300 && PlayerPrefs.GetInt(GameSession.CoinsKey) == campaignCoinsBefore,
            "Playtest banking never alters the campaign PlayerPrefs coin balance.");
        creator.EnterEditMode();
        yield return null;
        Require(creator.IsEditing && !session.IsPlaytestMode && session.GetCoins() == campaignCoinsBefore &&
            creator.Document.GetTile(-5, 3) == "bounce" && creator.Document.GetObject(-5, 4) == "red_enemy",
            "Returning to Edit Mode restores campaign state while keeping all level edits.");
        Require(creator.LoadDraft(draftPath) && creator.Document.GetObject(-2, 2) == "coin_200",
            "Creator loads the saved JSON draft cleanly.");
        creator.ExitCreatorToLevelSelector();
        yield return null;
        yield return new WaitForSecondsRealtime(0.25f);
        Require(SceneManager.GetActiveScene().name == "Level selector" && !session.IsPlaytestMode,
            "Exiting the Level Creator returns cleanly to the Level selector.");
        Require(GameObject.Find("Button_BackToStart") != null && GameObject.Find("Button_NextPage") != null,
            "Level selector creates Back to Menu and side-arrow pagination controls.");
        RectTransform backRect = GameObject.Find("Button_BackToStart").GetComponent<RectTransform>();
        RectTransform prevRect = GameObject.Find("Button_PrevPage").GetComponent<RectTransform>();
        RectTransform nextRect = GameObject.Find("Button_NextPage").GetComponent<RectTransform>();
        RectTransform creatorRect = GameObject.Find("Button_LevelCreator").GetComponent<RectTransform>();
        Require(!RectsOverlapScreen(backRect, prevRect) && !RectsOverlapScreen(nextRect, creatorRect) &&
            !RectsOverlapScreen(backRect, creatorRect),
            "Level selector side-arrow pagination boxes and bottom buttons never overlap.");
        PlayerPrefs.SetInt(StartMenu.UnlockedLevelKey, 3);
        LevelSelector.SetPage(0);
        LevelSelector slot5 = Array.Find(FindObjectsOfType<LevelSelector>(), s => s.level == 5);
        Require(slot5 && !slot5.GetComponent<UnityEngine.UI.Button>().interactable,
            "Unpassed campaign levels (e.g. Level 5 when unlocked=3) are locked and non-interactable.");
        yield return Capture("level-selector-locked-campaign", 800, 600);
        yield return Capture("level-selector-locked-campaign", 1280, 720);
        PlayerPrefs.SetInt(StartMenu.UnlockedLevelKey, 6);
        LevelSelector.SetPage(1);
        Require(LevelSelector.CurrentPage == 1 && Array.Find(FindObjectsOfType<LevelSelector>(), s => s.level == 7) != null,
            "Switching to Page 2 displays playable Custom Stage S7 in the level grid.");
        yield return Capture("level-selector-with-custom-stage", 1280, 720);
        LevelSelector.SetPage(0);
        CustomLevelSceneAdapter.DeleteCampaignOverride(4);
        File.Delete(CustomLevelSceneAdapter.GetCustomStageFilePath(7));
        LevelSelector.SetPage(0);
    }

    IEnumerator CheckEnemyAwareness(GameSession session)
    {
        EnemyPatrol2D source = FindObjectOfType<EnemyPatrol2D>();
        PlayerMovement player = FindObjectOfType<PlayerMovement>();
        Require(source && player, "Level 4 supplies the real enemy and player for awareness checks.");
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        Vector2 originalPosition = playerBody.position;
        RigidbodyConstraints2D originalConstraints = playerBody.constraints;
        player.enabled = false;
        playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        var fixture = new GameObject("Validation awareness fixture");
        EnemyPatrol2D enemy = Instantiate(source, new Vector3(1000f, 1000f), Quaternion.identity, fixture.transform);
        Vector3 scale = enemy.transform.localScale;
        enemy.transform.localScale = new Vector3(Mathf.Abs(scale.x), scale.y, scale.z);
        Rigidbody2D enemyBody = enemy.GetComponent<Rigidbody2D>();
        enemyBody.gravityScale = 0f;
        enemyBody.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
        CreateTestGround(fixture.transform, "Floor", new Vector2(1000f, 999f), new Vector2(40f, 1f));
        BoxCollider2D wall = CreateTestGround(fixture.transform, "Sight blocker",
            new Vector2(1001.5f, 1001f), new Vector2(0.3f, 3f));
        try
        {
            MovePlayer(playerBody, new Vector2(1003f, 1000f));
            yield return new WaitForSeconds(0.25f);
            Require(enemy.CurrentState == EnemyPatrol2D.EnemyState.Patrol,
                "Real Ground-layer geometry blocks enemy sight even with the player in range.");
            wall.enabled = false;
            Physics2D.SyncTransforms();
            yield return Until(() => enemy.CurrentState == EnemyPatrol2D.EnemyState.Alert, 1f,
                "Removing the sight blocker lets the patrol notice the player.");
            yield return Until(() => enemy.CurrentState == EnemyPatrol2D.EnemyState.Chase, 1f,
                "Visible player triggers Alert then Chase.");
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float calmSpeed = Mathf.Abs(enemyBody.velocity.x);
            float calmRange = enemy.DetectionRange;
            Require(calmSpeed > 0f && calmRange > 0f, "Calm chase has measurable speed and detection range.");
            MovePlayer(playerBody, enemyBody.position + Vector2.right * (calmRange + 0.3f));
            yield return Until(() => enemy.CurrentState == EnemyPatrol2D.EnemyState.Search, 0.2f,
                "A player beyond calm detection range is lost from sight.");
            foreach (int ore in new[] { 500, 1000, 1500 })
            {
                session.AddCoins(ore);
                Require(Mathf.Approximately(enemy.DetectionRange, calmRange * session.Pressure.PerceptionMultiplier),
                    $"Tier {session.Pressure.Tier} scales the real enemy detection range.");
                yield return Until(() => enemy.CurrentState == EnemyPatrol2D.EnemyState.Chase, 0.5f,
                    $"Tier {session.Pressure.Tier} detects the player beyond the original calm range.");
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Require(Mathf.Abs(Mathf.Abs(enemyBody.velocity.x) - calmSpeed * session.Pressure.ChaseMultiplier) < 0.01f,
                    $"Tier {session.Pressure.Tier} applies the seeded pursuit speed to real movement.");
            }
            player.ActivateInvisibility(10f);
            yield return Until(() => enemy.CurrentState == EnemyPatrol2D.EnemyState.Search, 0.5f,
                "Invisibility breaks an active enemy chase.");
            yield return new WaitForSeconds(0.25f);
            Require(enemy.CurrentState == EnemyPatrol2D.EnemyState.Search,
                "An invisible player is not immediately reacquired.");
            player.CancelAllPowerupsImmediately();
            yield return Until(() => enemy.CurrentState == EnemyPatrol2D.EnemyState.Chase, 0.5f,
                "A visible player in range is reacquired after invisibility ends.");
            wall.transform.position = (enemyBody.position + playerBody.position) * 0.5f;
            wall.enabled = true;
            Physics2D.SyncTransforms();
            yield return Until(() => enemy.CurrentState == EnemyPatrol2D.EnemyState.Search, 0.5f,
                "Ground geometry also breaks an established chase.");
            session.BankOre();
            Require(Mathf.Approximately(enemy.DetectionRange, calmRange),
                "Banking restores the enemy's original perception range.");
        }
        finally
        {
            player.CancelAllPowerupsImmediately();
            MovePlayer(playerBody, originalPosition);
            playerBody.constraints = originalConstraints;
            player.enabled = true;
            Destroy(fixture);
        }
        yield return null;
    }

    static BoxCollider2D CreateTestGround(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var ground = new GameObject(name);
        ground.transform.SetParent(parent, false);
        ground.transform.position = position;
        ground.layer = LayerMask.NameToLayer("Ground");
        BoxCollider2D collider = ground.AddComponent<BoxCollider2D>();
        collider.size = size;
        return collider;
    }

    IEnumerator CheckPressureEncounter(GameSession session)
    {
        int availableOre = 0;
        foreach (Collecting coin in FindObjectsOfType<Collecting>()) availableOre += coin.BaseOreValue;
        Require(availableOre >= 1500, "Level 4 contains enough active base ore to arm its vent without upgrades.");
        OreBankCheckpoint bank = FindObjectOfType<OreBankCheckpoint>();
        PressurePulseHazard vent = FindObjectOfType<PressurePulseHazard>();
        PlayerMovement player = FindObjectOfType<PlayerMovement>();
        Require(bank && vent && player, "Level 4 contains the authored checkpoint, vent, and player.");
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        RigidbodyConstraints2D originalConstraints = body.constraints;
        Vector2 originalPosition = body.position;
        player.enabled = false;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        session.HealStability(3);
        int banked = session.GetCoins();
        TextMeshPro ventLabel = vent.GetComponentInChildren<TextMeshPro>();
        MovePlayer(body, vent.transform.position + Vector3.up * 0.5f);
        int dormantHits = session.GetHitsTaken();
        yield return Capture("pressure-vent-off", 800, 600);
        Require(ventLabel.text == "OFF" && !vent.IsActive && session.GetHitsTaken() == dormantHits,
            "Disarmed vent explicitly says OFF and touching it is harmless.");
        session.AddCoins(500);
        yield return null;
        yield return null;
        Require(ventLabel.text == "OFF" && !vent.IsActive, "Tier 1 still leaves the vent OFF.");
        session.AddCoins(1000);
        Require(session.Pressure.Tier == 2 && session.Pressure.PendingCoins == 1750,
            "Real session uses pre-pickup rewards at the tier-2 threshold.");
        MovePlayer(body, vent.transform.position + Vector3.up * 0.5f);
        float safeStart = Time.time;
        int initialHits = session.GetHitsTaken();
        yield return Capture("pressure-vent-safe", 800, 600);
        Require(ventLabel.text == "SAFE" && !vent.IsActive,
            "An armed vent distinguishes its safe interval from the disarmed OFF state.");
        while (Time.time - safeStart < 3.2f) yield return new WaitForFixedUpdate();
        Require(session.GetHitsTaken() == initialHits && !vent.IsActive, "Vent grace period causes no contact damage.");
        SpriteRenderer indicator = vent.GetComponent<SpriteRenderer>();
        yield return Until(() => indicator.color.r > 0.9f && indicator.color.g > 0.7f, 2f, "Vent enters a visible warning phase.");
        Require(!vent.IsActive && session.GetHitsTaken() == initialHits, "Warning phase is harmless on contact.");
        Require(vent.GetComponentInChildren<TextMeshPro>().text == "WARNING",
            "Vent warning is communicated with text as well as color.");
        yield return Capture("pressure-vent-warning", 1280, 720);
        Time.timeScale = 0f;
        Color warningColor = indicator.color;
        Vector3 warningScale = vent.transform.localScale;
        yield return new WaitForSecondsRealtime(0.6f);
        Require(!vent.IsActive && indicator.color == warningColor && vent.transform.localScale == warningScale,
            "Pause freezes the vent warning phase and animation.");
        Time.timeScale = 1f;
        yield return Until(() => session.GetHitsTaken() > initialHits, 2f, "Active vent damages the touching player.");
        Require(session.GetHitsTaken() == initialHits + 1 && session.GetStability() == 2f,
            "Multiple player colliders produce one Stability hit.");
        Require(vent.GetComponentInChildren<TextMeshPro>().text == "DANGER", "Active vent has an explicit danger label.");
        yield return Capture("pressure-vent-active", 1280, 720);
        Require(session.GetHitsTaken() == initialHits + 1, "Hit invulnerability prevents repeated pulse damage.");
        MovePlayer(body, bank.transform.position + Vector3.up * 0.5f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Require(session.GetCoins() == banked + 1750 && session.Pressure.CarriedOre == 0 &&
            session.Pressure.PendingCoins == 0, "Checkpoint contact banks the exact reward once across player colliders.");
        Require(!vent.IsActive, "Banking immediately disarms the vent.");
        yield return null;
        yield return null;
        Require(ventLabel.text == "OFF", "Banking returns the vent label to OFF.");
        Require(PlayerPrefs.GetInt(StartMenu.UnlockedLevelKey) == 6, "Checkpoint banking preserves level unlocks.");
        yield return Capture("pressure-checkpoint-banked", 1280, 720);
        MovePlayer(body, bank.transform.position + Vector3.up * 4f);
        yield return new WaitForFixedUpdate();
        MovePlayer(body, bank.transform.position + Vector3.up * 0.5f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Require(session.GetCoins() == banked + 1750, "Re-entering an empty checkpoint does not duplicate money.");
        Require(session.PurchaseArmorUpgrade(), "Earned banked ore can purchase armor.");
        int armorTier = session.GetArmorTier();
        int armoredBalance = session.GetCoins();
        session.AddCoins(500);
        session.AddCoins(1000);
        yield return new WaitForFixedUpdate();
        Require(session.Pressure.CarriedOre == 1500, "A checkpoint does not continuously bank while standing inside.");
        MovePlayer(body, vent.transform.position + Vector3.up * 0.5f);
        player.ActivateInvisibility(12f);
        int hitsBeforeGhost = session.GetHitsTaken();
        yield return Until(() => vent.IsActive, 7f, "Vent rearms after new ore is collected.");
        yield return new WaitForSeconds(0.15f);
        Require(player.IsInvisible && session.GetHitsTaken() == hitsBeforeGhost, "Invisibility prevents active vent damage.");
        session.AddCoins(1500);
        yield return null;
        yield return null;
        Require(session.Pressure.Tier == 3 && !vent.IsActive, "Changing pressure tiers restarts the vent's safe phase.");
        int secondDeposit = session.Pressure.PendingCoins;
        MovePlayer(body, bank.transform.position + Vector3.up * 0.5f);
        player.CancelAllPowerupsImmediately();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Require(session.GetCoins() == armoredBalance + secondDeposit && session.GetArmorTier() == armorTier,
            "Checkpoint banking preserves purchased armor and the exact carried reward.");
        MovePlayer(body, vent.transform.position + Vector3.up * 0.5f);
        session.HealStability(3);
        session.AddCoins(1500);
        int armoredHits = session.GetHitsTaken();
        float stabilityBeforeHit = session.GetStability();
        float expectedDamage = StoreEconomy.ApplyArmorReductionUnits(
            vent.Damage * QuantumStability.UnitsPerPoint, armorTier) / (float)QuantumStability.UnitsPerPoint;
        yield return Until(() => session.GetHitsTaken() > armoredHits, 7f,
            "A real vent contact still damages an armored player.");
        Require(session.GetHitsTaken() == armoredHits + 1 &&
            Mathf.Approximately(session.GetStability(), stabilityBeforeHit - expectedDamage),
            "Vent contact uses the exact armor rounding rule rather than granting immunity.");
        Require(player.GetComponent<PlayerStability>().IsInvulnerable,
            "A vent hit starts the player's invulnerability window.");
        session.BankOre();
        MovePlayer(body, originalPosition);
        body.constraints = originalConstraints;
        player.enabled = true;
        session.HealStability(3);
        int balanceBeforeDeath = session.GetCoins();
        int livesBeforeDeath = session.GetLives();
        session.AddCoins(100);
        player.Kill(Vector2.zero);
        yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        Require(session.GetCoins() == balanceBeforeDeath && session.Pressure.CarriedOre == 0 &&
            session.GetLives() == livesBeforeDeath - 1 && session.GetStability() == session.GetMaxStability(),
            "Death loses only unbanked ore, consumes one life, and restores Stability.");
        session.AddCoins(100);
        session.ResetCurrentLevelToStart();
        yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        Require(session.GetCoins() == balanceBeforeDeath && session.Pressure.PendingCoins == 0 &&
            session.GetLives() == livesBeforeDeath - 1, "Manual reset loses only unbanked ore without consuming a life.");
        Collecting[] resetCoins = FindObjectsOfType<Collecting>();
        BreakableOreCrate[] resetCrates = FindObjectsOfType<BreakableOreCrate>();
        PressureGate gate = FindObjectOfType<PressureGate>();
        Require(resetCoins.Length == 15 && resetCrates.Length == 2 && gate,
            "Manual reset restores all 15 authored Level 4 coins, 2 breakable crates, and the pressure gate.");
        Collecting pickup = Array.Find(resetCoins, coin => coin.name == "Pressure Route Ore 01");
        Require(pickup, "The first route pickup exists for a real collection test.");
        player = FindObjectOfType<PlayerMovement>();
        body = player.GetComponent<Rigidbody2D>();
        originalConstraints = body.constraints;
        originalPosition = body.position;
        player.enabled = false;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        int expectedReward = session.Pressure.PreviewReward(pickup.BaseOreValue, session.IsCriticalStability());
        MovePlayer(body, pickup.transform.position);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Require(session.Pressure.PendingCoins == expectedReward && session.Pressure.CarriedOre == 100,
            "A real coin trigger awards its previewed value once across player colliders.");
        yield return new WaitForSecondsRealtime(0.6f);
        Require(FindObjectsOfType<Collecting>().Length == resetCoins.Length - 1, "Collected ore is removed from the persistent level objects.");
        Collider2D gateCollider = gate.GetComponent<Collider2D>();
        TextMeshPro gateLabel = gate.GetComponentInChildren<TextMeshPro>();
        Require(!gate.IsOpen && gateCollider.enabled && gateLabel.text == "LOCK T1",
            "Pressure gate is solid and locked below its required tier.");
        MovePlayer(body, gate.transform.position + Vector3.left * 1.4f);
        yield return Capture("pressure-vault-locked", 1280, 720);
        session.AddCoins(400);
        yield return null;
        yield return null;
        Require(session.Pressure.Tier == 1 && gate.IsOpen && !gateCollider.enabled && gateLabel.text == "OPEN",
            "Reaching tier 1 opens the Pressure gate and disables its solid barrier.");
        yield return Capture("pressure-vault-open", 1280, 720);
        BreakableOreCrate firstCrate = Array.Find(resetCrates, c => c.name == "Pressure Vault Crate 01");
        Require(firstCrate && !firstCrate.IsBroken, "First vault crate is ready for a real bullet test.");
        int pendingBeforeCrate = session.Pressure.PendingCoins;
        int expectedCrateReward = session.Pressure.PreviewReward(firstCrate.BaseOreValue, session.IsCriticalStability());
        MovePlayer(body, firstCrate.transform.position + Vector3.left * 1.6f);
        player.transform.localScale = new Vector3(1f, 1f, 1f);
        Bullets bulletPrefab = FindObjectOfType<Bullets>(true);
        var serializedPlayer = player.GetType().GetField("bullet",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var gunField = player.GetType().GetField("gun",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        GameObject configuredBullet = serializedPlayer != null ? (GameObject)serializedPlayer.GetValue(player) : null;
        Transform gunTransform = gunField != null ? (Transform)gunField.GetValue(player) : player.transform;
        Require(configuredBullet && gunTransform, "Player has a configured bullet prefab and gun transform.");
        Instantiate(configuredBullet, new Vector3(firstCrate.transform.position.x - 0.8f, firstCrate.transform.position.y, 0f), Quaternion.identity);
        yield return Until(() => firstCrate.IsBroken, 1.5f, "A real player bullet shatters the breakable ore crate.");
        Require(session.Pressure.PendingCoins == pendingBeforeCrate + expectedCrateReward &&
            session.Pressure.CarriedOre == 750,
            "Shattering a crate awards the exact pressure-scaled ore reward once.");
        yield return new WaitForSecondsRealtime(0.6f);
        Require(FindObjectsOfType<BreakableOreCrate>().Length == 1,
            "Shattered crate is destroyed and removed from persistent level objects.");
        JumpPad jumpPad = FindObjectOfType<JumpPad>();
        SentryTurret sentry = FindObjectOfType<SentryTurret>();
        ShieldedEnemyGuard shielded = FindObjectOfType<ShieldedEnemyGuard>();
        Require(jumpPad && sentry && shielded,
            "Level 4 contains the authored JumpPad, SentryTurret, and ShieldedEnemy.");
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        MovePlayer(body, jumpPad.transform.position);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Require(body.velocity.y >= jumpPad.LaunchVelocity - 1.5f && jumpPad.IsFlashing,
            "Touching the JumpPad launches the player upward and flashes BOOST!.");
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        EnemyPatrol2D shieldedPatrol = shielded.GetComponent<EnemyPatrol2D>();
        shieldedPatrol.enabled = false;
        shielded.transform.localScale = new Vector3(1f, 1f, 1f);
        MovePlayer(body, shielded.transform.position + Vector3.right * 1.6f);
        player.transform.localScale = new Vector3(-1f, 1f, 1f);
        Instantiate(configuredBullet, new Vector3(shielded.transform.position.x + 0.7f, shielded.transform.position.y, 0f), Quaternion.identity);
        yield return Until(() => shielded.RecentlyDeflected, 1.2f,
            "A frontal bullet is deflected by the ShieldedEnemy's front shield.");
        Require(shielded != null && shielded.gameObject != null,
            "ShieldedEnemy survives a frontal bullet hit.");
        yield return Capture("shielded-enemy-blocked", 1280, 720);
        MovePlayer(body, shielded.transform.position + Vector3.left * 1.6f);
        player.transform.localScale = new Vector3(1f, 1f, 1f);
        Instantiate(configuredBullet, new Vector3(shielded.transform.position.x - 0.7f, shielded.transform.position.y, 0f), Quaternion.identity);
        yield return Until(() => !shielded, 1.2f,
            "A bullet from behind defeats the ShieldedEnemy.");
        session.HealStability(3);
        int hitsBeforeSentry = session.GetHitsTaken();
        MovePlayer(body, sentry.transform.position + Vector3.left * 2.2f);
        yield return Until(() => sentry.Phase == SentryTurret.SentryPhase.Aiming, 1.2f,
            "SentryTurret enters its AIM! warning phase when the player is in line of sight.");
        yield return Capture("sentry-turret-aiming", 1280, 720);
        yield return Until(() => session.GetHitsTaken() > hitsBeforeSentry, 2f,
            "SentryTurret transitions to FIRE! and damages the visible player after the aim warning.");
        sentry.enabled = false;
        MovePlayer(body, originalPosition);
        body.constraints = originalConstraints;
        player.enabled = true;
    }

    static void MovePlayer(Rigidbody2D body, Vector2 position)
    {
        body.velocity = Vector2.zero;
        body.position = position;
        Physics2D.SyncTransforms();
    }

    static bool RectsOverlapScreen(RectTransform a, RectTransform b)
    {
        if (!a || !b) return false;
        Vector3[] ca = new Vector3[4];
        Vector3[] cb = new Vector3[4];
        a.GetWorldCorners(ca);
        b.GetWorldCorners(cb);
        Rect ra = new Rect(ca[0].x, ca[0].y, ca[2].x - ca[0].x, ca[2].y - ca[0].y);
        Rect rb = new Rect(cb[0].x, cb[0].y, cb[2].x - cb[0].x, cb[2].y - cb[0].y);
        return ra.Overlaps(rb);
    }

    IEnumerator Until(Func<bool> condition, float seconds, string message)
    {
        float expires = Time.time + seconds;
        while (!condition() && Time.time < expires) yield return null;
        Require(condition(), message);
    }

    IEnumerator Capture(string label, int width, int height)
    {
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
        yield return new WaitForSecondsRealtime(0.3f);
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        Require(Screen.width == width && Screen.height == height, $"Capture resolution is {width}x{height}.");
        Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
        try
        {
            string path = Path.Combine(outputDirectory, $"{label}-{width}x{height}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            report.screenshots.Add(path);
        }
        finally { Destroy(image); }
        foreach (TextMeshProUGUI text in FindObjectsOfType<TextMeshProUGUI>())
        {
            if (text.isActiveAndEnabled && text.text.Length > 0)
            {
                Debug.Log($"VALIDATION TEXT [{label}] {text.name}: {text.text} | overflow={text.isTextOverflowing}");
                if (text.GetComponentInParent<QuarryPressureHUD>())
                {
                    Require(!text.isTextOverflowing, $"Pressure text fits at {width}x{height}: {label}/{text.transform.parent.name}.");
                    var corners = new Vector3[4];
                    text.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                        Require(corner.x >= 0f && corner.x <= Screen.width && corner.y >= 0f && corner.y <= Screen.height,
                            $"Pressure label corner is onscreen: {label}/{width}x{height}.");
                }
            }
        }
    }

    void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        report.checks.Add(message);
    }

    void OnLog(string message, string stack, LogType type)
    {
        if (finished || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        report.errors.Add(message + "\n" + stack);
        Finish();
    }

    void Update()
    {
        if (!finished && Time.realtimeSinceStartup > deadline)
        {
            report.errors.Add("Runtime validation timed out.");
            Finish();
        }
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        Application.logMessageReceived -= OnLog;
        report.passed = report.errors.Count == 0;
        File.WriteAllText(Path.Combine(outputDirectory, "report.json"), JsonUtility.ToJson(report, true));
        Debug.Log($"Quarry runtime validation {(report.passed ? "passed" : "failed")}.");
        Application.Quit(report.passed ? 0 : 1);
    }
}
#endif
