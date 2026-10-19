using System;
using System.Globalization;

public enum MedalTier
{
    None = 0,
    Bronze = 1,
    Silver = 2,
    Gold = 3
}

[Serializable]
public readonly struct LevelMedalTargets
{
    public readonly string LevelId;
    public readonly int GoldTimeMs;
    public readonly int SilverTimeMs;
    public readonly int BronzeTimeMs;
    public readonly int GoldOre;
    public readonly int SilverOre;
    public readonly int BronzeOre;

    public LevelMedalTargets(string levelId, int goldTimeMs, int silverTimeMs, int bronzeTimeMs,
        int goldOre, int silverOre, int bronzeOre)
    {
        LevelId = levelId ?? "level-1";
        GoldTimeMs = Math.Max(1000, goldTimeMs);
        SilverTimeMs = Math.Max(GoldTimeMs, silverTimeMs);
        BronzeTimeMs = Math.Max(SilverTimeMs, bronzeTimeMs);
        BronzeOre = Math.Max(0, bronzeOre);
        SilverOre = Math.Max(BronzeOre, silverOre);
        GoldOre = Math.Max(SilverOre, goldOre);
    }
}

[Serializable]
public readonly struct LevelResult
{
    public readonly string LevelId;
    public readonly int ElapsedMs;
    public readonly int BankedOre;
    public readonly int LostOre;
    public readonly int HitsTaken;
    public readonly MedalTier TimeMedal;
    public readonly MedalTier OreMedal;
    public readonly MedalTier SurvivalMedal;

    public LevelResult(string levelId, int elapsedMs, int bankedOre, int lostOre, int hitsTaken,
        MedalTier timeMedal, MedalTier oreMedal, MedalTier survivalMedal)
    {
        LevelId = levelId ?? "level-1";
        ElapsedMs = Math.Max(0, elapsedMs);
        BankedOre = Math.Max(0, bankedOre);
        LostOre = Math.Max(0, lostOre);
        HitsTaken = Math.Max(0, hitsTaken);
        TimeMedal = timeMedal;
        OreMedal = oreMedal;
        SurvivalMedal = survivalMedal;
    }

    public MedalTier OverallMedal
    {
        get
        {
            int min = Math.Min((int)TimeMedal, Math.Min((int)OreMedal, (int)SurvivalMedal));
            if (min > 0) return (MedalTier)min;
            int max = Math.Max((int)TimeMedal, Math.Max((int)OreMedal, (int)SurvivalMedal));
            return (MedalTier)max;
        }
    }
}

[Serializable]
public readonly struct LevelPersonalBest
{
    public readonly bool Completed;
    public readonly int BestTimeMs;
    public readonly int BestBankedOre;
    public readonly int FewestHits;
    public readonly MedalTier BestTimeMedal;
    public readonly MedalTier BestOreMedal;
    public readonly MedalTier BestSurvivalMedal;

    public LevelPersonalBest(bool completed, int bestTimeMs, int bestBankedOre, int fewestHits,
        MedalTier bestTimeMedal, MedalTier bestOreMedal, MedalTier bestSurvivalMedal)
    {
        Completed = completed;
        BestTimeMs = completed ? Math.Max(0, bestTimeMs) : int.MaxValue;
        BestBankedOre = completed ? Math.Max(0, bestBankedOre) : 0;
        FewestHits = completed ? Math.Max(0, fewestHits) : int.MaxValue;
        BestTimeMedal = completed ? bestTimeMedal : MedalTier.None;
        BestOreMedal = completed ? bestOreMedal : MedalTier.None;
        BestSurvivalMedal = completed ? bestSurvivalMedal : MedalTier.None;
    }
}

public static class LevelMedals
{
    static readonly LevelMedalTargets[] CampaignTargets =
    {
        new LevelMedalTargets("level-1", 35000, 55000, 85000, 300, 200, 100),
        new LevelMedalTargets("level-2", 45000, 70000, 105000, 400, 250, 100),
        new LevelMedalTargets("level-3", 50000, 80000, 120000, 400, 250, 100),
        new LevelMedalTargets("level-4", 65000, 95000, 140000, 1500, 800, 300),
        new LevelMedalTargets("level-5", 70000, 105000, 150000, 600, 350, 150),
        new LevelMedalTargets("level-6", 80000, 120000, 170000, 800, 450, 200)
    };

    public static string NormalizeLevelId(string sceneOrLevelId)
    {
        if (string.IsNullOrWhiteSpace(sceneOrLevelId)) return null;
        string trimmed = sceneOrLevelId.Trim();
        if (trimmed.StartsWith("Level ", StringComparison.Ordinal) &&
            int.TryParse(trimmed.Substring(6), out int level) && level > 0)
            return $"level-{level}";
        if (trimmed.StartsWith("level-", StringComparison.Ordinal) &&
            int.TryParse(trimmed.Substring(6), out int normalized) && normalized > 0)
            return $"level-{normalized}";
        return null;
    }

