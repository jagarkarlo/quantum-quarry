using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public sealed class BreakableOreCrate : MonoBehaviour
{
    [SerializeField, Min(1)] int baseOreValue = 250;

    bool isBroken;
    SpriteRenderer crateRenderer;
    TextMeshPro label;
    GameSession session;
    int lastPreview = -1;

    public int BaseOreValue => Mathf.Max(1, baseOreValue);
    public bool IsBroken => isBroken;

    void Awake()
    {
        crateRenderer = GetComponent<SpriteRenderer>();
        label = GetComponentInChildren<TextMeshPro>();
        GetComponent<Collider2D>().isTrigger = false;
    }

    void Start()
    {
        session = FindObjectOfType<GameSession>();
        RefreshPreview();
    }

    void Update()
    {
        RefreshPreview();
    }

    void RefreshPreview()
    {
        if (!session || !label || isBroken) return;
        int preview = session.Pressure.PreviewReward(BaseOreValue, session.IsCriticalStability());
        if (preview == lastPreview) return;
        lastPreview = preview;
        label.text = $"CRATE +{preview}";
    }

    public int Shatter()
    {
        if (isBroken) return 0;
        isBroken = true;
        if (label) label.gameObject.SetActive(false);

        if (!session) session = FindObjectOfType<GameSession>();
        int awarded = session ? session.AddCoins(BaseOreValue) : BaseOreValue;

        ShowShatterFeedback(awarded);
        if (crateRenderer) crateRenderer.enabled = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col) col.enabled = false;
        StartCoroutine(DestroyAfterFeedback());
        return awarded;
    }

    void ShowShatterFeedback(int awarded)
    {
        var feedbackObject = new GameObject("CrateOreFeedback");
        feedbackObject.transform.position = transform.position + Vector3.up * 0.65f;
        TextMeshPro feedback = feedbackObject.AddComponent<TextMeshPro>();
        feedback.text = $"CRATE +{awarded}";
        feedback.fontSize = 3.5f;
        feedback.alignment = TextAlignmentOptions.Center;
        feedback.color = new Color(1f, 0.82f, 0.35f);
        if (crateRenderer) feedback.sortingLayerID = crateRenderer.sortingLayerID;
        feedback.sortingOrder = 100;
        feedbackObject.AddComponent<CoinPickupFeedback>();
    }

    IEnumerator DestroyAfterFeedback()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        Destroy(gameObject);
    }
}
