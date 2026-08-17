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
