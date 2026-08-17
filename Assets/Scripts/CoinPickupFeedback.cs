using UnityEngine;
using TMPro;

public class CoinPickupFeedback : MonoBehaviour
{
    [SerializeField] float lifetime = 0.75f;
    [SerializeField] float riseSpeed = 1.2f;

    float elapsed;
    TextMeshPro feedbackText;
    Color startColor;

    void Awake()
    {
        feedbackText = GetComponent<TextMeshPro>();
        if (feedbackText) startColor = feedbackText.color;
    }

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        transform.position += Vector3.up * (riseSpeed * Time.unscaledDeltaTime);

        if (feedbackText)
        {
            Color color = startColor;
            color.a = 1f - Mathf.Clamp01(elapsed / lifetime);
            feedbackText.color = color;
        }

        if (elapsed >= lifetime) Destroy(gameObject);
    }
}