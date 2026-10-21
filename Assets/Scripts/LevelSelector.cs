using System.Collections.Generic;
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
    static int currentPage = 0;

    int slotIndex = 1;
    Button btn;
    TextMeshProUGUI mainLabel;
    TextMeshProUGUI medalBadge;
    static TextMeshProUGUI pageIndicatorLabel;
    static Button prevPageBtn;
    static Button nextPageBtn;

    public static int CurrentPage => currentPage;

    void Awake()
    {
        btn = GetComponent<Button>();

        if (level <= 0)
        {
            var parts = gameObject.name.Split(' ');
            if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1], out int parsed) && parsed > 0)
                level = parsed;
            else
                level = 1;
        }
        slotIndex = Mathf.Clamp(level, 1, 6);
        Transform levelChild = transform.Find("Level");
        if (levelChild) mainLabel = levelChild.GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        if (!PlayerPrefs.HasKey(UnlockedLevelKey))
        {
            PlayerPrefs.SetInt(UnlockedLevelKey, 1);
            PlayerPrefs.Save();
        }

        if (slotIndex == 1)
        {
            int maxPage = GetTotalPages() - 1;
            if (currentPage > maxPage) currentPage = Mathf.Max(0, maxPage);
            EnsureNavigationControls();
        }

        ApplyPageToSlot();
    }

    public static int GetTotalPages()
    {
        List<int> customStages = CustomLevelSceneAdapter.GetSavedCustomStageNumbers();
        int customPages = (customStages.Count + 5) / 6;
        return 1 + customPages;
    }

    public static void SetPage(int page)
    {
        int total = GetTotalPages();
        currentPage = Mathf.Clamp(page, 0, Mathf.Max(0, total - 1));
        foreach (LevelSelector selector in FindObjectsOfType<LevelSelector>(true))
        {
            selector.ApplyPageToSlot();
        }
        RefreshPaginationControls();
    }

    public static void NextPage() => SetPage(currentPage + 1);
    public static void PrevPage() => SetPage(currentPage - 1);

    void ApplyPageToSlot()
    {
        int unlocked = PlayerPrefs.GetInt(UnlockedLevelKey, 1);
        Image bg = GetComponent<Image>();

        if (currentPage == 0)
        {
            level = slotIndex;
            bool isUnlocked = level <= unlocked;
            if (btn) btn.interactable = isUnlocked;
            if (bg)
            {
                bg.color = isUnlocked
                    ? new Color(0f, 0.682f, 1f, 1f)
                    : new Color(0.18f, 0.22f, 0.28f, 0.88f);
            }
            if (mainLabel)
            {
                mainLabel.text = isUnlocked ? level.ToString() : $"{level}\n<size=40%>LOCKED</size>";
                mainLabel.color = isUnlocked ? new Color(0.12f, 0.14f, 0.18f, 1f) : new Color(0.62f, 0.66f, 0.72f, 0.9f);
            }
            RefreshMedalBadge(isUnlocked);
        }
        else
        {
            List<int> customStages = CustomLevelSceneAdapter.GetSavedCustomStageNumbers();
            int listIdx = (currentPage - 1) * 6 + (slotIndex - 1);
            if (listIdx >= 0 && listIdx < customStages.Count)
            {
                level = customStages[listIdx];
                if (btn) btn.interactable = true;
                if (bg) bg.color = new Color(0.38f, 0.22f, 0.68f, 1f);
                if (mainLabel)
                {
                    mainLabel.text = $"S{level}";
                    mainLabel.color = Color.white;
                }
                RefreshMedalBadge(true);
            }
            else
            {
                level = -1;
                if (btn) btn.interactable = false;
                if (bg) bg.color = new Color(0.12f, 0.14f, 0.18f, 0.45f);
                if (mainLabel)
                {
                    mainLabel.text = "-";
                    mainLabel.color = new Color(0.5f, 0.55f, 0.6f, 0.5f);
                }
                if (medalBadge) medalBadge.gameObject.SetActive(false);
            }
        }
    }

    void EnsureNavigationControls()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (!canvas) return;

        if (!canvas.transform.Find("Button_BackToStart"))
        {
            CreateNavButton(canvas.transform, "Button_BackToStart", "BACK TO MENU",
                new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(210f, 48f),
                new Color(0.46f, 0.18f, 0.18f, 0.95f), () => SceneManager.LoadScene("Start"));
        }

        if (!canvas.transform.Find("Button_LevelCreator"))
        {
            CreateNavButton(canvas.transform, "Button_LevelCreator", "LEVEL CREATOR",
                new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(210f, 48f),
                new Color(0.12f, 0.36f, 0.48f, 0.95f), CustomLevelCreatorController.OpenCreatorFromMenu);
        }

        if (!canvas.transform.Find("Button_PrevPage"))
        {
            prevPageBtn = CreateNavButton(canvas.transform, "Button_PrevPage", "< PREV",
                new Vector2(0.5f, 0f), new Vector2(-130f, 24f), new Vector2(105f, 44f),
                new Color(0.18f, 0.28f, 0.42f, 0.95f), PrevPage);
        }

        if (!canvas.transform.Find("Button_NextPage"))
        {
            nextPageBtn = CreateNavButton(canvas.transform, "Button_NextPage", "NEXT >",
                new Vector2(0.5f, 0f), new Vector2(130f, 24f), new Vector2(105f, 44f),
                new Color(0.18f, 0.28f, 0.42f, 0.95f), NextPage);
        }

        Transform pageInfo = canvas.transform.Find("PageIndicator");
        if (!pageInfo)
        {
            var infoObj = new GameObject("PageIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
            infoObj.transform.SetParent(canvas.transform, false);
            RectTransform rect = infoObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 26f);
            rect.sizeDelta = new Vector2(145f, 40f);
            pageIndicatorLabel = infoObj.GetComponent<TextMeshProUGUI>();
            if (mainLabel && mainLabel.font) pageIndicatorLabel.font = mainLabel.font;
            pageIndicatorLabel.alignment = TextAlignmentOptions.Center;
            pageIndicatorLabel.enableAutoSizing = true;
            pageIndicatorLabel.fontSizeMin = 10f;
            pageIndicatorLabel.fontSizeMax = 18f;
            pageIndicatorLabel.color = Color.white;
            pageIndicatorLabel.raycastTarget = false;
        }

        RefreshPaginationControls();
    }

    static void RefreshPaginationControls()
    {
        int total = GetTotalPages();
        if (pageIndicatorLabel)
            pageIndicatorLabel.text = $"PAGE {currentPage + 1}/{total}";
        if (prevPageBtn)
            prevPageBtn.interactable = currentPage > 0;
        if (nextPageBtn)
            nextPageBtn.interactable = currentPage + 1 < total;
    }

    Button CreateNavButton(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size,
        Color color, UnityEngine.Events.UnityAction onClick)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = obj.GetComponent<Image>();
        img.color = color;
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(onClick);

        var labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(obj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
        if (mainLabel && mainLabel.font) label.font = mainLabel.font;
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 18f;
        label.color = Color.white;
        label.raycastTarget = false;
        return button;
    }

    public void RefreshMedalBadge(bool isUnlocked = true)
    {
        if (!isUnlocked || level <= 0)
        {
            if (medalBadge) medalBadge.gameObject.SetActive(false);
            return;
        }

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
            if (mainLabel && mainLabel.font) medalBadge.font = mainLabel.font;
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

    public void OpenScene()
    {
        if (level < 1) return;
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
        }
    }
}
