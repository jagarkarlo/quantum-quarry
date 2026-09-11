using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object value) => value != null;
        public static void Destroy(Object value) { }
        public static void DontDestroyOnLoad(Object value) { }
        public static T FindObjectOfType<T>() where T : Object => null;
    }
    public class MonoBehaviour : Object
    {
        public GameObject gameObject = new GameObject();
    }
    public class GameObject : Object
    {
        public static GameObject FindGameObjectWithTag(string tag) => null;
        public T GetComponent<T>() => default;
    }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string value) { } }
    public class SerializeField : Attribute { }
    public static class Mathf
    {
        public static int Max(int first, int second) => Math.Max(first, second);
        public static float Max(float first, float second) => Math.Max(first, second);
        public static int Clamp(int value, int minimum, int maximum) => Math.Clamp(value, minimum, maximum);
        public static float Clamp(float value, float minimum, float maximum) => Math.Clamp(value, minimum, maximum);
        public static float Ceil(float value) => (float)Math.Ceiling(value);
        public static bool Approximately(float first, float second) => Math.Abs(first - second) < 0.00001f;
    }
    public static class PlayerPrefs
    {
        static readonly Dictionary<string, int> values = new Dictionary<string, int>();
        public static int GetInt(string key, int fallback = 0) => values.TryGetValue(key, out int value) ? value : fallback;
        public static void SetInt(string key, int value) => values[key] = value;
        public static bool HasKey(string key) => values.ContainsKey(key);
        public static void DeleteKey(string key) => values.Remove(key);
        public static void DeleteAll() => values.Clear();
        public static void Save() { }
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public int buildIndex; }
    public enum LoadSceneMode { Single, Additive }
    public static class SceneManager
    {
        public static event Action<Scene, LoadSceneMode> sceneLoaded;
        public static Scene GetActiveScene() => new Scene { buildIndex = 1 };
        public static void LoadScene(int index) => sceneLoaded?.Invoke(new Scene { buildIndex = index }, LoadSceneMode.Single);
        public static void LoadScene(string name) => LoadScene(0);
    }
}
namespace TMPro
{
    public class TextMeshProUGUI : UnityEngine.Object { public string text; }
}
public class ScenePersist : UnityEngine.Object { public void ResetScenePersist() { } }
public static class QuarryPressureHUD { public static void EnsureForScene(GameSession session) { } }