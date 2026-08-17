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
    Button backButton;
    TextMeshProUGUI statusText;
    Coroutine statusRoutine;

    GameSession GS => FindObjectOfType<GameSession>();

    void OnEnable()
    {
        BindStoreUI();
        BuildResponsiveLayout();
        RefreshUI();
    }

    void BindStoreUI()
    {
        lifeButton = FindButton("Button_ExtraLife");
        speedButton = FindButton("Button_SpeedBoost");
        invisibilityButton = FindButton("Button_Invisibility");
        doubleJumpButton = FindButton("Button_DoubleJump");
        backButton = FindButton("Button_Back");
    }

    Button FindButton(string objectName)
    {
        GameObject buttonObject = GameObject.Find(objectName);
        return buttonObject ? buttonObject.GetComponent<Button>() : null;
    }

    void BuildResponsiveLayout()
    {
        if (!coinsText || !lifeButton) return;

        CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
        if (scaler) scaler.matchWidthOrHeight = 0.5f;

        RectTransform panel = lifeButton.transform.parent as RectTransform;
        if (!panel) return;

        Canvas canvas = panel.GetComponentInParent<Canvas>();
        Rect canvasRect = canvas ? ((RectTransform)canvas.transform).rect : new Rect(0f, 0f, 800f, 600f);
        float panelWidth = Mathf.Min(720f, Mathf.Max(320f, canvasRect.width - 40f));
        float panelHeight = Mathf.Min(560f, Mathf.Max(520f, canvasRect.height - 40f));

        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(panelWidth, panelHeight);

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage) panelImage.color = new Color(0.13f, 0.16f, 0.20f, 0.97f);

        coinsText.transform.SetParent(panel, false);
        float contentWidth = Mathf.Max(280f, panelWidth - 100f);
        ConfigureLabel(coinsText, "StoreBalance", -96f, contentWidth, 38f, 18f, 28f);
        coinsText.fontStyle = FontStyles.Bold;
        coinsText.color = new Color(1f, 0.86f, 0.30f);

        TextMeshProUGUI titleText = GetOrCreateLabel(panel, "StoreTitle");
        ConfigureLabel(titleText, "StoreTitle", -44f, contentWidth, 52f, 24f, 38f);
        titleText.text = "QUANTUM SUPPLY";
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;

        statusText = GetOrCreateLabel(panel, "StoreStatus");
        ConfigureLabel(statusText, "StoreStatus", -130f, contentWidth, 34f, 14f, 22f);
        statusText.text = string.Empty;

        ConfigureButton(lifeButton, -188f, new Color(0.98f, 0.69f, 0.25f), contentWidth);
        ConfigureButton(speedButton, -260f, new Color(0.18f, 0.72f, 0.92f), contentWidth);
        ConfigureButton(invisibilityButton, -332f, new Color(0.35f, 0.82f, 0.63f), contentWidth);
        ConfigureButton(doubleJumpButton, -404f, new Color(0.78f, 0.48f, 0.86f), contentWidth);
        ConfigureButton(backButton, -492f, new Color(0.25f, 0.29f, 0.35f), 260f, 52f);
    }

    TextMeshProUGUI GetOrCreateLabel(RectTransform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing) return existing.GetComponent<TextMeshProUGUI>();

        TextMeshProUGUI label = Instantiate(coinsText, parent);
        label.name = objectName;
        return label;
    }

    void ConfigureLabel(TextMeshProUGUI label, string objectName, float y, float width,
        float height, float minSize, float maxSize)
    {
        label.name = objectName;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.enableAutoSizing = true;
        label.fontSizeMin = minSize;
        label.fontSizeMax = maxSize;
        label.alignment = TextAlignmentOptions.Center;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    void ConfigureButton(Button button, float y, Color color, float width = 600f,
        float height = 62f)
    {
        if (!button) return;

        RectTransform rect = button.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(width, height);

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.disabledColor = new Color(0.28f, 0.30f, 0.33f, 0.65f);
        button.colors = colors;
    }

    void RefreshUI()
    {
        int coins = GS ? GS.GetCoins() : PlayerPrefs.GetInt(GameSession.CoinsKey, 0);
        if (coinsText) coinsText.text = "Coins: " + coins;

        RefreshButton(lifeButton, $"<b>EXTRA LIFE</b>  +1\n{priceLife} COINS", coins,
            priceLife, true);
        RefreshPowerupButton(speedButton, "SPEED BOOST", speedSeconds, priceSpeed,
            GameSession.SpeedBoostQueuedKey, coins);
        RefreshPowerupButton(invisibilityButton, "INVISIBILITY", invisSeconds, priceInvis,
            GameSession.InvisibilityQueuedKey, coins);
        RefreshPowerupButton(doubleJumpButton, "DOUBLE JUMP", doubleJumpSeconds,
            priceDoubleJump, GameSession.DoubleJumpQueuedSecs, coins);

        if (backButton)
        {
            TextMeshProUGUI backText = backButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (backText) backText.text = "BACK TO GAME";
        }
    }

    void RefreshPowerupButton(Button button, string productName, float duration, int price,
        string queueKey, int coins)
    {
        int seconds = Mathf.Max(1, Mathf.RoundToInt(duration));
        int queued = GameSession.GetQueuedPowerupSeconds(queueKey, seconds);
        bool hasCapacity = StoreEconomy.HasQueueCapacity(queued, seconds);
        string inventory = hasCapacity ? $"INVENTORY {queued}s" : "INVENTORY FULL";
        string label = $"<b>{productName}</b>  +{seconds}s\n{price} COINS   |   {inventory}";
        RefreshButton(button, label, coins, price, hasCapacity);
    }

    void RefreshButton(Button button, string label, int coins, int price, bool available)
    {
        if (!button) return;
        button.interactable = available && StoreEconomy.CanAfford(coins, price);

        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (!buttonText) return;

        buttonText.enableAutoSizing = true;
        buttonText.fontSizeMin = 14f;
        buttonText.fontSizeMax = 22f;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.lineSpacing = -8f;
        buttonText.color = new Color(0.08f, 0.10f, 0.13f);
        buttonText.text = label;
    }

    bool Spend(int cost)
    {
        GameSession session = GS;
        if (!session || !session.SpendCoins(cost))
        {
            ShowStatus("NOT ENOUGH COINS", false);
            return false;
        }

        return true;
    }

    void ShowStatus(string message, bool success = true)
    {
        RefreshUI();
        if (!statusText) return;

        if (statusRoutine != null) StopCoroutine(statusRoutine);
        statusText.color = success ? new Color(0.45f, 1f, 0.66f) : new Color(1f, 0.45f, 0.38f);
        statusRoutine = StartCoroutine(ShowStatusTemporarily(message));
    }

    IEnumerator ShowStatusTemporarily(string message)
    {
        statusText.text = message;
        yield return new WaitForSecondsRealtime(1.5f);
        statusText.text = string.Empty;
        statusRoutine = null;
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
