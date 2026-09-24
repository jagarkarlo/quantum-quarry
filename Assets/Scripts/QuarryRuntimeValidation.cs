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
        int expectedBalance = session.GetCoins() + session.Pressure.PendingCoins;
        int remainingCoins = FindObjectsOfType<Collecting>().Length;
        pause.LoadStore();
        yield return null;
        yield return new WaitForSecondsRealtime(0.3f);
        Require(!PauseMenu.GameIsPaused && Time.timeScale == 1f, "Entering the Store clears the pause state.");
        Require(session.GetCoins() == expectedBalance && session.Pressure.CarriedOre == 0 &&
            session.Pressure.PendingCoins == 0, "Store entry banks carried ore exactly once.");
        Require(FindObjectsOfType<Collecting>().Length == 0 && FindObjectsOfType<EnemyPatrol2D>().Length == 0,
            "Persisted coins and enemies are suspended, not visible or simulated in the Store.");
        yield return Capture("store", 800, 600);
        yield return Capture("store", 1280, 720);
        StoreManager store = FindObjectOfType<StoreManager>();
        Require(store, "Store has a manager.");
        store.ReturnToGame();
        yield return null;
        yield return new WaitForSecondsRealtime(0.3f);
        Require(SceneManager.GetActiveScene().name == "Level 4", "Store returns to the originating level.");
        Require(session.GetCoins() == expectedBalance, "Store return does not duplicate the deposit.");
        Require(FindObjectsOfType<Collecting>().Length == remainingCoins,
            "Store return restores remaining coins without respawning collected ore.");
        pause = FindObjectOfType<PauseMenu>();
        Require(pause, "Store return restores the gameplay pause menu.");
        pause.TogglePause();
        Require(PauseMenu.GameIsPaused && Time.timeScale == 0f, "Pause works on the first click after Store return.");
        pause.LoadMenu();
        yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        Require(!PauseMenu.GameIsPaused && Time.timeScale == 1f, "Returning to level selection clears the pause state.");
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
        Require(resetCoins.Length == 15, "Manual reset restores all 15 authored Level 4 coins.");
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
