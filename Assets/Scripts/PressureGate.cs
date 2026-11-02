using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public sealed class PressureGate : MonoBehaviour
{
    [SerializeField, Range(1, 3)] int requiredTier = 1;
    [SerializeField] bool latchWhenOpened;

    GameSession session;
    Collider2D barrier;
    SpriteRenderer indicator;
    TextMeshPro label;
    int lastTier = -1;
    bool isLatchedOpen;

    public int RequiredTier => Mathf.Clamp(requiredTier, 1, 3);
    public bool LatchWhenOpened => latchWhenOpened;
    public bool IsLatchedOpen => isLatchedOpen;
    public bool IsOpen => isLatchedOpen || (session && session.Pressure.MeetsGateRequirement(RequiredTier));

    public void SetRequiredTier(int tier, bool latch = false)
    {
        requiredTier = Mathf.Clamp(tier, 1, 3);
        latchWhenOpened = latch;
        isLatchedOpen = false;
        lastTier = -1;
        RefreshState();
    }

    void Awake()
    {
        barrier = GetComponent<Collider2D>();
        indicator = GetComponent<SpriteRenderer>();
        label = GetComponentInChildren<TextMeshPro>();
        barrier.isTrigger = false;
    }

    void Start()
    {
        session = FindObjectOfType<GameSession>();
        RefreshState();
    }

    void Update()
    {
        if (!session) session = FindObjectOfType<GameSession>();
        if (!session) return;
        if (session.Pressure.Tier != lastTier) RefreshState();
    }

    public void RefreshState()
    {
        if (!session) return;
        lastTier = session.Pressure.Tier;
        bool meets = session.Pressure.MeetsGateRequirement(RequiredTier);
        if (meets && latchWhenOpened) isLatchedOpen = true;
        bool open = isLatchedOpen || meets;
        if (!open && barrier && !barrier.enabled && IsPlayerInsideGateZone())
        {
            // Anti-crush safety: keep gate open while player overlaps its footprint.
            open = true;
        }
        if (barrier) barrier.enabled = !open;
        if (label)
        {
            label.text = isLatchedOpen ? "LATCHED" : session.Pressure.GetGateLabel(RequiredTier);
        }
        if (indicator)
        {
            indicator.color = open
                ? new Color(0.35f, 0.95f, 0.65f, 0.35f)
                : new Color(1f, 0.65f, 0.25f, 1f);
        }
    }

    bool IsPlayerInsideGateZone()
    {
        int playerMask = LayerMask.GetMask("Player");
        if (playerMask == 0) return false;
        return Physics2D.OverlapBox(transform.position, new Vector2(0.95f, 1.9f), 0f, playerMask) != null;
    }
}
