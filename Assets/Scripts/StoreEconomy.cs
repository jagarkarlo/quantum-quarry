using System;

public static class StoreEconomy
{
    public const int MaxQueuedSeconds = 120;

    public static bool CanAfford(int balance, int cost)
    {
        return cost >= 0 && balance >= cost;
    }

    public static int NormalizeQueuedSeconds(int storedSeconds, int legacySeconds)
    {
        int normalized = storedSeconds == 1 ? legacySeconds : storedSeconds;
        return Math.Max(0, Math.Min(MaxQueuedSeconds, normalized));
    }

    public static int AddQueuedSeconds(int storedSeconds, int purchasedSeconds, int legacySeconds)
    {
        int currentSeconds = NormalizeQueuedSeconds(storedSeconds, legacySeconds);
        int safePurchase = Math.Max(0, purchasedSeconds);
        return Math.Min(MaxQueuedSeconds, currentSeconds + safePurchase);
    }
}