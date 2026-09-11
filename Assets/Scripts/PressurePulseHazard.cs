using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public sealed class PressurePulseHazard : MonoBehaviour
{
    [SerializeField, Min(1)] int damage = 1;
    GameSession session;
    SpriteRenderer indicator;
    float elapsed;
    int previousTier;
    Vector3 restingScale;

    public int Damage => Mathf.Max(1, damage);
    public bool IsActive => session && session.Pressure.GetPulsePhase(elapsed) == QuarryPressure.PulsePhase.Active;

    void Start()
    {
        session = FindObjectOfType<GameSession>();
        indicator = GetComponent<SpriteRenderer>();
        restingScale = transform.localScale;
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Update()
    {
        if (!session) return;
        int tier = session.Pressure.Tier;
        if (tier != previousTier) elapsed = 0f;
        previousTier = tier;
        elapsed = session.Pressure.HasHazardPulses ? elapsed + Time.deltaTime : 0f;
        QuarryPressure.PulsePhase phase = session.Pressure.GetPulsePhase(elapsed);
        indicator.color = phase == QuarryPressure.PulsePhase.Active ? new Color(1f, 0.25f, 0.2f) :
            phase == QuarryPressure.PulsePhase.Warning ? new Color(1f, 0.8f, 0.2f) :
            new Color(0.35f, 0.5f, 0.55f);
        indicator.transform.localScale = restingScale *
            (phase == QuarryPressure.PulsePhase.Warning ? 1f + Mathf.Sin(elapsed * 12f) * 0.05f : 1f);
    }
}