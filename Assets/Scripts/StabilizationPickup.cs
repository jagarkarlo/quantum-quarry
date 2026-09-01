using System.Collections;
using UnityEngine;
using TMPro;

public class StabilizationPickup : MonoBehaviour
{
    [SerializeField] AudioClip pickupSound;
    [SerializeField] int restoreAmount = 1;

    bool wasCollected;
    SpriteRenderer pickupRenderer;
    Collider2D pickupCollider;

    void Awake()
    {
        pickupRenderer = GetComponent<SpriteRenderer>();
        pickupCollider = GetComponent<Collider2D>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (wasCollected) return;
        if (!other.CompareTag("Player")) return;

        var session = FindObjectOfType<GameSession>();
        if (!session)
        {
            Debug.LogWarning("GameSession not found: Stability was not restored.");
            return;
        }

        // Leave the pickup available if the player is already at maximum Stability.
        if (!session.HealStability(restoreAmount)) return;

        wasCollected = true;

        if (pickupSound && Camera.main)
            AudioSource.PlayClipAtPoint(pickupSound, Camera.main.transform.position);

        ShowRestoreFeedback();
        if (pickupRenderer) pickupRenderer.enabled = false;
        if (pickupCollider) pickupCollider.enabled = false;
        StartCoroutine(DestroyAfterFeedback());
    }

    void ShowRestoreFeedback()
    {
        GameObject feedbackObject = new GameObject("StabilityRestoreFeedback");
        feedbackObject.transform.position = transform.position + Vector3.up * 0.5f;

        TextMeshPro feedback = feedbackObject.AddComponent<TextMeshPro>();
        feedback.text = $"+{restoreAmount} Stability";
        feedback.fontSize = 4f;
        feedback.alignment = TextAlignmentOptions.Center;
        feedback.color = new Color(0.45f, 0.95f, 1f);
        feedback.sortingOrder = 100;

        feedbackObject.AddComponent<CoinPickupFeedback>();
    }

    IEnumerator DestroyAfterFeedback()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        Destroy(gameObject);
    }
}
