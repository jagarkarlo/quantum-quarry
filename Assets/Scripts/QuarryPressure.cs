using System;

[Serializable]
public sealed class QuarryPressure
{
    public const int DefaultSeed = 7319;
    public const int FirstThreshold = 500;
    public const int SecondThreshold = 1500;
    public const int ThirdThreshold = 3000;

    public int CarriedOre { get; private set; }
    public int PendingCoins { get; private set; }
    public int Seed { get; }
    public int Tier => CarriedOre >= ThirdThreshold ? 3 :
        CarriedOre >= SecondThreshold ? 2 : CarriedOre >= FirstThreshold ? 1 : 0;
    public int RewardPercent => 100 + Tier * 25;
    public int NextThreshold => Tier == 0 ? FirstThreshold : Tier == 1 ? SecondThreshold :
        Tier == 2 ? ThirdThreshold : 0;
    public bool HasHazardPulses => Tier >= 2;
    public bool HasSwiftEnemies => Tier > 0 && ((unchecked((uint)Seed * 1664525u +
        (uint)Tier * 1013904223u) >> 16) & 1u) != 0;
    public float PerceptionMultiplier => 1f + Tier * 0.15f;
    public float ChaseMultiplier => HasSwiftEnemies ? 1f + Tier * 0.1f : 1f;

    public QuarryPressure(int seed = DefaultSeed, int carriedOre = 0, int pendingCoins = 0)
    {
        Seed = seed;
        CarriedOre = Math.Max(0, carriedOre);
        PendingCoins = Math.Max(0, pendingCoins);
    }

    public int PreviewReward(int baseValue, bool critical)
    {
        long reward = (long)Math.Max(0, baseValue) * RewardPercent * (critical ? 2 : 1) / 100;
        return (int)Math.Min(int.MaxValue - (long)PendingCoins, reward);
    }

    public int Collect(int baseValue, bool critical)
    {
        int reward = PreviewReward(baseValue, critical);
        CarriedOre = (int)Math.Min(int.MaxValue, (long)CarriedOre + Math.Max(0, baseValue));
        PendingCoins += reward;
        return reward;
    }

    public int Bank()
    {
        int banked = PendingCoins;
        CarriedOre = 0;
        PendingCoins = 0;
        return banked;
    }

    public void Discard()
    {
        CarriedOre = 0;
        PendingCoins = 0;
    }
}