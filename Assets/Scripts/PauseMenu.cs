using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;
    public GameObject pauseMenuUI;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameIsPaused) Resume();
            else Pause();
        }
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
