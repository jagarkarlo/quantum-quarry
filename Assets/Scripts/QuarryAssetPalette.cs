using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "QuarryAssetPalette", menuName = "QuantumQuarry/Asset Palette")]
public sealed class QuarryAssetPalette : ScriptableObject
{
    public const string ResourcePath = "QuarryAssetPalette";

    [Header("Tiles")]
    public TileBase ruleGroundTile;
    public TileBase rockSlopeTile;
    public TileBase ladderTile;
    public TileBase bounceMushroomTile;
    public TileBase spikesTile;
    public TileBase waterSurfaceTile;
    public TileBase waterBodyTile;
    public TileBase lavaSurfaceTile;
    public TileBase lavaBodyTile;
    public TileBase backgroundTile;

    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject exitPrefab;
    public GameObject enemyPrefab;
    public GameObject redEnemyPrefab;
    public GameObject movingPlatformPrefab;
    public GameObject coinPrefab;
    public GameObject breakableCratePrefab;
    public GameObject bankCheckpointPrefab;
    public GameObject pulseVentPrefab;
    public GameObject pressureGatePrefab;
    public GameObject stabilizationPickupPrefab;
    public GameObject jumpPadPrefab;
    public GameObject sentryPrefab;
    public GameObject shieldedEnemyPrefab;

    static QuarryAssetPalette cached;

    public static QuarryAssetPalette Load()
    {
        if (!cached) cached = Resources.Load<QuarryAssetPalette>(ResourcePath);
        return cached;
    }

    public TileBase GetTileById(string id)
    {
        switch (id)
        {
            case "ground": return ruleGroundTile ? ruleGroundTile : rockSlopeTile;
            case "rock": return rockSlopeTile ? rockSlopeTile : ruleGroundTile;
            case "ladder": return ladderTile;
            case "bounce": return bounceMushroomTile;
            case "hazard_spike": return spikesTile;
            case "water_surface": return waterSurfaceTile;
            case "water": return waterBodyTile ? waterBodyTile : waterSurfaceTile;
            case "lava_surface": return lavaSurfaceTile;
            case "lava": return lavaBodyTile ? lavaBodyTile : lavaSurfaceTile;
            case "bg": return backgroundTile;
            default: return null;
        }
    }

    public string ClassifyTileAsset(TileBase tile, string mapLayerName)
    {
        if (!tile) return null;
        string n = tile.name;
        if (tile == ladderTile || n == "Ljestve" || mapLayerName == "Climbing") return "ladder";
        if (tile == bounceMushroomTile || n == "Gljiva" || mapLayerName == "Jump") return "bounce";
        if (tile == spikesTile || n == "Spikes") return "hazard_spike";
        if (tile == lavaSurfaceTile || n == "LavaSurface") return "lava_surface";
        if (tile == lavaBodyTile || n == "LavaBody") return "lava";
        if (tile == waterSurfaceTile || n == "SPA_Rock_Grass_Water_29") return "water_surface";
        if (tile == waterBodyTile || n == "SPA_Rock_Grass_Water_28") return "water";
        if (mapLayerName == "Background" || n.StartsWith("Pozadina") || n.StartsWith("SPA_Background")) return "bg";
        if (tile == ruleGroundTile || n == "Rule plocica") return "ground";
        return "ground";
    }
}
