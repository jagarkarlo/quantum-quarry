using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public sealed class JumpPad : MonoBehaviour
{
    [SerializeField, Min(1f)] float launchVelocity = 13.5f;

    SpriteRenderer indicator;
    TextMeshPro label;
    Vector3 restingScale;
    float flashUntil;

    public float LaunchVelocity => Mathf.Max(1f, launchVelocity);
    public bool IsFlashing => Time.time < flashUntil;

    void Awake()
    {
        indicator = GetComponent<SpriteRenderer>();
        label = GetComponentInChildren<TextMeshPro>();
        restingScale = transform.localScale;
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Update()
    {
        bool active = Time.time < flashUntil;
        if (indicator)
        {
            indicator.color = active ? new Color(0.55f, 1f, 0.75f) : Color.white;
            transform.localScale = active
                ? new Vector3(restingScale.x * 1.12f, restingScale.y * 0.86f, restingScale.z)
                : restingScale;
        }
        if (label) label.text = active ? "BOOST!" : "JUMP";
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryLaunch(other);
    }

    public bool TryLaunch(Collider2D other)
    {
        if (!other) return false;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (!player || !player.IsAlive || player.IsInvisible) return false;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (!rb) return false;

        rb.velocity = new Vector2(rb.velocity.x, Mathf.Max(rb.velocity.y, LaunchVelocity));
        flashUntil = Time.time + 0.35f;
        return true;
    }
}
