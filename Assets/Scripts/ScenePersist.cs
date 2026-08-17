using UnityEngine;

public class ScenePersist : MonoBehaviour
{
    void Awake()
    {
        var all = FindObjectsOfType<ScenePersist>();
        if (all.Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    public void ResetScenePersist()
    {
        Destroy(gameObject);
    }
}
