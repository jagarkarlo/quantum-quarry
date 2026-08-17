using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerRespawn : MonoBehaviour
{
    void Start()
    {
        // NEW: make 100% sure the game isn't considered paused on return
        PauseMenu.GameIsPaused = false;

        if (PlayerPrefs.GetInt("ReturnFromStore", 0) != 1) return;

        string returnScene = PlayerPrefs.GetString("ReturnScene", "");
        if (string.IsNullOrEmpty(returnScene)) return;

        if (SceneManager.GetActiveScene().name == returnScene)
        {
            float x = PlayerPrefs.GetFloat("ReturnPosX", transform.position.x);
            float y = PlayerPrefs.GetFloat("ReturnPosY", transform.position.y);
            float z = PlayerPrefs.GetFloat("ReturnPosZ", transform.position.z);
            transform.position = new Vector3(x, y, z);
        }

        PlayerPrefs.DeleteKey("ReturnFromStore");
        PlayerPrefs.DeleteKey("ReturnScene");
        PlayerPrefs.DeleteKey("ReturnPosX");
        PlayerPrefs.DeleteKey("ReturnPosY");
        PlayerPrefs.DeleteKey("ReturnPosZ");
        PlayerPrefs.Save();
    }
}
