using System.Collections;
using UnityEngine;
using TMPro;

public class Collecting : MonoBehaviour
{
    [SerializeField] AudioClip coinPickup;
    [SerializeField] int pointsForCoinPickup = 100;

    bool wasCollected = false;
    SpriteRenderer coinRenderer;
    Vector3 baseScale;
    TextMeshPro rewardPreview;
    GameSession session;
    int lastPreview = -1;

    public int BaseOreValue => pointsForCoinPickup;

    void Awake()
    {
        coinRenderer = GetComponent<SpriteRenderer>();
        ConfigureDenomination();
    }

    void Update()
    {
        RefreshRewardPreview();
        float pulseSpeed = pointsForCoinPickup >= 200 ? 4.5f : 3f;
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.04f;
        transform.localScale = baseScale * pulse;
    }

    void Start()
    {
        session = FindObjectOfType<GameSession>();
        var previewObject = new GameObject("OreRewardPreview");
        previewObject.transform.SetParent(transform, false);
        previewObject.transform.localPosition = Vector3.up * 0.6f;
        previewObject.transform.localScale = new Vector3(1f / baseScale.x, 1f / baseScale.y, 1f);
        rewardPreview = previewObject.AddComponent<TextMeshPro>();
        rewardPreview.fontSize = 2.5f;
        rewardPreview.alignment = TextAlignmentOptions.Center;
        if (coinRenderer) rewardPreview.sortingLayerID = coinRenderer.sortingLayerID;
        rewardPreview.sortingOrder = 100;
        rewardPreview.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
        RefreshRewardPreview();
    }

    void RefreshRewardPreview()
    {
        if (!session || !rewardPreview || wasCollected) return;
        int preview = session.Pressure.PreviewReward(pointsForCoinPickup, session.IsCriticalStability());
        if (preview == lastPreview) return;
        lastPreview = preview;
        rewardPreview.text = $"+{preview}";
    }

    void ConfigureDenomination()
    {
        float tierScale = pointsForCoinPickup >= 200 ? 2f :
            pointsForCoinPickup >= 150 ? 1.5f : 1f;
        baseScale = Vector3.one * tierScale;
        transform.localScale = baseScale;

        if (!coinRenderer) return;
        coinRenderer.color = pointsForCoinPickup >= 200
            ? new Color(0.45f, 0.90f, 1f)
            : pointsForCoinPickup >= 150
                ? new Color(1f, 0.56f, 0.18f)
                : new Color(1f, 0.92f, 0.38f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (wasCollected) return;
        if (!other.CompareTag("Player")) return;

        wasCollected = true;
        if (rewardPreview) rewardPreview.gameObject.SetActive(false);

        int awardedValue = pointsForCoinPickup;
        var gs = FindObjectOfType<GameSession>();
        if (gs) awardedValue = gs.AddCoins(pointsForCoinPickup);
        else Debug.LogWarning("GameSession not found: score not added.");

        if (coinPickup && Camera.main)
            AudioSource.PlayClipAtPoint(coinPickup, Camera.main.transform.position);

        ShowPickupValue(awardedValue);
        if (coinRenderer) coinRenderer.enabled = false;
        Collider2D pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider) pickupCollider.enabled = false;
        StartCoroutine(DestroyAfterFeedback());
    }

    void ShowPickupValue(int awardedValue)
    {
        GameObject feedbackObject = new GameObject("CoinValueFeedback");
        feedbackObject.transform.position = transform.position + Vector3.up * 0.5f;

        TextMeshPro feedback = feedbackObject.AddComponent<TextMeshPro>();
        feedback.text = $"+{awardedValue}";
        feedback.fontSize = pointsForCoinPickup >= 200 ? 5f : 4f;
        feedback.alignment = TextAlignmentOptions.Center;
        feedback.color = coinRenderer ? coinRenderer.color : Color.yellow;
        if (coinRenderer) feedback.sortingLayerID = coinRenderer.sortingLayerID;
        feedback.sortingOrder = 100;

        feedbackObject.AddComponent<CoinPickupFeedback>();
    }

    IEnumerator DestroyAfterFeedback()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        Destroy(gameObject);
    }
}
