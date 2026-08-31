using System;

public static class StoreEconomy
{
    public const int MaxQueuedSeconds = 120;
    public const int MaxArmorTier = 2;

    // Index 0 unused (tier 0 = no armor); costs/reduction apply to tiers 1..MaxArmorTier.
    static readonly int[] ArmorUpgradeCosts = { 0, 200, 350 };
    static readonly int[] ArmorDamageReductionPercent = { 0, 25, 45 };

    public static bool IsValidArmorTier(int tier)
    {
        return tier >= 1 && tier <= MaxArmorTier;
    }

    public static int GetArmorUpgradeCost(int tier)
    {
        return IsValidArmorTier(tier) ? ArmorUpgradeCosts[tier] : int.MaxValue;
    }

    public static int GetArmorDamageReductionPercent(int tier)
    {
        int safeTier = Math.Max(0, Math.Min(MaxArmorTier, tier));
        return ArmorDamageReductionPercent[safeTier];
    }

    public static int ApplyArmorReductionUnits(int units, int armorTier)
    {
        int safeUnits = Math.Max(0, units);
        if (safeUnits == 0) return 0;

        int reductionPercent = GetArmorDamageReductionPercent(armorTier);
        int reducedUnits = safeUnits - safeUnits * reductionPercent / 100;
        // Armor mitigates damage but never grants full immunity to a hit.
        return Math.Max(1, reducedUnits);
    }

    public static bool CanAfford(int balance, int cost)
    {
        return cost >= 0 && balance >= cost;
    }

    public static bool HasQueueCapacity(int storedSeconds, int purchasedSeconds)
    {
        int safePurchase = Math.Max(0, purchasedSeconds);
        int currentSeconds = NormalizeQueuedSeconds(storedSeconds, safePurchase);
        return safePurchase > 0 && currentSeconds <= MaxQueuedSeconds - safePurchase;
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