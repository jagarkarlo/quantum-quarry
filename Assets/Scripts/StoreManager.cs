using UnityEngine;
using UnityEngine.SceneManagement;
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

    GameSession GS => FindObjectOfType<GameSession>();

    void OnEnable() => RefreshUI();

    void RefreshUI()
    {
        if (!coinsText) return;
        int coins = 0;
        if (GS) coins = GS.GetCoins();
        else coins = PlayerPrefs.GetInt(GameSession.CoinsKey, 0);
        coinsText.text = "Coins: " + coins;
    }

    bool Spend(int cost)
    {
        if (!GS) return false;
        bool ok = GS.SpendCoins(cost);
        if (ok) RefreshUI();
        return ok;
    }

    public void BuyLife()
    {
        if (!Spend(priceLife)) return;
        GS.SetLives(GS.GetLives() + 1);
    }

    public void BuySpeed()
    {
        if (!Spend(priceSpeed)) return;

        var player = FindObjectOfType<PlayerMovement>();
        if (player) player.ActivateSpeedBoost(speedSeconds);
        else
        {
            PlayerPrefs.SetInt(GameSession.SpeedBoostQueuedKey, 1);
            PlayerPrefs.Save();
        }
    }

    public void BuyInvisibility()
    {
        if (!Spend(priceInvis)) return;

        var player = FindObjectOfType<PlayerMovement>();
        if (player) player.ActivateInvisibility(invisSeconds);
        else
        {
            PlayerPrefs.SetInt(GameSession.InvisibilityQueuedKey, 1);
            PlayerPrefs.Save();
        }
    }

    public void BuyDoubleJump()
    {
        if (!Spend(priceDoubleJump)) return;

        var player = FindObjectOfType<PlayerMovement>();
        if (player) player.ActivateDoubleJump(doubleJumpSeconds);
        else
        {
            PlayerPrefs.SetInt(GameSession.DoubleJumpQueuedSecs, Mathf.Max(1, doubleJumpSeconds));
            PlayerPrefs.Save();
        }
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
