#if QUARRY_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        session.AddCoins(500);
        session.AddCoins(1000);
        Require(session.Pressure.Tier == 2 && session.Pressure.PendingCoins == 1750,
            "Real session uses pre-pickup rewards at the tier-2 threshold.");
        MovePlayer(body, vent.transform.position + Vector3.up * 0.5f);
        float safeStart = Time.time;
        int initialHits = session.GetHitsTaken();
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
