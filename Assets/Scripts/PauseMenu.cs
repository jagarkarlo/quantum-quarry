using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;
    public GameObject pauseMenuUI;
    TextMeshProUGUI volumeBtnLabel;
    TextMeshProUGUI flashBtnLabel;
    TextMeshProUGUI splitsBtnLabel;

    void Start()
    {
        QuarrySettings.ApplyRuntimeAudio();
        CreatePauseButton();
        EnsureAccessibilityControls();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (GameIsPaused) Resume();
        else Pause();
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        GameIsPaused = false;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        GameIsPaused = true;
    }

    void CreatePauseButton()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (!canvas || transform.Find("PauseButton")) return;

        GameObject buttonObject = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);

        RectTransform buttonTransform = buttonObject.GetComponent<RectTransform>();
        buttonTransform.anchorMin = Vector2.one;
        buttonTransform.anchorMax = Vector2.one;
        buttonTransform.pivot = Vector2.one;
        buttonTransform.anchoredPosition = new Vector2(-18f, -18f);
        buttonTransform.sizeDelta = new Vector2(116f, 46f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.04f, 0.12f, 0.18f, 0.82f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(TogglePause);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelTransform = labelObject.GetComponent<RectTransform>();
        labelTransform.anchorMin = Vector2.zero;
        labelTransform.anchorMax = Vector2.one;
        labelTransform.sizeDelta = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        GameObject scoreObject = GameObject.FindGameObjectWithTag("ScoreText");
        TextMeshProUGUI scoreLabel = scoreObject ? scoreObject.GetComponent<TextMeshProUGUI>() : null;
        label.font = scoreLabel && scoreLabel.font ? scoreLabel.font : TMP_Settings.defaultFontAsset;
        label.text = "PAUSE";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 24f;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    void EnsureAccessibilityControls()
    {
        if (!pauseMenuUI || pauseMenuUI.transform.Find("Button_AudioVol")) return;
        volumeBtnLabel = CreateOptionButton("Button_AudioVol", new Vector2(-180f, -245f), () =>
        {
            QuarrySettings.CycleVolumeStep();
            RefreshOptionLabels();
        });
        flashBtnLabel = CreateOptionButton("Button_ReducedFlash", new Vector2(0f, -245f), () =>
        {
            QuarrySettings.ReducedHitFlash = !QuarrySettings.ReducedHitFlash;
            RefreshOptionLabels();
        });
        splitsBtnLabel = CreateOptionButton("Button_ShowSplits", new Vector2(180f, -245f), () =>
        {
            QuarrySettings.ShowSpeedrunSplits = !QuarrySettings.ShowSpeedrunSplits;
            RefreshOptionLabels();
        });
        RefreshOptionLabels();
    }

    TextMeshProUGUI CreateOptionButton(string name, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        var btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(pauseMenuUI.transform, false);
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(165f, 40f);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.08f, 0.22f, 0.34f, 0.94f);
        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(btnObj.transform, false);
        RectTransform lrect = labelObj.GetComponent<RectTransform>();
        lrect.anchorMin = Vector2.zero;
        lrect.anchorMax = Vector2.one;
        lrect.sizeDelta = Vector2.zero;

        TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI anyLabel = pauseMenuUI.GetComponentInChildren<TextMeshProUGUI>(true);
        if (anyLabel && anyLabel.font) label.font = anyLabel.font;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 9f;
        label.fontSizeMax = 15f;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    void RefreshOptionLabels()
    {
        if (volumeBtnLabel) volumeBtnLabel.text = $"AUDIO: {QuarrySettings.MasterVolumePercent}%";
        if (flashBtnLabel) flashBtnLabel.text = $"FLASH: {(QuarrySettings.ReducedHitFlash ? "SOFT" : "FULL")}";
        if (splitsBtnLabel) splitsBtnLabel.text = $"SPLITS: {(QuarrySettings.ShowSpeedrunSplits ? "ON" : "OFF")}";
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        GameIsPaused = false;
        SceneManager.LoadScene("Level selector"); // ensure exact scene name
    }

    public void LoadStore()
    {
        Time.timeScale = 1f;
        GameIsPaused = false;

        // Save scene + player position to return EXACTLY once
        PlayerPrefs.SetString("ReturnScene", SceneManager.GetActiveScene().name);
        PlayerPrefs.SetInt("ReturnFromStore", 1);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var p = player.transform.position;
            PlayerPrefs.SetFloat("ReturnPosX", p.x);
            PlayerPrefs.SetFloat("ReturnPosY", p.y);
            PlayerPrefs.SetFloat("ReturnPosZ", p.z);
        }
        PlayerPrefs.Save();

        SceneManager.LoadScene("Store");
    }

    public void ResetLevel()
    {
        Time.timeScale = 1f;
        GameIsPaused = false;

        var gs = FindObjectOfType<GameSession>();
        if (gs) gs.ResetCurrentLevelToStart();
        else
        {
            // fallback: just reload scene
            var idx = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(idx);
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
