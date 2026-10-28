using System;
using UnityEngine;

[Serializable]
public struct StoreProductEntry
{
    public string productId;
    public string displayName;
    public int basePrice;
    public int durationSeconds;
    public Color buttonColor;
}

[CreateAssetMenu(fileName = "StoreCatalog", menuName = "QuantumQuarry/Store Catalog")]
public sealed class StoreCatalog : ScriptableObject
{
    public const string ResourcePath = "StoreCatalog";

    public StoreProductEntry extraLife = new StoreProductEntry
    {
        productId = "extra_life",
        displayName = "EXTRA LIFE",
        basePrice = 50,
        durationSeconds = 0,
        buttonColor = new Color(0.98f, 0.69f, 0.25f)
    };

    public StoreProductEntry speedBoost = new StoreProductEntry
    {
        productId = "speed_boost",
        displayName = "SPEED BOOST",
        basePrice = 100,
        durationSeconds = 10,
        buttonColor = new Color(0.18f, 0.72f, 0.92f)
    };

    public StoreProductEntry invisibility = new StoreProductEntry
    {
        productId = "invisibility",
        displayName = "INVISIBILITY",
        basePrice = 150,
        durationSeconds = 10,
        buttonColor = new Color(0.35f, 0.82f, 0.63f)
    };

    public StoreProductEntry doubleJump = new StoreProductEntry
    {
        productId = "double_jump",
        displayName = "DOUBLE JUMP",
        basePrice = 120,
        durationSeconds = 10,
        buttonColor = new Color(0.78f, 0.48f, 0.86f)
    };

    static StoreCatalog cached;

    public static StoreCatalog Load()
    {
        if (!cached) cached = Resources.Load<StoreCatalog>(ResourcePath);
        return cached;
    }
}
