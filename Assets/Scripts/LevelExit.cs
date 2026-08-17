using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelExit : MonoBehaviour
{
    [SerializeField] float levelLoadDelay = 1.0f;
    const string UnlockedLevelKey = "UnlockedLevelNumber";

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) StartCoroutine(LoadNextLevel());
    }

    IEnumerator LoadNextLevel()
    {
        yield return new WaitForSecondsRealtime(levelLoadDelay);

        string currentName = SceneManager.GetActiveScene().name; // "Level N"
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

    int ParseLevelNumber(string sceneName)
    {
        var parts = sceneName.Split(' ');
        if (parts.Length >= 2 && int.TryParse(parts[1], out int n)) return n;
        return 1;
    }
}
