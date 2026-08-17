using UnityEngine;

public class PauseMenuSpawner : MonoBehaviour
{
    [SerializeField] GameObject pauseMenuPrefab; // Assign in Inspector

    void Awake()
    {
        if (!FindObjectOfType<PauseMenu>())
        {
            Instantiate(pauseMenuPrefab);
        }
    }
}
