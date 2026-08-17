using UnityEngine;
using UnityEngine.SceneManagement;

public class VictoryMenu : MonoBehaviour
{
    public void GoToStore()
    {
        SceneManager.LoadScene("Store");
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("Start");
    }
}
