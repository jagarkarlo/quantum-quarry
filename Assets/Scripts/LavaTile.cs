using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "LavaTile", menuName = "QuantumQuarry/Lava Tile")]
public sealed class LavaTile : Tile
{
    public Sprite[] frames;
    public const float FramesPerSecond = 4f;

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
    {
        if (frames == null || frames.Length == 0)
        {
            Debug.LogError($"Lava tile {name} has no animation frames.", this);
            return false;
        }
        tileAnimationData.animatedSprites = frames;
        tileAnimationData.animationSpeed = FramesPerSecond;
        tileAnimationData.animationStartTime = 0f;
        return true;
    }
}
