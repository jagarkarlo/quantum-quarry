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
        yield return ResizeOpenUI("start-menu", GameObject.Find("Button_FrontLevelCreator").GetComponentInParent<Canvas>(), centeredMenu: true);
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
        yield return ResizeOpenUI("gameplay-hud", null);
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
        yield return ResizeOpenUI("pause", pause.pauseMenuUI.GetComponentInParent<Canvas>());
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
        Require(GameObject.Find("Button_Pickaxe") != null && GameObject.Find("Button_Magnet") != null,
            "Store displays permanent Pickaxe and Magnet upgrade buttons.");
        yield return ResizeOpenUI("store", GameObject.Find("StorePanel").GetComponentInParent<Canvas>(), centeredMenu: true);
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
        yield return ResizeOpenUI("result-banner", GameObject.Find("LevelResultsBanner").GetComponent<Canvas>());
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
        yield return ResizeOpenUI("selector", level4Button.GetComponentInParent<Canvas>(), centeredMenu: true);
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
        PlayerPrefs.SetInt(GameSession.FinalCoinsKey, int.MaxValue);
        PlayerPrefs.SetInt(GameSession.LastScoreKey, int.MaxValue);
        foreach (string scene in new[] { "Victory", "GameOver", "Start" })
        {
            yield return SceneManager.LoadSceneAsync(scene);
            yield return new WaitForSecondsRealtime(0.2f);
            Require(!FindObjectOfType<QuarryPressureHUD>(), $"{scene} has no gameplay Pressure HUD.");
            yield return Capture(scene.ToLowerInvariant(), 1280, 720);
            if (scene != "Start")
            {
                var canvas = GameObject.Find("TitleText").GetComponentInParent<Canvas>();
                var scores = Array.FindAll(canvas.GetComponentsInChildren<TextMeshProUGUI>(), t => t.text == int.MaxValue.ToString());
                Require(scores.Length == (scene == "Victory" ? 1 : 2) && Array.TrueForAll(scores, t => t.textInfo.lineCount == 1),
                    $"{scene} displays the longest possible score on one line.");
                yield return ResizeOpenUI(scene, canvas, centeredMenu: true);
            }
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
        Require(creator.ApplyToolAtCell(-7, 3, "ground") && creator.ApplyToolAtCell(-7, 5, "gauge") &&
            creator.Document.GetObject(-7, 4) == "gauge",
            "Creator snaps a placed PressureGauge down onto the solid tile at (-7,3).");
        Require(File.Exists(creator.GetAutosaveFilePath()),
            "Creator automatically writes an autosave recovery snapshot after edits.");
        Require(creator.ApplyToolAtCell(-2, 2, "coin_200") && creator.Document.GetObject(-2, 2) == "coin_200",
            "Creator places a 200-ore coin prefab onto the level.");
        GameObject.Find("Btn_Objects").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Require(!creator.ApplyToolAtCell(-20, 40) && !creator.ApplyToolAtCell(-18, 42) &&
            creator.ApplyToolAtCell(-20, 42) && !creator.ApplyToolAtCell(-19, 41, "rock"),
            "Rail tool keeps the first anchor after a bad end and rejects terrain across the finished lift.");
        Require(creator.Undo() && creator.Document.GetObject(-20, 40) == null && creator.Redo(),
            "Both rail endpoints are restored by one undo/redo operation.");
        string draftPath = Path.Combine(outputDirectory, "creator-draft.json");
        Require(creator.SaveDraft(draftPath) && File.Exists(draftPath),
            "Creator saves a validated JSON draft to disk.");
        string sharePath = Path.Combine(outputDirectory, "shared-level.qqlevel");
        Require(creator.ExportShareFile(sharePath) && File.Exists(sharePath),
            "Creator exports a checksummed .qqlevel file.");
        Require(creator.ImportShareFile(sharePath) && creator.Document.GetObject(-2, 2) == "coin_200",
            "Creator imports and verifies the checksummed .qqlevel file.");
        creator.GenerateProceduralDraft(7319);
        Require(creator.Document.Validate().Count == 0 && creator.Document.levelId.StartsWith("proc-"),
            "Creator generates a valid deterministic procedural level.");
        yield return CheckGeneratedMovement(creator);
        Require(creator.LoadDraft(draftPath) && creator.Document.GetObject(-2, 2) == "coin_200",
            "Creator reloads the edited Level 4 draft after procedural preview.");
        var lift = FindObjectOfType<FollowingPoint>();
        Vector2 expectedA = new Vector2(-19.5f, 40.5f), expectedB = expectedA + Vector2.up * 2f;
        Require(lift && lift.TryGetRoute(out Vector2 liftA, out Vector2 liftB) &&
            liftA == expectedA && liftB == expectedB,
            "A Unity-written draft reloads both vertical waypoints into the real platform prefab.");
        Require(creator.SaveCurrentLevelCampaignOverride() && CustomLevelSceneAdapter.HasCampaignOverride(4),
            "Creator saves a live Campaign Override for Level 4.");
        int savedStageNum = creator.SaveAsPlayableCustomStage(7);
        Require(savedStageNum == 7 && CustomLevelSceneAdapter.GetSavedCustomStageNumbers().Contains(7),
            "Creator saves a playable Custom Stage 7 for the Level Selector.");
        yield return Capture("level-creator-edit", 1280, 720);
        CheckCreatorButtons();
        yield return Capture("level-creator-rail-tools", 800, 600);
        CheckCreatorButtons();
        yield return ResizeOpenUI("creator", GameObject.Find("CustomCreatorCanvas").GetComponent<Canvas>());
        creator.StartPlaytest();
        yield return null;
        yield return new WaitForSecondsRealtime(0.25f);
        Require(!creator.IsEditing && session.IsPlaytestMode && session.GetCoins() == 0,
            "Creator Playtest enters isolated playtest mode with zero disposable coins.");
        PlayerMovement playtestPlayer = FindObjectOfType<PlayerMovement>();
        Require(playtestPlayer && playtestPlayer.gameObject.activeInHierarchy,
            "Creator Playtest activates the playable character at the spawn point.");
        lift = FindObjectOfType<FollowingPoint>();
        yield return Until(() => lift.Velocity.y < -1f && lift.transform.position.y > 41.5f, 2f,
            "The saved vertical lift travels up and starts back down at its authored end.");
        yield return Capture("level-creator-playtest", 1280, 720);
        yield return ResizeOpenUI("playtest", GameObject.Find("CustomCreatorCanvas").GetComponent<Canvas>());
        var transport = (RectTransform)GameObject.Find("Btn_Playtest").transform.parent;
        foreach (var pressureLabel in FindObjectOfType<QuarryPressureHUD>().GetComponentsInChildren<TextMeshProUGUI>())
            Require(!RectsOverlapScreen(transport, pressureLabel.rectTransform), "Playtest controls leave the Pressure HUD uncovered.");
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
        Array.Find(FindObjectsOfType<LevelSelector>(), s => s.level == 7).OpenScene();
        yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        var savedLift = FindObjectOfType<FollowingPoint>();
        Require(!FindObjectOfType<CustomLevelCreatorController>() && savedLift &&
            savedLift.TryGetRoute(out Vector2 savedA, out Vector2 savedB) && savedA == expectedA && savedB == expectedB &&
            savedLift.Velocity.y > 0f, "Selecting S7 launches the published vertical lift in normal gameplay.");
        PlayerPrefs.SetInt(CustomLevelSceneAdapter.ActiveCustomStageKey, 0);
        LevelSelector.SetPage(0);
        CustomLevelSceneAdapter.DeleteCampaignOverride(4);
        File.Delete(CustomLevelSceneAdapter.GetCustomStageFilePath(7));
        LevelSelector.SetPage(0);
    }

    void CheckCreatorButtons()
    {
        var buttons = GameObject.Find("CustomCreatorCanvas").GetComponentsInChildren<UnityEngine.UI.Button>();
        bool fit = true;
        for (int i = 0; i < buttons.Length; i++)
        {
            var r = (RectTransform)buttons[i].transform;
            var corners = new Vector3[4];
            r.GetWorldCorners(corners);
            fit &= corners[0].x >= 0 && corners[0].y >= 0 && corners[2].x <= Screen.width && corners[2].y <= Screen.height;
            for (int j = 0; j < i; j++) fit &= !RectsOverlapScreen(r, (RectTransform)buttons[j].transform);
        }
        Require(fit && GameObject.Find("Btn_gate_t2"), $"All creator object tools fit without overlap at {Screen.width}x{Screen.height}.");
    }

    IEnumerator CheckGeneratedMovement(CustomLevelCreatorController creator)
    {
        for (int x = -15; x <= -13; x++) creator.ApplyToolAtCell(x, 6, "ground");
        Require(creator.ApplyToolAtCell(-14, 7, "enemy"), "Creator places a patrol on a three-cell test ledge.");
        CustomLevelCell rail = creator.Document.objects.Find(c => c.id == "platform");
        Camera.main.transform.position = new Vector3(rail.exactX, rail.exactY + 1f, -10f);
        yield return Capture("seeded-ferry-guides", 1280, 720);
        creator.StartPlaytest();
        yield return new WaitForFixedUpdate();
        var ferry = FindObjectOfType<FollowingPoint>();
        var player = FindObjectOfType<PlayerMovement>().GetComponent<Rigidbody2D>();
        var deck = ferry.GetComponent<Rigidbody2D>();
        var patrol = Array.Find(FindObjectsOfType<EnemyPatrol2D>(), p => p.transform.position.x < 0).GetComponent<Rigidbody2D>();
        MovePlayer(player, deck.position + Vector2.up * 0.8f);
        float min = patrol.position.x, max = min;
        float furthest = deck.position.x;
        bool stayedAboard = true, stayedOnLedge = true;
        int ticks = Mathf.CeilToInt((rail.spanX + 0.6f) / Time.fixedDeltaTime); // At speed 2, this spans a return trip.
        for (int i = 0; i < ticks; i++)
        {
            yield return new WaitForFixedUpdate();
            if (!creator.IsEditing && deck && patrol && player)
            {
                min = Mathf.Min(min, patrol.position.x);
                max = Mathf.Max(max, patrol.position.x);
                stayedOnLedge &= min >= -14.61f && max <= -12.39f && Mathf.Abs(patrol.position.y - 7.5f) < 0.1f;
                stayedAboard &= Mathf.Abs(player.position.x - deck.position.x) < 0.95f &&
                    player.position.y > deck.position.y && player.position.y < deck.position.y + 1.1f;
                furthest = Mathf.Max(furthest, deck.position.x);
            }
            else throw new InvalidOperationException("Generated movement playtest ended during the ferry crossing.");
        }
        Require(stayedAboard && furthest >= rail.exactX + rail.spanX - 0.05f && deck.position.x < furthest - 1f,
            "An idle real player rides the seeded deck to its far bank and stays aboard after reversal.");
        Require(stayedOnLedge && max - min > 1.5f,
            $"The patrol moves across its ledge and stays supported at both turnarounds ({min:F2}..{max:F2}).");
        var motor = FindObjectOfType<PlayerMovement>();
        var constraints = player.constraints;
        motor.enabled = false;
        player.constraints = RigidbodyConstraints2D.FreezeAll;
        var stop = CreateTestGround(null, "Rail obstruction", new Vector2(rail.exactX + rail.spanX - 0.25f, rail.exactY),
            new Vector2(0.3f, 0.2f));
        try
        {
            MovePlayer(player, new Vector2(-11, 7.5f));
            float obstructionX = stop.bounds.min.x;
            yield return Until(() => ferry.Velocity.x < 0f && deck.position.x > obstructionX - 1.5f &&
                ferry.GetComponent<Collider2D>().bounds.max.x <= obstructionX + 0.02f, 4f,
                "A live solid on a custom rail turns the deck around before penetration.");
            var enemy = patrol.GetComponent<EnemyPatrol2D>();
            yield return new WaitForSeconds(3f);
            Require(GameObject.FindGameObjectsWithTag("Player").Length == 1 && motor.CompareTag("Player"),
                "The real character is the only Player-tagged object in a generated ferry stage.");
            Require(enemy.CurrentState == EnemyPatrol2D.EnemyState.Chase &&
                patrol.position.x > -12.55f && patrol.position.x <= -12.39f && Mathf.Abs(patrol.velocity.x) < 0.01f,
                $"A chasing patrol stops on the ledge: {enemy.CurrentState}, enemy {patrol.position:F3}, " +
                $"vx {patrol.velocity.x:F3}, target {motor.transform.position:F3}.");
        }
        finally
        {
            Destroy(stop.gameObject);
            player.constraints = constraints;
            motor.enabled = true;
        }
        creator.EnterEditMode();
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
        PressureGauge gauge = FindObjectOfType<PressureGauge>();
        Require(jumpPad && sentry && shielded && gauge,
            "Level 4 contains the authored JumpPad, SentryTurret, ShieldedEnemy, and PressureGauge.");
        gauge.RefreshGauge();
        Require(gauge.CurrentText == session.Pressure.GetGaugeLabel(),
            "PressureGauge displays the exact live Pressure tier and carried ore progress.");
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

    IEnumerator ResizeOpenUI(string label, Canvas root, bool centeredMenu = false)
    {
        var original = new Dictionary<UnityEngine.UI.Button, Rect>();
        var sizes = new[] { new Vector2Int(1280, 720), new Vector2Int(800, 600),
            new Vector2Int(1440, 600), new Vector2Int(600, 800), new Vector2Int(640, 360), new Vector2Int(1280, 720) };
        Vector2 reference = centeredMenu ? root.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution : Vector2.one;
        float firstFit = Mathf.Min(sizes[0].x / reference.x, sizes[0].y / reference.y);
        for (int index = 0; index < sizes.Length; index++)
        {
            var size = sizes[index];
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            float timeout = Time.realtimeSinceStartup + 2f;
            do { yield return null; }
            while ((Screen.width != size.x || Screen.height != size.y) && Time.realtimeSinceStartup < timeout);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            string at = $"{label} {size.x}x{size.y}";
            Require(Screen.width == size.x && Screen.height == size.y, $"{at}: the open window resized.");
            var buttons = root ? root.GetComponentsInChildren<UnityEngine.UI.Button>() : FindObjectsOfType<UnityEngine.UI.Button>();
            var bounds = new Rect[buttons.Length];
            if (index > 0 && buttons.Length != original.Count)
                throw new InvalidOperationException($"{at}: the number of visible controls changed.");
            float scale = Mathf.Min(size.x / reference.x, size.y / reference.y) / firstFit;
            for (int i = 0; i < buttons.Length; i++)
            {
                bounds[i] = ScreenBounds((RectTransform)buttons[i].transform);
                CheckOnscreen(bounds[i], $"{at}/{buttons[i].name}");
                for (int j = 0; j < i; j++)
                {
                    Rect overlap = Rect.MinMaxRect(Mathf.Max(bounds[i].xMin, bounds[j].xMin), Mathf.Max(bounds[i].yMin, bounds[j].yMin),
                        Mathf.Min(bounds[i].xMax, bounds[j].xMax), Mathf.Min(bounds[i].yMax, bounds[j].yMax));
                    if (overlap.width > 1f && overlap.height > 1f)
                        throw new InvalidOperationException($"{at}: {buttons[i].name} overlaps {buttons[j].name}.");
                }
                if (index == 0) original.Add(buttons[i], bounds[i]);
                else if (!original.TryGetValue(buttons[i], out Rect first))
                    throw new InvalidOperationException($"{at}: unexpected control {buttons[i].name}.");
                else if (centeredMenu || index == sizes.Length - 1)
                {
                    Vector2 center = (Vector2)size * 0.5f + (first.center - (Vector2)sizes[0] * 0.5f) * scale;
                    if (Vector2.Distance(bounds[i].center, center) > 1.5f || Vector2.Distance(bounds[i].size, first.size * scale) > 1.5f)
                        throw new InvalidOperationException($"{at}: {buttons[i].name} shifted or stretched relative to the original layout.");
                }
            }
            var labels = root ? root.GetComponentsInChildren<TextMeshProUGUI>() : FindObjectsOfType<TextMeshProUGUI>();
            foreach (var text in labels)
            {
                if (!text.isActiveAndEnabled || !text.canvas || !text.canvas.enabled || string.IsNullOrWhiteSpace(text.text)) continue;
                CheckOnscreen(ScreenBounds(text.rectTransform), $"{at}/{text.name}");
                if (text.isTextOverflowing || text.isTextTruncated)
                    throw new InvalidOperationException($"{at}: clipped label '{text.text}' ({text.name}).");
            }
            if (label == "store" && GameObject.FindGameObjectWithTag("LivesText")?.GetComponentInParent<Canvas>() is Canvas hud && hud.enabled)
                throw new InvalidOperationException("Gameplay HUD leaked into the shop.");
            if (index == 2 || index == 3)
            {
                string path = Path.Combine(outputDirectory, $"resize-{label}-{size.x}x{size.y}.png");
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(path, shot.EncodeToPNG()); }
                finally { Destroy(shot); }
                report.screenshots.Add(path);
            }
            Require(true, $"{at}: active controls and labels fit without clipping or intersecting buttons.");
        }
    }

    static Rect ScreenBounds(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    static void CheckOnscreen(Rect rect, string label)
    {
        if (rect.width <= 0 || rect.height <= 0 || rect.xMin < -1f || rect.yMin < -1f ||
            rect.xMax > Screen.width + 1f || rect.yMax > Screen.height + 1f)
            throw new InvalidOperationException($"{label}: offscreen or empty rect {rect}.");
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
