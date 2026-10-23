using System;

[Serializable]
public readonly struct DailyChallengeDescriptor
{
    public readonly int DateCode;
    public readonly int Seed;
    public readonly int TargetLevelNumber;
    public readonly int TargetBankedOre;
    public readonly bool NoStoreAllowed;
    public readonly string Title;

    public DailyChallengeDescriptor(int dateCode, int seed, int targetLevelNumber,
        int targetBankedOre, bool noStoreAllowed, string title)
    {
        DateCode = dateCode;
        Seed = seed;
        TargetLevelNumber = Math.Max(1, Math.Min(6, targetLevelNumber));
        TargetBankedOre = Math.Max(100, targetBankedOre);
        NoStoreAllowed = noStoreAllowed;
        Title = title ?? "DAILY CHALLENGE";
    }
}

public static class SeededChallengeRules
{
    public const string ActiveChallengeSeedKey = "ActiveChallengeSeed";
    public const string ActiveChallengeNoStoreKey = "ActiveChallengeNoStore";
    public const string ActiveChallengeDateKey = "ActiveChallengeDateCode";

    public static int FormatDateCode(int year, int month, int day)
    {
        int y = Math.Max(2020, Math.Min(2100, year));
        int m = Math.Max(1, Math.Min(12, month));
        int d = Math.Max(1, Math.Min(31, day));
        return y * 10000 + m * 100 + d;
    }

    public static int ComputeDailySeed(int dateCode)
    {
        uint hash = unchecked((uint)dateCode * 2654435761u ^ 0x5A17C3E9u);
        hash ^= hash >> 15;
        hash = unchecked(hash * 2246822519u);
        hash ^= hash >> 13;
        return (int)(hash & 0x7FFFFFFFu);
    }

    public static DailyChallengeDescriptor BuildForDate(int year, int month, int day)
    {
        int code = FormatDateCode(year, month, day);
        int seed = ComputeDailySeed(code);
        int level = 1 + Math.Abs(seed) % 6;
        int targetOre = level == 4 ? 1200 : 300 + level * 100;
        bool noStore = (Math.Abs(seed >> 4) & 1) == 1;
        string ruleTag = noStore ? "NO STORE" : "IRON ORE";
        string title = $"DAILY #{code % 10000:0000} (L{level} {ruleTag})";
        return new DailyChallengeDescriptor(code, seed, level, targetOre, noStore, title);
    }

    public static bool MeetsChallengeGoal(DailyChallengeDescriptor challenge, int bankedOre, int hitsTaken)
    {
        return bankedOre >= challenge.TargetBankedOre && hitsTaken <= 3;
    }
}
