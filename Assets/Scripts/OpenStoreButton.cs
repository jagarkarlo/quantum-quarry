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
            PlayerPrefs.SetInt("ReturnFromStore", 1);
            PlayerPrefs.SetFloat("ReturnPosX", pos.x);
            PlayerPrefs.SetFloat("ReturnPosY", pos.y);
            PlayerPrefs.SetFloat("ReturnPosZ", pos.z);
            PlayerPrefs.Save();

            // Leave physics in a clean state
            player.CancelAllPowerupsImmediately();
        }

        Time.timeScale = 1f; // safety
        SceneManager.LoadScene(storeSceneName);
    }
}
