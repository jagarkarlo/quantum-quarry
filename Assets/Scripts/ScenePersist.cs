using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePersist : MonoBehaviour
{
    static ScenePersist instance;
    readonly List<GameObject> suspendedChildren = new List<GameObject>();
    string ownerScene;

    void Awake()
    {
        ownerScene = gameObject.scene.name;
        if (instance && instance != this)
        {
            if (instance.ownerScene == ownerScene)
            {
                Destroy(gameObject);
                return;
            }
            Destroy(instance.gameObject);
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == ownerScene)
        {
            foreach (GameObject child in suspendedChildren)
                if (child) child.SetActive(true);
            suspendedChildren.Clear();
            return;
        }
        if (GameSession.IsGameplayScene(scene.name))
        {
            ResetScenePersist();
            return;
        }

        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf) continue;
            suspendedChildren.Add(child.gameObject);
            child.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    public void ResetScenePersist()
    {
        if (instance == this) instance = null;
        Destroy(gameObject);
    }
}
