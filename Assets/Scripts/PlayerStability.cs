using UnityEngine;

[RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D))]
public sealed class PlayerStability : MonoBehaviour
{
    [Header("Contact Damage")]
    [SerializeField] int enemyDamage = 1;
    [SerializeField] int hazardDamage = 2;
    [SerializeField] float invulnerabilitySeconds = 1f;
    [SerializeField] Vector2 knockback = new Vector2(10f, 12f);

    PlayerMovement movement;
    Rigidbody2D rb;
    GameSession session;
    SpriteRenderer playerRenderer;
    int enemyLayer;
    int hazardLayer;
    int waterLayer;
    float invulnerableUntil;

    public bool IsInvulnerable => Time.time < invulnerableUntil;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>();
        playerRenderer = GetComponent<SpriteRenderer>();
        enemyLayer = LayerMask.NameToLayer("Enemy");
        hazardLayer = LayerMask.NameToLayer("Hazard");
        waterLayer = LayerMask.NameToLayer("Water");
    }

    void Start()
    {
        session = FindObjectOfType<GameSession>();
        if (!session) Debug.LogError("GameSession not found: Quantum Stability is unavailable.");
    }

    void Update()
    {
        if (playerRenderer)
            playerRenderer.color = IsInvulnerable && Mathf.FloorToInt(Time.unscaledTime * 16f) % 2 == 0
                ? new Color(0.45f, 0.95f, 1f, 0.35f)
                : Color.white;
    }

    void OnDisable()
    {
        if (playerRenderer) playerRenderer.color = Color.white;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        ProcessContact(collision.gameObject);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        ProcessContact(collision.gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        ProcessContact(other.gameObject);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        ProcessContact(other.gameObject);
    }

    void ProcessContact(GameObject contactObject)
    {
        if (!movement.IsAlive || movement.IsInvisible || !session) return;

        int layer = contactObject.layer;
        Vector2 kick = CalculateKnockback(contactObject.transform.position);

        if (layer == waterLayer)
        {
            movement.Kill(kick);
            return;
        }

        if (IsInvulnerable) return;

        int damage = layer == enemyLayer ? enemyDamage : layer == hazardLayer ? hazardDamage : 0;
        if (damage <= 0) return;

        bool depleted = session.TakeStabilityDamage(damage);
        if (depleted)
        {
            movement.Kill(kick);
            return;
        }

        invulnerableUntil = Time.time + invulnerabilitySeconds;
        rb.velocity = kick;
    }

    Vector2 CalculateKnockback(Vector3 contactPosition)
    {
        float direction = Mathf.Sign(transform.position.x - contactPosition.x);
        if (Mathf.Approximately(direction, 0f)) direction = -Mathf.Sign(rb.velocity.x);
        if (Mathf.Approximately(direction, 0f)) direction = 1f;
        return new Vector2(direction * knockback.x, knockback.y);
    }
}