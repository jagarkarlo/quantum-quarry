using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenStoreButton : MonoBehaviour
{
    [SerializeField] string storeSceneName = "Store"; // set to your store scene name

    public void OpenStore()
    {
        var player = FindObjectOfType<PlayerMovement>();
        if (player)
        {
            // Save return scene + position
            var sceneName = SceneManager.GetActiveScene().name;
            var pos = player.transform.position;

            PlayerPrefs.SetString("ReturnScene", sceneName);
            PlayerPrefs.SetFloat("ReturnX", pos.x);
            PlayerPrefs.SetFloat("ReturnY", pos.y);
            PlayerPrefs.SetInt("HasReturnPos", 1);
            PlayerPrefs.Save();

            // Leave physics in a clean state
            player.CancelAllPowerupsImmediately();
        }

        Time.timeScale = 1f; // safety
        SceneManager.LoadScene(storeSceneName);
    }
}
