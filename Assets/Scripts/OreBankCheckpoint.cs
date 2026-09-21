using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public sealed class OreBankCheckpoint : MonoBehaviour
{
    SpriteRenderer indicator;
    TextMeshPro label;
    Color restingColor;
    float flashUntil;

    void Awake()
    {
        indicator = GetComponent<SpriteRenderer>();
        label = GetComponentInChildren<TextMeshPro>();
        restingColor = indicator.color;
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Update()
    {
        indicator.color = Time.time < flashUntil ? new Color(0.45f, 1f, 0.65f) : restingColor;
        if (label) label.enabled = Time.time >= flashUntil;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (!player || !player.IsAlive) return;
        GameSession session = FindObjectOfType<GameSession>();
        if (!session) return;
        bool hadPressure = session.Pressure.CarriedOre > 0;
        int deposited = session.BankOre();
        if (deposited == 0 && !hadPressure) return;

        flashUntil = Time.time + 0.75f;
        var feedbackObject = new GameObject("OreBankFeedback");
        feedbackObject.transform.position = transform.position + Vector3.up;
        TextMeshPro feedback = feedbackObject.AddComponent<TextMeshPro>();
        feedback.text = $"Banked +{deposited}";
        feedback.fontSize = 3f;
        feedback.alignment = TextAlignmentOptions.Center;
        feedback.color = new Color(0.45f, 1f, 0.65f);
        feedback.sortingLayerID = indicator.sortingLayerID;
        feedback.sortingOrder = 100;
        feedbackObject.AddComponent<CoinPickupFeedback>();
    }
}