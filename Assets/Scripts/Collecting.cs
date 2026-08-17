using UnityEngine;

public class Collecting : MonoBehaviour
{
    [SerializeField] AudioClip coinPickup;
    [SerializeField] int pointsForCoinPickup = 100;

    bool wasCollected = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (wasCollected) return;
        if (!other.CompareTag("Player")) return;

        wasCollected = true;

        var gs = FindObjectOfType<GameSession>();
        if (gs) gs.AddCoins(pointsForCoinPickup);
        else Debug.LogWarning("GameSession not found: score not added.");

        if (coinPickup && Camera.main)
            AudioSource.PlayClipAtPoint(coinPickup, Camera.main.transform.position);

        Destroy(gameObject);
    }
}
