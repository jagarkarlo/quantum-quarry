using System;

[Serializable]
public sealed class DamageLog
{
    public int HitsTaken { get; private set; }
    public int StabilityUnitsLost { get; private set; }

    public float StabilityPointsLost => StabilityUnitsLost / (float)QuantumStability.UnitsPerPoint;

    public DamageLog(int hitsTaken, int stabilityUnitsLost)
    {
        HitsTaken = Math.Max(0, hitsTaken);
        StabilityUnitsLost = Math.Max(0, stabilityUnitsLost);
    }

    public void RecordHit(int unitsLost)
    {
        if (unitsLost <= 0) return;

        HitsTaken++;
        StabilityUnitsLost += unitsLost;
    }
}
