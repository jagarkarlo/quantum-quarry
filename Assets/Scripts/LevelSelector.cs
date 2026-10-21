using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LevelSelector : MonoBehaviour
{
    [Tooltip("Level number this button opens (1-based).")]
    public int level = 1;

    const string UnlockedLevelKey = "UnlockedLevelNumber";
    Button btn;
    TextMeshProUGUI medalBadge;

    void Awake()
    {
        btn = GetComponent<Button>();

        // Safety: if you forgot to set 'level', try to parse it from the GameObject name "Level 3" etc.
        if (level <= 0)
        {
            // Try to read number from name
            var parts = gameObject.name.Split(' ');
            if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1], out int parsed) && parsed > 0)
                level = parsed;
            else
                level = 1; // fallback
        }
    }

    void OnEnable()
    {
        // Ensure the key exists (in case someone opened Level selector scene directly)
        if (!PlayerPrefs.HasKey(UnlockedLevelKey))
        {
            PlayerPrefs.SetInt(UnlockedLevelKey, 1);
            PlayerPrefs.Save();
        }

        int unlocked = PlayerPrefs.GetInt(UnlockedLevelKey, 1);
        if (btn) btn.interactable = (level <= unlocked || level >= 7);
        RefreshMedalBadge();
        EnsureCreatorButton();
        EnsureCustomStageButtons();
    }

    void EnsureCreatorButton()
    {
        if (level != 1) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (!canvas || canvas.transform.Find("Button_LevelCreator")) return;

        var creatorObj = new GameObject("Button_LevelCreator", typeof(RectTransform), typeof(Image), typeof(Button));
        creatorObj.transform.SetParent(canvas.transform, false);
        RectTransform rect = creatorObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 24f);
        rect.sizeDelta = new Vector2(210f, 48f);

        Image img = creatorObj.GetComponent<Image>();
        img.color = new Color(0.12f, 0.36f, 0.48f, 0.94f);
        Button creatorBtn = creatorObj.GetComponent<Button>();
        creatorBtn.targetGraphic = img;
        creatorBtn.onClick.AddListener(CustomLevelCreatorController.OpenCreatorFromMenu);

        var labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(creatorObj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI parentLabel = GetComponentInChildren<TextMeshProUGUI>();
        if (parentLabel && parentLabel.font) label.font = parentLabel.font;
        label.text = "LEVEL CREATOR";
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 20f;
        label.color = new Color(0.92f, 0.98f, 1f);
        label.raycastTarget = false;
    }

    public void RefreshMedalBadge()
    {
        LevelPersonalBest record = GameSession.GetPersonalBest($"level-{level}");
        string badgeText = LevelMedals.FormatShortBadge(record);
        if (level >= 1 && level <= 6 && CustomLevelSceneAdapter.HasCampaignOverride(level))
        {
            badgeText = string.IsNullOrEmpty(badgeText) ? "REMIXED" : $"{badgeText} *";
        }
        if (string.IsNullOrEmpty(badgeText))
        {
            if (medalBadge) medalBadge.gameObject.SetActive(false);
            return;
        }

        if (!medalBadge)
        {
            Transform existing = transform.Find("MedalBadge");
            if (existing) medalBadge = existing.GetComponent<TextMeshProUGUI>();
        }
        if (!medalBadge)
        {
            var badgeObj = new GameObject("MedalBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeObj.transform.SetParent(transform, false);
            RectTransform rect = badgeObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0.34f);
            rect.offsetMin = new Vector2(4f, 2f);
            rect.offsetMax = new Vector2(-4f, 0f);
            medalBadge = badgeObj.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI parentLabel = GetComponentInChildren<TextMeshProUGUI>();
            if (parentLabel && parentLabel != medalBadge && parentLabel.font)
                medalBadge.font = parentLabel.font;
            medalBadge.alignment = TextAlignmentOptions.Center;
            medalBadge.enableAutoSizing = true;
            medalBadge.fontSizeMin = 8f;
            medalBadge.fontSizeMax = 14f;
            medalBadge.color = new Color(1f, 0.86f, 0.32f);
            medalBadge.raycastTarget = false;
        }

        medalBadge.gameObject.SetActive(true);
        medalBadge.text = badgeText;
    }

    void EnsureCustomStageButtons()
    {
        if (level != 1) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (!canvas) return;

        var stages = CustomLevelSceneAdapter.GetSavedCustomStageNumbers();
        for (int i = 0; i < stages.Count; i++)
        {
            int stageNum = stages[i];
            string btnName = $"Button_CustomStage_{stageNum}";
            if (canvas.transform.Find(btnName)) continue;

            var stageObj = new GameObject(btnName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LevelSelector));
            stageObj.transform.SetParent(canvas.transform, false);
            RectTransform rect = stageObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(24f + i * 150f, 24f);
            rect.sizeDelta = new Vector2(140f, 48f);

            Image img = stageObj.GetComponent<Image>();
            img.color = new Color(0.24f, 0.16f, 0.42f, 0.94f);
            Button stageBtn = stageObj.GetComponent<Button>();
            stageBtn.targetGraphic = img;

            var labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(stageObj.transform, false);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI parentLabel = GetComponentInChildren<TextMeshProUGUI>();
            if (parentLabel && parentLabel.font) label.font = parentLabel.font;
            label.text = $"STAGE {stageNum}";
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 18f;
            label.color = new Color(0.98f, 0.92f, 1f);
            label.raycastTarget = false;

            LevelSelector selector = stageObj.GetComponent<LevelSelector>();
            selector.level = stageNum;
            stageBtn.onClick.AddListener(selector.OpenScene);
            selector.RefreshMedalBadge();
        }
    }

    public void OpenScene()
    {
        if (level >= 7)
        {
            string stagePath = CustomLevelSceneAdapter.GetCustomStageFilePath(level);
            if (System.IO.File.Exists(stagePath))
            {
                CustomLevelDocument doc = CustomLevelDocument.FromJson(System.IO.File.ReadAllText(stagePath));
                int hostLvl = Mathf.Clamp(doc.sourceCampaignLevel, 1, 6);
                PlayerPrefs.SetInt(CustomLevelSceneAdapter.ActiveCustomStageKey, level);
                PlayerPrefs.Save();
                SceneManager.LoadScene($"Level {hostLvl}");
                return;
            }
        }

        PlayerPrefs.SetInt(CustomLevelSceneAdapter.ActiveCustomStageKey, 0);
        int unlocked = PlayerPrefs.GetInt(UnlockedLevelKey, 1);
        if (level <= unlocked)
        {
            string sceneName = "Level " + level;
            if (Application.CanStreamedLevelBeLoaded(sceneName))
                SceneManager.LoadScene(sceneName);
            else
                Debug.LogWarning($"Scene not found: {sceneName} (check Build Settings name).");
        }
        else
        {
            Debug.Log("Level locked!");
            // Optional: show a lock popup here
        }
    }
}
