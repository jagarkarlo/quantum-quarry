using System;

[Serializable]
public sealed class QuantumStability
{
    public const int UnitsPerPoint = 2;

    readonly int maxUnits;
    int currentUnits;

    public float Max => maxUnits / (float)UnitsPerPoint;
    public float Current => currentUnits / (float)UnitsPerPoint;
    public int CurrentUnits => currentUnits;
    public bool IsCritical => currentUnits > 0 && currentUnits <= UnitsPerPoint;
    public bool IsDepleted => currentUnits <= 0;

    public QuantumStability(int maxStability, int currentStability)
    {
        maxUnits = Math.Max(UnitsPerPoint, maxStability * UnitsPerPoint);
        currentUnits = Clamp(currentStability * UnitsPerPoint, 0, maxUnits);
    }

    public static QuantumStability FromUnits(int maxUnits, int currentUnits)
    {
        var stability = new QuantumStability(maxUnits);
        stability.currentUnits = Clamp(currentUnits, 0, stability.maxUnits);
        return stability;
    }

    QuantumStability(int maxUnits)
    {
        this.maxUnits = Math.Max(UnitsPerPoint, maxUnits);
        currentUnits = this.maxUnits;
    }

    public bool TakeDamage(int amount)
    {
        return TakeDamageUnits(amount * UnitsPerPoint);
    }

    public bool TakeDamageUnits(int units)
    {
        if (units <= 0 || IsDepleted) return false;

        currentUnits = Math.Max(0, currentUnits - units);
        return true;
    }

    public bool Heal(int amount)
    {
        if (amount <= 0 || IsDepleted || currentUnits >= maxUnits) return false;

        currentUnits = Math.Min(maxUnits, currentUnits + amount * UnitsPerPoint);
        return true;
    }

    public void Restore()
    {
        currentUnits = maxUnits;
    }

    static int Clamp(int value, int minimum, int maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }
}