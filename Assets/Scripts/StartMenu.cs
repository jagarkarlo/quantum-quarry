using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenu : MonoBehaviour
{
    [SerializeField] float sceneLoadDelay = 0.25f;
    public const string UnlockedLevelKey = "UnlockedLevelNumber";

    void Awake()
    {
        if (!PlayerPrefs.HasKey(UnlockedLevelKey))
        {
            PlayerPrefs.SetInt(UnlockedLevelKey, 1);
            PlayerPrefs.Save();
        }
        EnsureFrontPageCreatorButton();
    }

    void EnsureFrontPageCreatorButton()
    {
        if (SceneManager.GetActiveScene().name != "Start") return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (!canvas) canvas = FindObjectOfType<Canvas>();
        if (!canvas || canvas.transform.Find("Button_FrontLevelCreator")) return;

        var creatorObj = new GameObject("Button_FrontLevelCreator", typeof(RectTransform), typeof(Image), typeof(Button));
        creatorObj.transform.SetParent(canvas.transform, false);
        RectTransform rect = creatorObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 24f);
        rect.sizeDelta = new Vector2(230f, 52f);

        Image img = creatorObj.GetComponent<Image>();
        img.color = new Color(0.08f, 0.38f, 0.52f, 0.96f);
        Button btn = creatorObj.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OpenLevelCreator);

        var labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(creatorObj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI anyLabel = canvas.GetComponentInChildren<TextMeshProUGUI>();
        if (anyLabel && anyLabel.font) label.font = anyLabel.font;
        label.text = "LEVEL CREATOR";
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 22f;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    public void OpenLevelCreator()
    {
        int unlocked = Mathf.Clamp(PlayerPrefs.GetInt(UnlockedLevelKey, 1), 1, 6);
        CustomLevelCreatorController.OpenCreatorForCampaignLevel(unlocked);
    }

    public void PlayGame()
    {
        PlayerPrefs.SetInt(CustomLevelSceneAdapter.ActiveCustomStageKey, 0);
        PlayerPrefs.Save();
        StartCoroutine(WaitAndLoad("Level 1", sceneLoadDelay));
    }

    public void NewGame()
    {
        PlayerPrefs.SetInt(CustomLevelSceneAdapter.ActiveCustomStageKey, 0);
        PlayerPrefs.SetInt(UnlockedLevelKey, 1);
        PlayerPrefs.Save();

        var gs = FindObjectOfType<GameSession>();
        if (gs) gs.ResetSession();  

        StartCoroutine(WaitAndLoad("Level 1", sceneLoadDelay));
    }

    public void ResetLevelProgressOnly()
    {
        PlayerPrefs.SetInt(UnlockedLevelKey, 1);
        PlayerPrefs.Save();
    }

    public void LoadMainMenu()
    {
        StartCoroutine(WaitAndLoad("Start", sceneLoadDelay));
    }

    public void LoadLevelSelector()
    {
        StartCoroutine(WaitAndLoad("Level selector", sceneLoadDelay));
    }

    public void LoadGameOver()
    {
        StartCoroutine(WaitAndLoad("GameOver", sceneLoadDelay));
    }

    public void QuitGame()
    {
        Debug.Log("Exiting game…");
        Application.Quit();
    }

    IEnumerator WaitAndLoad(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }
}
