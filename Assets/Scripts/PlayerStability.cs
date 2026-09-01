using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D))]
public sealed class PlayerStability : MonoBehaviour
{
    [Header("Contact Damage")]
    [SerializeField] int enemyDamage = 1;
    [SerializeField] int hazardDamage = 2;
    [SerializeField] float invulnerabilitySeconds = 1f;
    [SerializeField] Vector2 knockback = new Vector2(10f, 12f);

    [Header("Hit Feedback")]
    [SerializeField] float hitFlashSeconds = 0.15f;
    [SerializeField] Color hitFlashColor = new Color(1f, 0.35f, 0.35f);

    [Header("Underwater Breath")]
    [SerializeField] float baseBreathSeconds = 6f;
    [SerializeField] float breathPenaltyPerLevel = 0.5f;
    [SerializeField] float minimumBreathSeconds = 3f;
    [SerializeField] float drowningTickSeconds = 1.25f;

    PlayerMovement movement;
    Rigidbody2D rb;
    GameSession session;
    SpriteRenderer playerRenderer;
    int enemyLayer;
    int hazardLayer;
    float invulnerableUntil;
    float hitFlashUntil;
    float breathRemaining;
    float breathMaximum;
    float drowningTimer;

    public bool IsInvulnerable => Time.time < invulnerableUntil;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>();
        playerRenderer = GetComponent<SpriteRenderer>();
        enemyLayer = LayerMask.NameToLayer("Enemy");
        hazardLayer = LayerMask.NameToLayer("Hazard");
        breathMaximum = LiquidRules.GetBreathSeconds(GetLevelNumber(), baseBreathSeconds,
            breathPenaltyPerLevel, minimumBreathSeconds);
        breathRemaining = breathMaximum;
    }

    void Start()
    {
        session = FindObjectOfType<GameSession>();
        if (!session) Debug.LogError("GameSession not found: Quantum Stability is unavailable.");
    }

    void Update()
    {
        ProcessLiquid();

        if (!playerRenderer) return;

        playerRenderer.color = Time.time < hitFlashUntil
            ? hitFlashColor
            : IsInvulnerable && Mathf.FloorToInt(Time.unscaledTime * 16f) % 2 == 0
                ? new Color(0.45f, 0.95f, 1f, 0.35f)
                : Color.white;
    }

    void OnDisable()
    {
        if (playerRenderer) playerRenderer.color = Color.white;
        if (session) session.ClearBreathStatus();
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

        LiquidKind contactLiquid = movement.GetContactLiquid(contactObject);
        if (contactLiquid == LiquidKind.Lava)
        {
            movement.Kill(kick);
            return;
        }

        if (contactLiquid == LiquidKind.Water) return;

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
        hitFlashUntil = Time.time + hitFlashSeconds;
        rb.velocity = kick;
    }

    void ProcessLiquid()
    {
        if (!movement.IsAlive || movement.IsInvisible || !session)
        {
            ResetBreath();
            return;
        }

        if (movement.IsInLava)
        {
            session.ClearBreathStatus();
            movement.Kill(Vector2.up * knockback.y);
            return;
        }

        if (!movement.IsSwimming || !movement.IsSubmerged)
        {
            ResetBreath();
            return;
        }

        breathRemaining = Mathf.Max(0f, breathRemaining - Time.deltaTime);
        session.SetBreathStatus(breathRemaining, breathMaximum);
        if (breathRemaining > 0f) return;

        drowningTimer += Time.deltaTime;
        if (drowningTimer < drowningTickSeconds) return;

        drowningTimer = 0f;
        if (session.TakeStabilityDamageUnits(1))
        {
            session.ClearBreathStatus();
            movement.Kill(Vector2.up * knockback.y);
        }
        else
        {
            hitFlashUntil = Time.time + hitFlashSeconds;
        }
    }

    void ResetBreath()
    {
        breathRemaining = breathMaximum;
        drowningTimer = 0f;
        if (session) session.ClearBreathStatus();
    }

    Vector2 CalculateKnockback(Vector3 contactPosition)
    {
        float direction = Mathf.Sign(transform.position.x - contactPosition.x);
        if (Mathf.Approximately(direction, 0f)) direction = -Mathf.Sign(rb.velocity.x);
        if (Mathf.Approximately(direction, 0f)) direction = 1f;
        return new Vector2(direction * knockback.x, knockback.y);
    }

    static int GetLevelNumber()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!sceneName.StartsWith("Level ")) return 1;
        return int.TryParse(sceneName.Substring(6), out int parsedLevel) ? Mathf.Max(1, parsedLevel) : 1;
    }
}