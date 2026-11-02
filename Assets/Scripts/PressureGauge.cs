using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public sealed class PressureGauge : MonoBehaviour
{
    GameSession session;
    SpriteRenderer indicator;
    TextMeshPro label;
    int lastOre = -1;
    int lastTier = -1;

    public string CurrentText => label ? label.text : string.Empty;

    void Awake()
    {
        indicator = GetComponent<SpriteRenderer>();
        label = GetComponentInChildren<TextMeshPro>();
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        session = FindObjectOfType<GameSession>();
        RefreshGauge();
    }

    void Update()
    {
        if (!session) session = FindObjectOfType<GameSession>();
        if (!session) return;
        if (session.Pressure.CarriedOre != lastOre || session.Pressure.Tier != lastTier)
            RefreshGauge();
    }

    public void RefreshGauge()
    {
        if (!session) return;
        lastOre = session.Pressure.CarriedOre;
        lastTier = session.Pressure.Tier;
        if (label) label.text = session.Pressure.GetGaugeLabel();
        if (indicator)
        {
            indicator.color = lastTier >= 3 ? new Color(1f, 0.35f, 0.3f) :
                              lastTier == 2 ? new Color(1f, 0.82f, 0.28f) :
                              lastTier == 1 ? new Color(0.45f, 0.95f, 0.65f) :
                              new Color(0.65f, 0.85f, 0.95f);
        }
    }
}
