using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameSession : MonoBehaviour
{
    [Header("Player State")]
    [SerializeField] int playerLives = 3;
    [SerializeField] int coins = 0;

    [Header("UI (auto-assigned by tag in each scene)")]
    [SerializeField] TextMeshProUGUI livesText;
    [SerializeField] TextMeshProUGUI scoreText; // shows coins

    // Keys
    public const string CoinsKey = "Coins";
    public const string LivesKey = "Lives";
    public const string LastScoreKey = "LastScore";

    // Powerup queue keys (store can set, player consumes)
    public const string SpeedBoostQueuedKey   = "SpeedBoostQueued";
    public const string InvisibilityQueuedKey = "InvisibilityQueued";
    public const string DoubleJumpQueuedSecs  = "DoubleJumpQueuedSeconds";

    static GameSession instance;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Pull persisted values (for Store / continue same session)
        coins = PlayerPrefs.GetInt(CoinsKey, coins);
        playerLives = PlayerPrefs.GetInt(LivesKey, playerLives);
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
        if (livesText) livesText.text = playerLives.ToString();
        if (scoreText) scoreText.text = coins.ToString();
    }

    // ---------- Coins & Lives ----------
    public int GetCoins() => coins;
    public int GetLives() => playerLives;

    public void AddCoins(int amount)
    {
        coins += Mathf.Max(0, amount);
        PlayerPrefs.SetInt(CoinsKey, coins);
        PlayerPrefs.Save();
        if (scoreText) scoreText.text = coins.ToString();
    }

    public bool SpendCoins(int cost)
    {
        if (!StoreEconomy.CanAfford(coins, cost)) return false;
        coins -= cost;
        PlayerPrefs.SetInt(CoinsKey, coins);
        PlayerPrefs.Save();
        if (scoreText) scoreText.text = coins.ToString();
        return true;
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
        if (livesText) livesText.text = playerLives.ToString();
    }

    // ---------- Death / Reload ----------
    public void ProcessPlayerDeath()
    {
        if (playerLives > 1)
        {
            TakeLifeAndReload();
        }
        else
        {
            // Save summary for GameOver screen
            PlayerPrefs.SetInt(LastScoreKey, coins);

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
        PlayerPrefs.Save();

        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
        if (livesText) livesText.text = playerLives.ToString();
    }

    // ---------- Victory summary ----------
    public void SaveFinalScoreForSummary()
    {
        PlayerPrefs.SetInt("FinalCoins", coins);
        PlayerPrefs.SetInt("FinalLives", playerLives);
        PlayerPrefs.Save();
    }

    // ---------- Hard reset current level (for Pause Reset) ----------
    public void ResetCurrentLevelToStart()
    {
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

        PlayerPrefs.SetInt(CoinsKey, coins);
        PlayerPrefs.SetInt(LivesKey, playerLives);

        // clear queued powerups
        PlayerPrefs.DeleteKey(SpeedBoostQueuedKey);
        PlayerPrefs.DeleteKey(InvisibilityQueuedKey);
        PlayerPrefs.DeleteKey(DoubleJumpQueuedSecs);

        PlayerPrefs.Save();
        RefreshUI();
    }

    public static void ClearPersistentRunState()
    {
        PlayerPrefs.DeleteKey(CoinsKey);
        PlayerPrefs.DeleteKey(LivesKey);
        PlayerPrefs.DeleteKey(SpeedBoostQueuedKey);
        PlayerPrefs.DeleteKey(InvisibilityQueuedKey);
        PlayerPrefs.DeleteKey(DoubleJumpQueuedSecs);
        PlayerPrefs.Save();
    }
}
