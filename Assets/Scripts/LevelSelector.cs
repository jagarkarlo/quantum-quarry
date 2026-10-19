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
        if (btn) btn.interactable = (level <= unlocked);
        RefreshMedalBadge();
    }

    public void RefreshMedalBadge()
    {
        LevelPersonalBest record = GameSession.GetPersonalBest($"level-{level}");
        string badgeText = LevelMedals.FormatShortBadge(record);
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

    public void OpenScene()
    {
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
