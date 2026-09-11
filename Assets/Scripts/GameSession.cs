using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameSession : MonoBehaviour
{
    [Header("Player State")]
    [SerializeField] int playerLives = 3;
    [SerializeField] int coins = 0;
    [SerializeField] int maxStability = 3;

    [Header("UI (auto-assigned by tag in each scene)")]
    [SerializeField] TextMeshProUGUI livesText;
    [SerializeField] TextMeshProUGUI scoreText; // shows coins

    // Keys
    public const string CoinsKey = "Coins";
    public const string CarriedOreKey = "PressureCarriedOre";
    public const string PendingCoinsKey = "PressurePendingCoins";
    public const string PressureSeedKey = "PressureSeed";
    public const string LivesKey = "Lives";
    public const string LastScoreKey = "LastScore";
    public const string FinalCoinsKey = "FinalCoins";
    public const string FinalLivesKey = "FinalLives";
    public const string StabilityKey = "QuantumStability";
    public const string StabilityUnitsKey = "QuantumStabilityUnits";
    public const string ArmorTierKey = "ArmorTier";
    public const string HitsTakenKey = "RunHitsTaken";
    public const string StabilityLostUnitsKey = "RunStabilityLostUnits";
    public const string LastHitsTakenKey = "LastHitsTaken";
    public const string LastStabilityLostUnitsKey = "LastStabilityLostUnits";
    public const string FinalHitsTakenKey = "FinalHitsTaken";
    public const string FinalStabilityLostUnitsKey = "FinalStabilityLostUnits";

    // Powerup queue keys (store can set, player consumes)
    public const string SpeedBoostQueuedKey   = "SpeedBoostQueued";
    public const string InvisibilityQueuedKey = "InvisibilityQueued";
    public const string DoubleJumpQueuedSecs  = "DoubleJumpQueuedSeconds";

    static GameSession instance;
    QuantumStability stability;
    QuarryPressure pressure;
    public QuarryPressure Pressure => pressure;
    public event System.Action PressureChanged;
    DamageLog damageLog;
    int armorTier;
    float breathRemaining = -1f;
    float breathMaximum;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Pull persisted values (for Store / continue same session)
        coins = Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, coins));
        pressure = new QuarryPressure(
            PlayerPrefs.GetInt(PressureSeedKey, QuarryPressure.DefaultSeed),
            PlayerPrefs.GetInt(CarriedOreKey, 0),
            PlayerPrefs.GetInt(PendingCoinsKey, 0));
        playerLives = PlayerPrefs.GetInt(LivesKey, playerLives);
        maxStability = Mathf.Max(1, maxStability);
        int persistedUnits = PlayerPrefs.HasKey(StabilityUnitsKey)
            ? PlayerPrefs.GetInt(StabilityUnitsKey)
            : PlayerPrefs.GetInt(StabilityKey, maxStability) * QuantumStability.UnitsPerPoint;
        stability = QuantumStability.FromUnits(
            maxStability * QuantumStability.UnitsPerPoint, persistedUnits);
        armorTier = Mathf.Clamp(PlayerPrefs.GetInt(ArmorTierKey, 0), 0, StoreEconomy.MaxArmorTier);
        damageLog = new DamageLog(
            PlayerPrefs.GetInt(HitsTakenKey, 0),
            PlayerPrefs.GetInt(StabilityLostUnitsKey, 0));
    }

    void Start()
    {
        RebindUI();
        RefreshUI();
    }

    void OnDestroy()
    {
        if (instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindUI();
        RefreshUI();
    }

    void RebindUI()
    {
        QuarryPressureHUD.EnsureForScene(this);
        if (!livesText)
        {
            var go = GameObject.FindGameObjectWithTag("LivesText");
            if (go) livesText = go.GetComponent<TextMeshProUGUI>();
        }
        if (!scoreText)
        {
            var go = GameObject.FindGameObjectWithTag("ScoreText");
            if (go) scoreText = go.GetComponent<TextMeshProUGUI>();
        }
    }

    void RefreshUI()
    {
        RefreshPlayerStateUI();
        RefreshCoinsUI();
    }

    void RefreshPlayerStateUI()
    {
        if (!livesText) return;

        string bonus = stability.IsCritical ? "  <color=#55E8FF>Coins x2</color>" : string.Empty;
        string breath = breathRemaining >= 0f
            ? $"  <color=#70DFFF>Breath {breathRemaining:0.0}s</color>"
            : string.Empty;
        livesText.text = $"Lives {playerLives}  Stability {stability.Current:0.#}/{stability.Max:0.#}{bonus}{breath}";
    }

    // ---------- Coins & Lives ----------
    public int GetCoins() => coins;
    public int GetLives() => playerLives;
    public float GetStability() => stability.Current;
    public float GetMaxStability() => stability.Max;
    public bool IsCriticalStability() => stability.IsCritical;
    public int GetArmorTier() => armorTier;
    public int GetHitsTaken() => damageLog.HitsTaken;
    public float GetStabilityLost() => damageLog.StabilityPointsLost;

    public int AddCoins(int amount)
    {
        int awardedCoins = pressure.Collect(amount, stability.IsCritical);
        SavePressure();
        RefreshCoinsUI();
        PressureChanged?.Invoke();
        return awardedCoins;
    }

    public int BankOre()
    {
        int deposited = (int)System.Math.Min(int.MaxValue - (long)coins, pressure.Bank());
        coins += deposited;
        PlayerPrefs.SetInt(CoinsKey, coins);
        SavePressure();
        RefreshCoinsUI();
        PressureChanged?.Invoke();
        return deposited;
    }

    void SavePressure()
    {
        PlayerPrefs.SetInt(CarriedOreKey, pressure.CarriedOre);
        PlayerPrefs.SetInt(PendingCoinsKey, pressure.PendingCoins);
        PlayerPrefs.SetInt(PressureSeedKey, pressure.Seed);
        PlayerPrefs.Save();
    }

    void DiscardOre()
    {
        pressure.Discard();
        SavePressure();
        RefreshCoinsUI();
        PressureChanged?.Invoke();
    }

    public bool TakeStabilityDamage(int amount)
    {
        return ApplyStabilityDamage(amount * QuantumStability.UnitsPerPoint);
    }

    public bool TakeStabilityDamageUnits(int units)
    {
        return ApplyStabilityDamage(units);
    }

    bool ApplyStabilityDamage(int units)
    {
        int reducedUnits = StoreEconomy.ApplyArmorReductionUnits(units, armorTier);
        if (!stability.TakeDamageUnits(reducedUnits)) return stability.IsDepleted;

        damageLog.RecordHit(reducedUnits);
        SaveDamageLog();
        SaveStability();
        RefreshPlayerStateUI();
        return stability.IsDepleted;
    }

    void SaveDamageLog()
    {
        PlayerPrefs.SetInt(HitsTakenKey, damageLog.HitsTaken);
        PlayerPrefs.SetInt(StabilityLostUnitsKey, damageLog.StabilityUnitsLost);
        PlayerPrefs.Save();
    }

    public bool PurchaseArmorUpgrade()
    {
        int nextTier = armorTier + 1;
        if (!StoreEconomy.IsValidArmorTier(nextTier)) return false;
        if (!SpendCoins(StoreEconomy.GetArmorUpgradeCost(nextTier))) return false;

        armorTier = nextTier;
        PlayerPrefs.SetInt(ArmorTierKey, armorTier);
        PlayerPrefs.Save();
        return true;
    }

    public void SetBreathStatus(float remaining, float maximum)
    {
        float safeMaximum = Mathf.Max(0.5f, maximum);
        float safeRemaining = Mathf.Ceil(Mathf.Clamp(remaining, 0f, safeMaximum) * 10f) / 10f;
        if (Mathf.Approximately(breathRemaining, safeRemaining) &&
            Mathf.Approximately(breathMaximum, safeMaximum)) return;

        breathRemaining = safeRemaining;
        breathMaximum = safeMaximum;
        RefreshPlayerStateUI();
    }

    public void ClearBreathStatus()
    {
        if (breathRemaining < 0f) return;
        breathRemaining = -1f;
        breathMaximum = 0f;
        RefreshPlayerStateUI();
    }

    public bool HealStability(int amount)
    {
        if (!stability.Heal(amount)) return false;

        SaveStability();
        RefreshPlayerStateUI();
        return true;
    }

    void RestoreStability()
    {
        stability.Restore();
        SaveStability();
        RefreshPlayerStateUI();
    }

    void SaveStability()
    {
        PlayerPrefs.SetInt(StabilityUnitsKey, stability.CurrentUnits);
        PlayerPrefs.DeleteKey(StabilityKey);
        PlayerPrefs.Save();
    }

    public bool SpendCoins(int cost)
    {
        if (!StoreEconomy.CanAfford(coins, cost)) return false;
        coins -= cost;
        PlayerPrefs.SetInt(CoinsKey, coins);
        PlayerPrefs.Save();
        RefreshCoinsUI();
        return true;
    }

    void RefreshCoinsUI()
    {
        if (scoreText) scoreText.text = $"Banked {coins}";
    }

    public static int GetQueuedPowerupSeconds(string key, int legacySeconds)
    {
        return StoreEconomy.NormalizeQueuedSeconds(PlayerPrefs.GetInt(key, 0), legacySeconds);
    }

    public static int QueuePowerupSeconds(string key, int purchasedSeconds, int legacySeconds)
    {
        int queuedSeconds = StoreEconomy.AddQueuedSeconds(
            PlayerPrefs.GetInt(key, 0), purchasedSeconds, legacySeconds);
        PlayerPrefs.SetInt(key, queuedSeconds);
        PlayerPrefs.Save();
        return queuedSeconds;
    }

    public static int ConsumeQueuedPowerupSeconds(string key, int legacySeconds)
    {
        int queuedSeconds = GetQueuedPowerupSeconds(key, legacySeconds);
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        return queuedSeconds;
    }

    public void SetLives(int newLives)
    {
        playerLives = Mathf.Max(0, newLives);
        PlayerPrefs.SetInt(LivesKey, playerLives);
        PlayerPrefs.Save();
        RefreshPlayerStateUI();
    }

    // ---------- Death / Reload ----------
    public void ProcessPlayerDeath()
    {
        DiscardOre();
        if (playerLives > 1)
        {
            TakeLifeAndReload();
        }
        else
        {
            // Save summary for GameOver screen
            PlayerPrefs.SetInt(LastScoreKey, coins);
            PlayerPrefs.SetInt(LastHitsTakenKey, damageLog.HitsTaken);
            PlayerPrefs.SetInt(LastStabilityLostUnitsKey, damageLog.StabilityUnitsLost);

            // NEW: clear carry-over so next run starts fresh
            ClearPersistentRunState();

            var sp = FindObjectOfType<ScenePersist>();
            if (sp) sp.ResetScenePersist();

            SceneManager.LoadScene("GameOver");
            Destroy(gameObject);
        }
    }

    void TakeLifeAndReload()
    {
        playerLives--;
        PlayerPrefs.SetInt(LivesKey, playerLives);
        RestoreStability();
        PlayerPrefs.Save();

        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
        RefreshPlayerStateUI();
    }

    // ---------- Victory summary ----------
    public void SaveFinalScoreForSummary()
    {
        BankOre();
        PlayerPrefs.SetInt(FinalCoinsKey, coins);
        PlayerPrefs.SetInt(FinalLivesKey, playerLives);
        PlayerPrefs.SetInt(FinalHitsTakenKey, damageLog.HitsTaken);
        PlayerPrefs.SetInt(FinalStabilityLostUnitsKey, damageLog.StabilityUnitsLost);
        PlayerPrefs.Save();
    }

    // ---------- Hard reset current level (for Pause Reset) ----------
    public void ResetCurrentLevelToStart()
    {
        DiscardOre();
        // Reset any persistent scene objects
        var sp = FindObjectOfType<ScenePersist>();
        if (sp) sp.ResetScenePersist();

        // Don’t restore store-return position
        PlayerPrefs.DeleteKey("ReturnFromStore");
        PlayerPrefs.DeleteKey("ReturnScene");
        PlayerPrefs.DeleteKey("ReturnPosX");
        PlayerPrefs.DeleteKey("ReturnPosY");
        PlayerPrefs.DeleteKey("ReturnPosZ");
        PlayerPrefs.Save();

        // Reload current scene
        int idx = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(idx);
    }

    // ---------- New game / clear persisted run ----------
    public void ResetSession()
    {
        coins = 0;
        playerLives = 3;
        stability = new QuantumStability(maxStability, maxStability);
        armorTier = 0;
        damageLog = new DamageLog(0, 0);
        pressure = new QuarryPressure();
        SavePressure();
        PressureChanged?.Invoke();

        PlayerPrefs.SetInt(CoinsKey, coins);
        PlayerPrefs.SetInt(LivesKey, playerLives);
        PlayerPrefs.SetInt(StabilityUnitsKey, stability.CurrentUnits);
        PlayerPrefs.DeleteKey(StabilityKey);
        PlayerPrefs.DeleteKey(ArmorTierKey);
        PlayerPrefs.DeleteKey(HitsTakenKey);
        PlayerPrefs.DeleteKey(StabilityLostUnitsKey);

        // clear queued powerups
        PlayerPrefs.DeleteKey(SpeedBoostQueuedKey);
        PlayerPrefs.DeleteKey(InvisibilityQueuedKey);
        PlayerPrefs.DeleteKey(DoubleJumpQueuedSecs);

        PlayerPrefs.Save();
        RefreshUI();
    }

    public static void ClearPersistentRunState()
    {
        PlayerPrefs.DeleteKey(CarriedOreKey);
        PlayerPrefs.DeleteKey(PendingCoinsKey);
        PlayerPrefs.DeleteKey(PressureSeedKey);
        PlayerPrefs.DeleteKey(CoinsKey);
        PlayerPrefs.DeleteKey(LivesKey);
        PlayerPrefs.DeleteKey(StabilityKey);
        PlayerPrefs.DeleteKey(StabilityUnitsKey);
        PlayerPrefs.DeleteKey(ArmorTierKey);
        PlayerPrefs.DeleteKey(HitsTakenKey);
        PlayerPrefs.DeleteKey(StabilityLostUnitsKey);
        PlayerPrefs.DeleteKey(SpeedBoostQueuedKey);
        PlayerPrefs.DeleteKey(InvisibilityQueuedKey);
        PlayerPrefs.DeleteKey(DoubleJumpQueuedSecs);
        PlayerPrefs.Save();
    }
}
