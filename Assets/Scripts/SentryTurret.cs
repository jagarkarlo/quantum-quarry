using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public sealed class SentryTurret : MonoBehaviour
{
    public enum SentryPhase
    {
        Idle,
        Aiming,
        Firing
    }

    [SerializeField] float baseRange = 6.5f;
    [SerializeField] float verticalRange = 2.2f;
    [SerializeField] float aimDuration = 1.1f;
    [SerializeField] float fireDuration = 0.45f;
    [SerializeField] float cooldownDuration = 1.4f;
    [SerializeField] LayerMask groundMask = 1 << 6;

    GameSession session;
    PlayerMovement player;
    SpriteRenderer indicator;
    TextMeshPro label;
    SentryPhase phase = SentryPhase.Idle;
    float timer;
    int facingDir = 1;

    public SentryPhase Phase => phase;
    public float EffectiveRange => baseRange * (session ? session.Pressure.PerceptionMultiplier : 1f);

    void Awake()
    {
        indicator = GetComponent<SpriteRenderer>();
        label = GetComponentInChildren<TextMeshPro>();
        if (groundMask.value == 0) groundMask = LayerMask.GetMask("Ground", "Jump");
    }

    void Start()
    {
        session = FindObjectOfType<GameSession>();
        FindPlayer();
        RefreshVisuals();
    }

    void Update()
    {
        if (!session) session = FindObjectOfType<GameSession>();
        if (!player) FindPlayer();

        bool seesPlayer = CanSeePlayer();

        if (phase == SentryPhase.Idle)
        {
            if (timer > 0f)
            {
                timer -= Time.deltaTime;
            }
            else if (seesPlayer)
            {
                FacePlayer();
                phase = SentryPhase.Aiming;
                timer = aimDuration;
            }
        }
        else if (phase == SentryPhase.Aiming)
        {
            if (!seesPlayer)
            {
                phase = SentryPhase.Idle;
                timer = 0.25f;
            }
            else
            {
                FacePlayer();
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    phase = SentryPhase.Firing;
                    timer = fireDuration;
                    DeliverBeamPulse();
                }
            }
        }
        else if (phase == SentryPhase.Firing)
        {
            timer -= Time.deltaTime;
            if (seesPlayer) DeliverBeamPulse();
            if (timer <= 0f)
            {
                phase = SentryPhase.Idle;
                timer = cooldownDuration;
            }
        }

        RefreshVisuals();
    }

    void DeliverBeamPulse()
    {
        if (!CanSeePlayer() || !player || !session) return;
        PlayerStability stability = player.GetComponent<PlayerStability>();
        if (stability && stability.IsInvulnerable) return;
        if (stability)
        {
            stability.SendMessage("ProcessContact", gameObject, SendMessageOptions.DontRequireReceiver);
        }
    }

    public bool CanSeePlayer()
    {
        if (!player || !player.IsAlive || player.IsInvisible) return false;
        Vector2 offset = player.transform.position - transform.position;
        if (Mathf.Abs(offset.y) > verticalRange || offset.sqrMagnitude > EffectiveRange * EffectiveRange)
            return false;
        return !Physics2D.Linecast(transform.position, player.transform.position, groundMask);
    }

    void FacePlayer()
    {
        if (!player) return;
        float dx = player.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f)
        {
            facingDir = dx >= 0f ? 1 : -1;
            Vector3 s = transform.localScale;
            transform.localScale = new Vector3(Mathf.Abs(s.x) * facingDir, s.y, s.z);
            if (label)
            {
                Vector3 ls = label.transform.localScale;
                label.transform.localScale = new Vector3(Mathf.Abs(ls.x) * facingDir, ls.y, ls.z);
            }
        }
    }

    void RefreshVisuals()
    {
        if (label)
        {
            label.text = phase == SentryPhase.Firing ? "FIRE!" :
                         phase == SentryPhase.Aiming ? "AIM!" : "SENTRY";
        }
        if (indicator)
        {
            indicator.color = phase == SentryPhase.Firing ? new Color(1f, 0.28f, 0.22f) :
                              phase == SentryPhase.Aiming ? new Color(1f, 0.85f, 0.25f) :
                              Color.white;
        }
    }

    void FindPlayer()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go) player = go.GetComponent<PlayerMovement>();
        if (!player) player = FindObjectOfType<PlayerMovement>();
    }
}
