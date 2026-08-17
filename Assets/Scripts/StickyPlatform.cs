using UnityEngine;

public class StickyPlatform : MonoBehaviour
{
    FollowingPoint movement;
    Collider2D platformCollider;

    public Vector2 DeltaPosition => movement ? movement.DeltaPosition : Vector2.zero;
    public Vector2 Velocity => movement ? movement.Velocity : Vector2.zero;

    void Awake()
    {
        movement = GetComponent<FollowingPoint>();
        platformCollider = GetComponent<Collider2D>();
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player") || !platformCollider) return;

        PlayerMovement player = collision.gameObject.GetComponent<PlayerMovement>();
        Collider2D playerCollider = collision.collider;
        if (!player || !playerCollider) return;

        float platformCenter = platformCollider.bounds.center.y;
        bool playerIsAbove = playerCollider.bounds.min.y >= platformCenter - 0.1f;
        if (playerIsAbove) player.AttachToPlatform(this);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        PlayerMovement player = collision.gameObject.GetComponent<PlayerMovement>();
        if (player) player.DetachFromPlatform(this, false);
    }
}
