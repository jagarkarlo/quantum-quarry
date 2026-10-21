using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelExit : MonoBehaviour
{
    [SerializeField] float levelLoadDelay = 1.0f;
    const string UnlockedLevelKey = "UnlockedLevelNumber";
    bool isExiting;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isExiting) return;
        if (other.CompareTag("Player"))
        {
            isExiting = true;
            StartCoroutine(LoadNextLevel());
        }
    }

    IEnumerator LoadNextLevel()
    {
        string currentName = SceneManager.GetActiveScene().name; // "Level N"
        var session = FindObjectOfType<GameSession>();
        if (session && session.IsPlaytestMode)
        {
            session.NotifyPlaytestCompleted();
            ShowResultsBanner(session.LastCompletedLevelResult);
            yield return new WaitForSecondsRealtime(levelLoadDelay);
            GameObject banner = GameObject.Find("LevelResultsBanner");
            if (banner) Destroy(banner);
            isExiting = false;
            yield break;
        }
        if (session)
        {
            LevelResult result = session.CompleteCurrentLevel(currentName);
            ShowResultsBanner(result);
        }

        yield return new WaitForSecondsRealtime(levelLoadDelay);

        int currentNumber = ParseLevelNumber(currentName);
        int nextNumber = currentNumber + 1;

        int unlocked = PlayerPrefs.GetInt(UnlockedLevelKey, 1);
        if (nextNumber > unlocked)
        {
            PlayerPrefs.SetInt(UnlockedLevelKey, nextNumber);
            PlayerPrefs.Save();
        }

        var sp = FindObjectOfType<ScenePersist>();
        if (sp) sp.ResetScenePersist();

        string nextSceneName = "Level " + nextNumber;
        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            var gs = FindObjectOfType<GameSession>();
            if (gs) gs.SaveFinalScoreForSummary();

            // NEW: clear next-run state & kill session so HUD cannot linger
            GameSession.ClearPersistentRunState();
            if (gs) Destroy(gs.gameObject);

            if (Application.CanStreamedLevelBeLoaded("Victory")) SceneManager.LoadScene("Victory");
            else SceneManager.LoadScene("Start");
        }
    }

    public static void ShowResultsBannerForTest(LevelResult result) => ShowResultsBanner(result);

    static void ShowResultsBanner(LevelResult result)
    {
        var canvasObject = new GameObject("LevelResultsBanner", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(540f, 150f);
        Image bg = panel.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.08f, 0.12f, 0.94f);
        bg.raycastTarget = false;

        var labelObject = new GameObject("ResultsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(panel.transform, false);
        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(14f, 10f);
        text.rectTransform.offsetMax = new Vector2(-14f, -10f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 20f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 20f;
        text.color = new Color(0.95f, 0.98f, 1f);
        text.raycastTarget = false;
        text.text =
            $"LEVEL COMPLETE  |  OVERALL {LevelMedals.FormatMedal(result.OverallMedal)}\n" +
            $"Time {LevelMedals.FormatDuration(result.ElapsedMs)} ({LevelMedals.FormatMedal(result.TimeMedal)})   " +
            $"Banked {result.BankedOre} ({LevelMedals.FormatMedal(result.OreMedal)})\n" +
            $"Hits {result.HitsTaken} ({LevelMedals.FormatMedal(result.SurvivalMedal)})   " +
            $"Lost Ore {result.LostOre}";
    }

    int ParseLevelNumber(string sceneName)
    {
        var parts = sceneName.Split(' ');
        if (parts.Length >= 2 && int.TryParse(parts[1], out int n)) return n;
        return 1;
    }
}
