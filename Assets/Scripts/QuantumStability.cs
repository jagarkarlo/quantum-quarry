using System;

[Serializable]
public sealed class QuantumStability
{
    readonly int maxStability;
    int currentStability;

    public int Max => maxStability;
    public int Current => currentStability;
    public bool IsCritical => currentStability == 1;
    public bool IsDepleted => currentStability <= 0;

    public QuantumStability(int maxStability, int currentStability)
    {
        this.maxStability = Math.Max(1, maxStability);
        this.currentStability = Clamp(currentStability, 0, this.maxStability);
    }

    public bool TakeDamage(int amount)
    {
        if (amount <= 0 || IsDepleted) return false;

        currentStability = Math.Max(0, currentStability - amount);
        return true;
    }

    public bool Heal(int amount)
    {
        if (amount <= 0 || IsDepleted || currentStability >= maxStability) return false;

        currentStability = Math.Min(maxStability, currentStability + amount);
        return true;
    }

    public void Restore()
    {
        currentStability = maxStability;
    }

    static int Clamp(int value, int minimum, int maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }
}