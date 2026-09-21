using System;
using System.Reflection;
using UnityEngine;

static class SessionTests
{
    static int assertions;

    static void Equal<T>(T expected, T actual, string name)
    {
        assertions++;
        if (!Equals(expected, actual)) throw new Exception($"{name}: expected {expected}, got {actual}");
    }

    static GameSession Restore(GameSession previous = null)
    {
        if (previous != null)
            typeof(GameSession).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(previous, null);
        typeof(GameSession).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        var session = new GameSession();
        typeof(GameSession).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
        return session;
    }

    public static void Run()
    {
        Equal(true, GameSession.IsGameplayScene("Level 4"), "numbered level is gameplay");
        Equal(false, GameSession.IsGameplayScene("Level selector"), "level selector is not gameplay");
        Equal(false, GameSession.IsGameplayScene("Store"), "store is not gameplay");
        Equal(false, GameSession.IsGameplayScene("Level 0"), "zero level is not gameplay");
        Equal(false, GameSession.IsGameplayScene("Level -1"), "negative level is not gameplay");
        Equal(false, GameSession.IsGameplayScene(""), "empty scene name is not gameplay");
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetInt(GameSession.CoinsKey, 700);
        PlayerPrefs.SetInt("UnlockedLevelNumber", 4);
        var session = Restore();
        Equal(700, session.GetCoins(), "legacy coins remain banked");
        Equal(0, session.Pressure.PendingCoins, "legacy has no pending ore");
        int changes = 0;
        session.PressureChanged += () => changes++;
        Equal(500, session.AddCoins(500), "session pickup");
        Equal(700, session.GetCoins(), "pickup cannot spend unbanked ore");
        Equal(500, PlayerPrefs.GetInt(GameSession.PendingCoinsKey), "pending persisted");
        Equal(1, changes, "pickup notification");
        session = Restore(session);
        Equal(1, session.Pressure.Tier, "reload preserves pressure");
        Equal(500, session.BankOre(), "bank deposit");
        Equal(1200, session.GetCoins(), "bank credits balance");
        Equal(0, PlayerPrefs.GetInt(GameSession.CarriedOreKey), "bank persists reset");
        Equal(0, session.BankOre(), "no duplicate deposits");
        Equal(true, session.SpendCoins(200), "banked ore spendable");
        session.AddCoins(500);
        session.ProcessPlayerDeath();
        Equal(1000, session.GetCoins(), "death preserves banked coins");
        Equal(0, session.Pressure.PendingCoins, "death loses unbanked coins");
        Equal(2, session.GetLives(), "death consumes life");
        Equal(4, PlayerPrefs.GetInt("UnlockedLevelNumber"), "death preserves progression");
        session.AddCoins(500);
        session.ResetCurrentLevelToStart();
        Equal(0, session.Pressure.Tier, "manual reset clears risk");
        session.AddCoins(100);
        session.SaveFinalScoreForSummary();
        Equal(1100, PlayerPrefs.GetInt(GameSession.FinalCoinsKey), "victory banks before summary");
        session.AddCoins(200);
        session.SetLives(1);
        session.ProcessPlayerDeath();
        Equal(1100, PlayerPrefs.GetInt(GameSession.LastScoreKey), "game over excludes lost ore");
        Equal(false, PlayerPrefs.HasKey(GameSession.PendingCoinsKey), "game over clears pending save");
        Equal(false, PlayerPrefs.HasKey(GameSession.PressureSeedKey), "game over clears seed");
        Equal(4, PlayerPrefs.GetInt("UnlockedLevelNumber"), "game over retains unlocks");
        session.ResetSession();
        Equal(0, session.GetCoins(), "new run balance");
        Equal(0, session.Pressure.Tier, "new run pressure");
        Equal(3, session.GetLives(), "new run lives");
        PlayerPrefs.SetInt(GameSession.CoinsKey, int.MaxValue - 1);
        session = Restore(session);
        session.AddCoins(500);
        Equal(1, session.BankOre(), "bank clips to available capacity");
        Equal(int.MaxValue, session.GetCoins(), "balance cannot overflow");
        GameSession.ClearPersistentRunState();
        Equal(false, PlayerPrefs.HasKey(GameSession.CarriedOreKey), "hard clear removes ore");
        Console.WriteLine($"GameSession integration (Unity test doubles): {assertions} assertions passed.");
    }
}