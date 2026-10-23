using UnityEngine;

public static class QuarryProfileManager
{
    public static string GetGhostPrefsKey(string levelId)
    {
        string normalized = LevelMedals.NormalizeLevelId(levelId) ?? levelId ?? "level-1";
        return $"GhostReplay_{normalized}";
    }

    public static GhostReplayData LoadBestGhost(string levelId)
    {
        string raw = PlayerPrefs.GetString(GetGhostPrefsKey(levelId), string.Empty);
        return GhostReplayData.FromCompactString(raw);
    }

    public static void SaveGhostIfFaster(GhostReplayData candidate)
    {
        if (candidate == null || candidate.frames == null || candidate.frames.Count < 2) return;
        string key = GetGhostPrefsKey(candidate.levelId);
        GhostReplayData existing = LoadBestGhost(candidate.levelId);
        if (existing == null || existing.totalElapsedMs <= 0 || candidate.totalElapsedMs <= existing.totalElapsedMs)
        {
            PlayerPrefs.SetString(key, candidate.ToCompactString());
            PlayerPrefs.Save();
        }
    }

    public static void WipeFullProfileConfirmed()
    {
        int vol = QuarrySettings.MasterVolumePercent;
        bool reducedFlash = QuarrySettings.ReducedHitFlash;
        bool splits = QuarrySettings.ShowSpeedrunSplits;

        GameSession.ClearPersistentRunState();
        PlayerPrefs.SetInt(StartMenu.UnlockedLevelKey, 1);
        PlayerPrefs.DeleteKey("BestScore");
        PlayerPrefs.DeleteKey(SeededChallengeRules.ActiveChallengeSeedKey);
        PlayerPrefs.DeleteKey(SeededChallengeRules.ActiveChallengeNoStoreKey);
        PlayerPrefs.DeleteKey(SeededChallengeRules.ActiveChallengeDateKey);

        for (int lvl = 1; lvl <= 20; lvl++)
        {
            string id = $"level-{lvl}";
            PlayerPrefs.DeleteKey($"LevelPB_{id}_Completed");
            PlayerPrefs.DeleteKey($"LevelPB_{id}_TimeMs");
            PlayerPrefs.DeleteKey($"LevelPB_{id}_Ore");
            PlayerPrefs.DeleteKey($"LevelPB_{id}_Hits");
            PlayerPrefs.DeleteKey($"LevelPB_{id}_TimeMedal");
            PlayerPrefs.DeleteKey($"LevelPB_{id}_OreMedal");
            PlayerPrefs.DeleteKey($"LevelPB_{id}_SurvivalMedal");
            PlayerPrefs.DeleteKey(GetGhostPrefsKey(id));
        }

        QuarrySettings.MasterVolumePercent = vol;
        QuarrySettings.ReducedHitFlash = reducedFlash;
        QuarrySettings.ShowSpeedrunSplits = splits;
        PlayerPrefs.Save();
    }
}