    public static LevelMedalTargets GetTargets(string sceneOrLevelId)
    {
        string id = NormalizeLevelId(sceneOrLevelId) ?? "level-1";
        for (int index = 0; index < CampaignTargets.Length; index++)
            if (CampaignTargets[index].LevelId == id)
                return CampaignTargets[index];
        return new LevelMedalTargets(id, 60000, 90000, 130000, 500, 300, 100);
    }

    public static MedalTier EvaluateTimeMedal(int elapsedMs, LevelMedalTargets targets)
    {
        int safeMs = Math.Max(0, elapsedMs);
        if (safeMs <= targets.GoldTimeMs) return MedalTier.Gold;
        if (safeMs <= targets.SilverTimeMs) return MedalTier.Silver;
        if (safeMs <= targets.BronzeTimeMs) return MedalTier.Bronze;
        return MedalTier.None;
    }

    public static MedalTier EvaluateOreMedal(int bankedOre, LevelMedalTargets targets)
    {
        int safeOre = Math.Max(0, bankedOre);
        if (safeOre >= targets.GoldOre) return MedalTier.Gold;
        if (safeOre >= targets.SilverOre) return MedalTier.Silver;
        if (safeOre >= targets.BronzeOre) return MedalTier.Bronze;
        return MedalTier.None;
    }

    public static MedalTier EvaluateSurvivalMedal(int hitsTaken)
    {
        int safeHits = Math.Max(0, hitsTaken);
        if (safeHits == 0) return MedalTier.Gold;
        if (safeHits <= 1) return MedalTier.Silver;
        if (safeHits <= 3) return MedalTier.Bronze;
        return MedalTier.None;
    }

    public static LevelResult Evaluate(string sceneOrLevelId, int elapsedMs, int bankedOre, int lostOre, int hitsTaken)
    {
        LevelMedalTargets targets = GetTargets(sceneOrLevelId);
        return new LevelResult(
            targets.LevelId,
            elapsedMs,
            bankedOre,
            lostOre,
            hitsTaken,
            EvaluateTimeMedal(elapsedMs, targets),
            EvaluateOreMedal(bankedOre, targets),
            EvaluateSurvivalMedal(hitsTaken));
    }

    public static LevelPersonalBest MergePersonalBest(LevelPersonalBest existing, LevelResult latest)
    {
        if (!existing.Completed)
        {
            return new LevelPersonalBest(
                true,
                latest.ElapsedMs,
                latest.BankedOre,
                latest.HitsTaken,
                latest.TimeMedal,
                latest.OreMedal,
                latest.SurvivalMedal);
        }

        return new LevelPersonalBest(
            true,
            Math.Min(existing.BestTimeMs, latest.ElapsedMs),
            Math.Max(existing.BestBankedOre, latest.BankedOre),
            Math.Min(existing.FewestHits, latest.HitsTaken),
            (MedalTier)Math.Max((int)existing.BestTimeMedal, (int)latest.TimeMedal),
            (MedalTier)Math.Max((int)existing.BestOreMedal, (int)latest.OreMedal),
            (MedalTier)Math.Max((int)existing.BestSurvivalMedal, (int)latest.SurvivalMedal));
    }

    public static string FormatDuration(int elapsedMs)
    {
        int safeMs = Math.Max(0, elapsedMs);
        int minutes = safeMs / 60000;
        double seconds = (safeMs % 60000) / 1000.0;
        return minutes > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00.0}s", minutes, seconds)
            : string.Format(CultureInfo.InvariantCulture, "{0:0.0}s", seconds);
    }

    public static string FormatMedal(MedalTier tier)
    {
        switch (tier)
        {
            case MedalTier.Gold: return "GOLD";
            case MedalTier.Silver: return "SILVER";
            case MedalTier.Bronze: return "BRONZE";
            default: return "NONE";
        }
    }

    public static string FormatShortBadge(LevelPersonalBest record)
    {
        if (!record.Completed) return string.Empty;
        return $"T:{ShortCode(record.BestTimeMedal)} O:{ShortCode(record.BestOreMedal)} S:{ShortCode(record.BestSurvivalMedal)}";
    }

    static char ShortCode(MedalTier tier)
    {
        switch (tier)
        {
            case MedalTier.Gold: return 'G';
            case MedalTier.Silver: return 'S';
            case MedalTier.Bronze: return 'B';
            default: return '-';
        }
    }
}
