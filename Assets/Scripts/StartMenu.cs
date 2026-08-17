using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    }

    public void PlayGame()
    {
        StartCoroutine(WaitAndLoad("Level 1", sceneLoadDelay));
    }

    public void NewGame()
    {
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
