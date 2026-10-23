using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenu : MonoBehaviour
{
    [SerializeField] float sceneLoadDelay = 0.25f;
    public const string UnlockedLevelKey = "UnlockedLevelNumber";
    bool wipeConfirmPending;
    TextMeshProUGUI wipeButtonLabel;

    void Awake()
    {
        if (!PlayerPrefs.HasKey(UnlockedLevelKey))
        {
            PlayerPrefs.SetInt(UnlockedLevelKey, 1);
            PlayerPrefs.Save();
        }
        QuarrySettings.ApplyRuntimeAudio();
        EnsureFrontPageCreatorButton();
    }

    void EnsureFrontPageCreatorButton()
    {
        if (SceneManager.GetActiveScene().name != "Start") return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (!canvas) canvas = FindObjectOfType<Canvas>();
        if (!canvas || canvas.transform.Find("Button_FrontLevelCreator")) return;

        CreateFrontButton(canvas, "Button_FrontLevelCreator", "LEVEL CREATOR",
            new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(220f, 48f),
            new Color(0.08f, 0.38f, 0.52f, 0.96f), OpenLevelCreator);

        System.DateTime now = System.DateTime.UtcNow;
        DailyChallengeDescriptor daily = SeededChallengeRules.BuildForDate(now.Year, now.Month, now.Day);
        CreateFrontButton(canvas, "Button_DailyChallenge", daily.Title,
            new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(260f, 48f),
            new Color(0.42f, 0.24f, 0.12f, 0.96f), () => StartDailyChallenge(daily));

        Button wipeBtn = CreateFrontButton(canvas, "Button_WipeProfile", "RESET PROFILE",
            new Vector2(1f, 0f), new Vector2(-24f, 80f), new Vector2(220f, 40f),
            new Color(0.45f, 0.14f, 0.16f, 0.94f), OnWipeProfileClicked);
        wipeButtonLabel = wipeBtn.GetComponentInChildren<TextMeshProUGUI>();
    }

    Button CreateFrontButton(Canvas canvas, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size,
        Color color, UnityEngine.Events.UnityAction onClick)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(canvas.transform, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = obj.GetComponent<Image>();
        img.color = color;
        Button btn = obj.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(obj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI anyLabel = canvas.GetComponentInChildren<TextMeshProUGUI>();
        if (anyLabel && anyLabel.font) label.font = anyLabel.font;
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 20f;
        label.color = Color.white;
        label.raycastTarget = false;
        return btn;
    }

    public void StartDailyChallenge(DailyChallengeDescriptor challenge)
    {
        int unlocked = Mathf.Clamp(PlayerPrefs.GetInt(UnlockedLevelKey, 1), 1, 6);
        int targetLvl = Mathf.Min(challenge.TargetLevelNumber, unlocked);
        PlayerPrefs.SetInt(CustomLevelSceneAdapter.ActiveCustomStageKey, 0);
        PlayerPrefs.SetInt(SeededChallengeRules.ActiveChallengeSeedKey, challenge.Seed);
        PlayerPrefs.SetInt(SeededChallengeRules.ActiveChallengeNoStoreKey, challenge.NoStoreAllowed ? 1 : 0);
        PlayerPrefs.SetInt(SeededChallengeRules.ActiveChallengeDateKey, challenge.DateCode);
        PlayerPrefs.SetInt(GameSession.PressureSeedKey, challenge.Seed);
        PlayerPrefs.Save();
        SceneManager.LoadScene($"Level {targetLvl}");
    }

    public void OnWipeProfileClicked()
    {
        if (!wipeConfirmPending)
        {
            wipeConfirmPending = true;
            if (wipeButtonLabel) wipeButtonLabel.text = "CONFIRM WIPE?";
            return;
        }
        wipeConfirmPending = false;
        QuarryProfileManager.WipeFullProfileConfirmed();
        var gs = FindObjectOfType<GameSession>();
        if (gs) gs.ResetSession();
        if (wipeButtonLabel) wipeButtonLabel.text = "PROFILE WIPED";
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
