using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class QuarryPressureHUD : MonoBehaviour
{
    GameSession session;
    TextMeshProUGUI status;
    TextMeshProUGUI notice;
    int previousTier;
    float noticeUntil;
    int displayedOre = -1;
    int displayedPending = -1;
    bool displayedCritical;

    public static void EnsureForScene(GameSession owner)
    {
        if (!SceneManager.GetActiveScene().name.StartsWith("Level ") || FindObjectOfType<QuarryPressureHUD>()) return;
        var root = new GameObject("QuarryPressureHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.matchWidthOrHeight = 0.5f;
        QuarryPressureHUD hud = root.AddComponent<QuarryPressureHUD>();
        hud.session = owner;
        hud.previousTier = owner.Pressure.Tier;
        hud.status = hud.CreateLabel("PressureStatus", 12f, 66f, 16f);
        hud.notice = hud.CreateLabel("PressureNotice", 82f, 28f, 18f);
        hud.notice.color = new Color(1f, 0.85f, 0.3f);
        owner.PressureChanged += hud.OnPressureChanged;
        hud.Refresh();
    }

    TextMeshProUGUI CreateLabel(string labelName, float bottom, float height, float fontSize)
    {
        var panel = new GameObject(labelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.offsetMin = new Vector2(16f, bottom);
        panelRect.offsetMax = new Vector2(-16f, bottom + height);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.04f, 0.06f, 0.07f, 0.86f);
        background.raycastTarget = false;
        var labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(panel.transform, false);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(8f, 2f);
        label.rectTransform.offsetMax = new Vector2(-8f, -2f);
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    void Update()
    {
        if (!session) return;
        if (displayedOre != session.Pressure.CarriedOre || displayedPending != session.Pressure.PendingCoins ||
            displayedCritical != session.IsCriticalStability()) Refresh();
        notice.transform.parent.gameObject.SetActive(Time.time < noticeUntil);
    }

    void OnPressureChanged()
    {
        int tier = session.Pressure.Tier;
        if (tier > previousTier)
        {
            notice.text = $"PRESSURE {tier}  |  {session.Pressure.ModifierName}";
            noticeUntil = Time.time + 3f;
        }
        else if (tier < previousTier) noticeUntil = 0f;
        previousTier = tier;
        Refresh();
    }

    void Refresh()
    {
        QuarryPressure pressure = session.Pressure;
        displayedOre = pressure.CarriedOre;
        displayedPending = pressure.PendingCoins;
        displayedCritical = session.IsCriticalStability();
        string next = pressure.NextThreshold > 0 ? $"Next {pressure.NextThreshold} ore" : "Maximum pressure";
        string pulses = pressure.HasHazardPulses ? "  |  Vents armed" : string.Empty;
        status.text = $"CARRIED {pressure.PendingCoins}  |  Pickup {pressure.RewardLabel(displayedCritical)}  |  Pressure {pressure.Tier}/3\n" +
            $"Ore {pressure.CarriedOre}  |  {next}  |  {pressure.ModifierName}{pulses}  |  Seed {pressure.Seed}";
    }

    void OnDestroy()
    {
        if (session) session.PressureChanged -= OnPressureChanged;
    }
}