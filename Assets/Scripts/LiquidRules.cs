using System;

public enum LiquidKind
{
    None,
    Water,
    Lava
}

public static class LiquidRules
{
    const int LavaLevel = 6;

    public static LiquidKind ClassifyTile(string tileName, int levelNumber)
    {
        if (!IsLiquidTile(tileName)) return LiquidKind.None;
        return levelNumber >= LavaLevel ? LiquidKind.Lava : LiquidKind.Water;
    }

    public static bool IsLiquidTile(string tileName)
    {
        return !string.IsNullOrEmpty(tileName) &&
            (tileName.EndsWith("_28", StringComparison.Ordinal) ||
             tileName.EndsWith("_29", StringComparison.Ordinal));
    }

    public static float GetBreathSeconds(int levelNumber, float baseSeconds,
        float penaltyPerLevel, float minimumSeconds)
    {
        int safeLevel = Math.Max(1, levelNumber);
        float duration = baseSeconds - (safeLevel - 1) * Math.Max(0f, penaltyPerLevel);
        return Math.Max(Math.Max(0.5f, minimumSeconds), duration);
    }
}