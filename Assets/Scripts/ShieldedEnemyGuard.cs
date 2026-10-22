using TMPro;
using UnityEngine;

[RequireComponent(typeof(EnemyPatrol2D), typeof(SpriteRenderer))]
public sealed class ShieldedEnemyGuard : MonoBehaviour
{
    SpriteRenderer indicator;
    TextMeshPro label;
    float deflectedUntil;

    public int FacingDirection => transform.localScale.x >= 0f ? 1 : -1;
    public bool RecentlyDeflected => Time.time < deflectedUntil;

    void Awake()
    {
        indicator = GetComponent<SpriteRenderer>();
        label = GetComponentInChildren<TextMeshPro>();
    }

    void Update()
    {
        bool deflected = Time.time < deflectedUntil;
        if (indicator)
        {
            indicator.color = deflected ? new Color(0.45f, 0.95f, 1f) : Color.white;
        }
        if (label)
        {
            Vector3 ls = label.transform.localScale;
            label.transform.localScale = new Vector3(Mathf.Abs(ls.x) * FacingDirection, ls.y, ls.z);
            label.text = deflected ? "BLOCKED!" : "SHIELD";
        }
    }

    public bool ShouldDeflectBullet(Vector2 bulletPosition, float bulletVelocityX)
    {
        int facing = FacingDirection;
        float relativeX = bulletPosition.x - transform.position.x;
        bool hitFromFront = (facing > 0 && relativeX >= -0.05f) || (facing < 0 && relativeX <= 0.05f);
        if (!hitFromFront && Mathf.Abs(bulletVelocityX) > 0.01f)
        {
            hitFromFront = (facing > 0 && bulletVelocityX < 0f) || (facing < 0 && bulletVelocityX > 0f);
        }
        if (hitFromFront)
        {
            deflectedUntil = Time.time + 0.45f;
            return true;
        }
        return false;
    }
}
