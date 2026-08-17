using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class StoreManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI coinsText;

    // Prices
    [SerializeField] int priceLife = 50;
    [SerializeField] int priceSpeed = 100;
    [SerializeField] int priceInvis = 150;
    [SerializeField] int priceDoubleJump = 120;

    // Durations
    [SerializeField] float speedSeconds = 10f;
    [SerializeField] float invisSeconds = 10f;
    [SerializeField] int doubleJumpSeconds = 10;

    Button lifeButton;
    Button speedButton;
    Button invisibilityButton;
    Button doubleJumpButton;
    Coroutine statusRoutine;

    GameSession GS => FindObjectOfType<GameSession>();

    void OnEnable()
    {
        BindStoreUI();
        RefreshUI();
    }

    void BindStoreUI()
    {
        lifeButton = FindButton("Button_ExtraLife");
        speedButton = FindButton("Button_SpeedBoost");
        invisibilityButton = FindButton("Button_Invisibility");
        doubleJumpButton = FindButton("Button_DoubleJump");
    }

    Button FindButton(string objectName)
    {
        GameObject buttonObject = GameObject.Find(objectName);
        return buttonObject ? buttonObject.GetComponent<Button>() : null;
    }

    void RefreshUI()
    {
        int coins = GS ? GS.GetCoins() : PlayerPrefs.GetInt(GameSession.CoinsKey, 0);
        if (coinsText) coinsText.text = "Coins: " + coins;

        RefreshButton(lifeButton, $"Extra Life - {priceLife}", coins, priceLife);
        RefreshButton(speedButton, PowerupLabel("Speed", speedSeconds, priceSpeed,
            GameSession.SpeedBoostQueuedKey), coins, priceSpeed);
        RefreshButton(invisibilityButton, PowerupLabel("Invisibility", invisSeconds, priceInvis,
            GameSession.InvisibilityQueuedKey), coins, priceInvis);
        RefreshButton(doubleJumpButton, PowerupLabel("Double Jump", doubleJumpSeconds,
            priceDoubleJump, GameSession.DoubleJumpQueuedSecs), coins, priceDoubleJump);
    }

    string PowerupLabel(string name, float duration, int price, string queueKey)
    {
        int seconds = Mathf.Max(1, Mathf.RoundToInt(duration));
        int queued = GameSession.GetQueuedPowerupSeconds(queueKey, seconds);
        string inventory = queued > 0 ? $" [{queued}s]" : string.Empty;
        return $"{name} +{seconds}s - {price}{inventory}";
    }

    void RefreshButton(Button button, string label, int coins, int price)
    {
        if (!button) return;
        button.interactable = StoreEconomy.CanAfford(coins, price);

        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (!buttonText) return;

        buttonText.enableAutoSizing = true;
        buttonText.fontSizeMin = 14f;
        buttonText.fontSizeMax = 24f;
        buttonText.text = label;
    }

    bool Spend(int cost)
    {
        GameSession session = GS;
        if (!session || !session.SpendCoins(cost))
        {
            ShowStatus("Not enough coins");
            return false;
        }

        return true;
    }

    void ShowStatus(string message)
    {
        RefreshUI();
        if (!coinsText) return;

        if (statusRoutine != null) StopCoroutine(statusRoutine);
        statusRoutine = StartCoroutine(ShowStatusTemporarily(message));
    }

    IEnumerator ShowStatusTemporarily(string message)
    {
        coinsText.text = message;
        yield return new WaitForSecondsRealtime(1.25f);
        statusRoutine = null;
        RefreshUI();
    }

    public void BuyLife()
    {
        if (!Spend(priceLife)) return;
        GS.SetLives(GS.GetLives() + 1);
        ShowStatus("Extra life purchased");
    }

    public void BuySpeed()
    {
        if (!Spend(priceSpeed)) return;
        GameSession.QueuePowerupSeconds(GameSession.SpeedBoostQueuedKey,
            Mathf.RoundToInt(speedSeconds), Mathf.RoundToInt(speedSeconds));
        ShowStatus("Speed boost added");
    }

    public void BuyInvisibility()
    {
        if (!Spend(priceInvis)) return;
        GameSession.QueuePowerupSeconds(GameSession.InvisibilityQueuedKey,
            Mathf.RoundToInt(invisSeconds), Mathf.RoundToInt(invisSeconds));
        ShowStatus("Invisibility added");
    }

    public void BuyDoubleJump()
    {
        if (!Spend(priceDoubleJump)) return;
        GameSession.QueuePowerupSeconds(GameSession.DoubleJumpQueuedSecs,
            doubleJumpSeconds, doubleJumpSeconds);
        ShowStatus("Double jump added");
    }

    public void ReturnToGame()
    {
        Time.timeScale = 1f; // safety
        // NEW: ensure stale pause flag never blocks shooting
        PauseMenu.GameIsPaused = false;

        string sceneName = PlayerPrefs.GetString("ReturnScene", "Level 1");
        SceneManager.LoadScene(sceneName);
    }
}
