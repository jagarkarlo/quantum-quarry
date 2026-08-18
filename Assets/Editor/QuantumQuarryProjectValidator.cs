using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class QuantumQuarryProjectValidator
{
    const string StoreScenePath = "Assets/Levels/Store.unity";
    const string Level4ScenePath = "Assets/Levels/Level 4.unity";
    const string Level5ScenePath = "Assets/Levels/Level 5.unity";
    const string Level6ScenePath = "Assets/Levels/Level 6.unity";

    [MenuItem("Tools/QuantumQuarry/Validate Project")]
    public static void ValidateFromMenu()
    {
        List<string> errors = CollectErrors();
        if (errors.Count == 0)
        {
            Debug.Log("QuantumQuarry validation passed.");
            return;
        }

        foreach (string error in errors) Debug.LogError(error);
        throw new InvalidOperationException($"QuantumQuarry validation failed with {errors.Count} error(s).");
    }

    public static void ValidateBatch()
    {
        ValidateFromMenu();
    }

    static List<string> CollectErrors()
    {
        var errors = new List<string>();
        ValidateEconomy(errors);
        ValidateStability(errors);
        ValidateBuildScenes(errors);
        ValidatePrefabs(errors);
        ValidateStoreScene(errors);
        ValidateCoinTier(Level4ScenePath, 150, errors);
        ValidateCoinTier(Level5ScenePath, 200, errors);
        ValidateCoinTier(Level6ScenePath, 150, errors);
        ValidateCoinTier(Level6ScenePath, 200, errors);
        return errors;
    }

    static void ValidateEconomy(List<string> errors)
    {
        if (StoreEconomy.CanAfford(99, 100)) errors.Add("Store allows an unaffordable purchase.");
        if (!StoreEconomy.CanAfford(100, 100)) errors.Add("Store rejects an exact-balance purchase.");
        if (!StoreEconomy.HasQueueCapacity(110, 10))
            errors.Add("Power-up inventory reports a non-full queue as full.");
        if (StoreEconomy.HasQueueCapacity(111, 10))
            errors.Add("Power-up inventory accepts a purchase without room for its full duration.");
        if (StoreEconomy.NormalizeQueuedSeconds(1, 10) != 10)
            errors.Add("Legacy power-up queue migration is invalid.");
        if (StoreEconomy.AddQueuedSeconds(115, 10, 10) != StoreEconomy.MaxQueuedSeconds)
            errors.Add("Power-up inventory does not enforce its duration cap.");
    }

    static void ValidateStability(List<string> errors)
    {
        var stability = new QuantumStability(3, 3);
        if (!stability.TakeDamage(1) || stability.Current != 2)
            errors.Add("Quantum Stability does not apply contact damage.");
        if (!stability.TakeDamage(1) || !stability.IsCritical)
            errors.Add("Quantum Stability does not enter critical state at one point.");
        if (!stability.Heal(10) || stability.Current != stability.Max)
            errors.Add("Quantum Stability healing does not clamp to its maximum.");
        if (!stability.TakeDamage(10) || !stability.IsDepleted)
            errors.Add("Quantum Stability cannot be depleted by lethal damage.");
        if (stability.Heal(1))
            errors.Add("Depleted Quantum Stability can be healed before a life is processed.");

        stability.Restore();
        if (stability.Current != stability.Max)
            errors.Add("Quantum Stability does not restore after losing a life.");
    }

    static void ValidateBuildScenes(List<string> errors)
    {
        int enabledSceneCount = 0;
        var uniquePaths = new HashSet<string>();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            enabledSceneCount++;
            if (!uniquePaths.Add(scene.path)) errors.Add($"Duplicate build scene: {scene.path}");
            if (!File.Exists(scene.path)) errors.Add($"Missing build scene: {scene.path}");
        }

        if (enabledSceneCount != 11)
            errors.Add($"Expected 11 enabled build scenes, found {enabledSceneCount}.");
    }

    static void ValidatePrefabs(List<string> errors)
    {
        ValidatePrefabComponent<PlayerMovement>("Assets/Prefabs/Player.prefab", errors);
        ValidatePrefabComponent<PlayerStability>("Assets/Prefabs/Player.prefab", errors);
        ValidatePrefabComponent<EnemyPatrol2D>("Assets/Prefabs/Enemy.prefab", errors);
        ValidatePrefabComponent<EnemyPatrol2D>("Assets/Prefabs/Red Enemy.prefab", errors);
        ValidatePrefabComponent<Collecting>("Assets/Prefabs/Coin.prefab", errors);
        ValidatePrefabComponent<FollowingPoint>("Assets/Prefabs/Platform.prefab", errors);
        ValidatePrefabComponent<StickyPlatform>("Assets/Prefabs/Platform.prefab", errors);
    }

    static void ValidatePrefabComponent<T>(string path, List<string> errors) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab) errors.Add($"Missing prefab: {path}");
        else if (!prefab.GetComponentInChildren<T>(true))
            errors.Add($"{path} is missing {typeof(T).Name}.");
    }

    static void ValidateStoreScene(List<string> errors)
    {
        Scene storeScene = SceneManager.GetSceneByPath(StoreScenePath);
        bool openedForValidation = !storeScene.isLoaded;

        try
        {
            if (openedForValidation)
                storeScene = EditorSceneManager.OpenScene(StoreScenePath, OpenSceneMode.Additive);

            StoreManager storeManager = FindInScene<StoreManager>(storeScene);
            if (!storeManager)
            {
                errors.Add("Store scene is missing StoreManager.");
                return;
            }

            ValidateButton(storeScene, "Button_ExtraLife", "BuyLife", storeManager, errors);
            ValidateButton(storeScene, "Button_SpeedBoost", "BuySpeed", storeManager, errors);
            ValidateButton(storeScene, "Button_Invisibility", "BuyInvisibility", storeManager, errors);
            ValidateButton(storeScene, "Button_DoubleJump", "BuyDoubleJump", storeManager, errors);
            ValidateButton(storeScene, "Button_Back", "ReturnToGame", storeManager, errors);
        }
        finally
        {
            if (openedForValidation && storeScene.isLoaded)
                EditorSceneManager.CloseScene(storeScene, true);
        }
    }

    static void ValidateCoinTier(string scenePath, int expectedValue, List<string> errors)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForValidation = !scene.isLoaded;

        try
        {
            if (openedForValidation)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collecting coin in root.GetComponentsInChildren<Collecting>(true))
                {
                    var serializedCoin = new SerializedObject(coin);
                    if (serializedCoin.FindProperty("pointsForCoinPickup").intValue == expectedValue)
                        return;
                }
            }

            errors.Add($"{scenePath} is missing a {expectedValue}-value coin.");
        }
        finally
        {
            if (openedForValidation && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component) return component;
        }

        return null;
    }

    static void ValidateButton(Scene scene, string objectName, string methodName, StoreManager target,
        List<string> errors)
    {
        Button button = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button candidate in root.GetComponentsInChildren<Button>(true))
            {
                if (candidate.name != objectName) continue;
                button = candidate;
                break;
            }

            if (button) break;
        }
        if (!button)
        {
            errors.Add($"Store scene is missing button {objectName}.");
            return;
        }

        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
        {
            if (button.onClick.GetPersistentTarget(index) == target &&
                button.onClick.GetPersistentMethodName(index) == methodName) return;
        }

        errors.Add($"{objectName} is not wired to StoreManager.{methodName}.");
    }
}