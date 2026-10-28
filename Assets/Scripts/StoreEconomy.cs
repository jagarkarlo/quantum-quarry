using System;

public static class StoreEconomy
{
    public const int MaxQueuedSeconds = 120;
    public const int MaxArmorTier = 2;
    public const int MaxPickaxeTier = 2;
    public const int MaxMagnetTier = 2;

    // Index 0 unused (tier 0 = no armor); costs/reduction apply to tiers 1..MaxArmorTier.
    static readonly int[] ArmorUpgradeCosts = { 0, 200, 350 };
    static readonly int[] ArmorDamageReductionPercent = { 0, 25, 45 };
    static readonly int[] PickaxeUpgradeCosts = { 0, 250, 450 };
    static readonly int[] PickaxeBonusPercent = { 0, 15, 30 };
    static readonly int[] MagnetUpgradeCosts = { 0, 180, 320 };
    static readonly float[] MagnetRadiusUnits = { 0f, 2.2f, 3.8f };

    public static bool IsValidArmorTier(int tier)
    {
        return tier >= 1 && tier <= MaxArmorTier;
    }

    public static bool IsValidPickaxeTier(int tier)
    {
        return tier >= 1 && tier <= MaxPickaxeTier;
    }

    public static int GetPickaxeUpgradeCost(int tier)
    {
        return IsValidPickaxeTier(tier) ? PickaxeUpgradeCosts[tier] : int.MaxValue;
    }

    public static int GetPickaxeBonusPercent(int tier)
    {
        int safeTier = Math.Max(0, Math.Min(MaxPickaxeTier, tier));
        return PickaxeBonusPercent[safeTier];
    }

    public static int ApplyPickaxeBonusOre(int baseOre, int pickaxeTier)
    {
        int safeOre = Math.Max(0, baseOre);
        int bonusPct = GetPickaxeBonusPercent(pickaxeTier);
        return safeOre + safeOre * bonusPct / 100;
    }

    public static bool IsValidMagnetTier(int tier)
    {
        return tier >= 1 && tier <= MaxMagnetTier;
    }

    public static int GetMagnetUpgradeCost(int tier)
    {
        return IsValidMagnetTier(tier) ? MagnetUpgradeCosts[tier] : int.MaxValue;
    }

    public static float GetMagnetRadius(int tier)
    {
        int safeTier = Math.Max(0, Math.Min(MaxMagnetTier, tier));
        return MagnetRadiusUnits[safeTier];
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