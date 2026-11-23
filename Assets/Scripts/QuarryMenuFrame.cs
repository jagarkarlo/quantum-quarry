using UnityEngine;
using UnityEngine.UI;

public static class QuarryMenuFrame
{
    // Keep the authored menu composition intact. Extra window space is a matte,
    // not extra distance between the heading, buttons and footer.
    public static RectTransform Ensure(Canvas canvas)
    {
        const string frameName = "MenuFrame";
        var existing = canvas.transform.Find(frameName) as RectTransform;
        if (existing) return existing;

        var scaler = canvas.GetComponent<CanvasScaler>();
        var frame = new GameObject(frameName, typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(canvas.transform, false);
        frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.one * 0.5f;
        frame.sizeDelta = scaler.referenceResolution;
        // Move from the front, preserving the original draw and navigation order.
        while (canvas.transform.GetChild(0) != frame)
            canvas.transform.GetChild(0).SetParent(frame, false);

        var matte = new GameObject("MenuMatte", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        matte.transform.SetParent(canvas.transform, false);
        matte.transform.SetAsFirstSibling();
        matte.rectTransform.anchorMin = Vector2.zero;
        matte.rectTransform.anchorMax = Vector2.one;
        matte.rectTransform.offsetMin = matte.rectTransform.offsetMax = Vector2.zero;
        matte.color = new Color(0.035f, 0.045f, 0.065f);
        matte.raycastTarget = false;
        return frame;
    }
}
