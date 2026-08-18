using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;
    public GameObject pauseMenuUI;

    void Awake()
    {
        CreatePauseButton();
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
        buttonTransform.anchoredPosition = new Vector2(-24f, -24f);
        buttonTransform.sizeDelta = new Vector2(96f, 48f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0.44f, 0.72f, 0.9f);

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
        label.font = TMP_Settings.defaultFontAsset;
        label.text = "||";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 28f;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Level selector"); // ensure exact scene name
    }

    public void LoadStore()
    {
        Time.timeScale = 1f;

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
