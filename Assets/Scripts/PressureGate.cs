using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public sealed class PressureGate : MonoBehaviour
{
    [SerializeField, Range(1, 3)] int requiredTier = 1;

    GameSession session;
    Collider2D barrier;
    SpriteRenderer indicator;
    TextMeshPro label;
    int lastTier = -1;

    public int RequiredTier => Mathf.Clamp(requiredTier, 1, 3);
    public bool IsOpen => session && session.Pressure.MeetsGateRequirement(RequiredTier);

    public void SetRequiredTier(int tier)
    {
        requiredTier = Mathf.Clamp(tier, 1, 3);
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
        bool open = session.Pressure.MeetsGateRequirement(RequiredTier);
        if (barrier) barrier.enabled = !open;
        if (label) label.text = session.Pressure.GetGateLabel(RequiredTier);
        if (indicator)
        {
            indicator.color = open
                ? new Color(0.35f, 0.95f, 0.65f, 0.35f)
                : new Color(1f, 0.65f, 0.25f, 1f);
        }
    }
}
