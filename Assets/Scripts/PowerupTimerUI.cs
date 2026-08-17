using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PowerupTimerUI : MonoBehaviour
{
    [SerializeField] Image fill; // assign a Filled Radial Image
    Coroutine running;

    void Awake()
    {
        if (!fill) fill = GetComponent<Image>();
        if (!fill)
        {
            Debug.LogWarning($"{name}: PowerupTimerUI has no Image assigned.");
            return;
        }

        // Ensure the Image can render a filled shape
        if (fill.type != Image.Type.Filled)
            Debug.LogWarning($"{name}: Image.type should be 'Filled'. Current: {fill.type}");

        if (fill.sprite == null)
            Debug.LogWarning($"{name}: Image has no Sprite set (e.g. Knob/UISprite).");

        fill.raycastTarget = false; // avoid blocking clicks
        Hide();
    }

    public void StartTimer(float seconds)
    {
        if (running != null) StopCoroutine(running);
        if (fill) { fill.fillAmount = 1f; fill.enabled = true; } // start fully visible
        running = StartCoroutine(RunTimer(seconds));
    }

    public void StopTimer()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        Hide();
    }

    IEnumerator RunTimer(float seconds)
    {
        Show();
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;                 // unaffected by pause
            if (fill) fill.fillAmount = 1f - Mathf.Clamp01(t / seconds);
            yield return null;
        }
        Hide();
        running = null;
    }

    void Show() { if (fill) fill.enabled = true; }
    void Hide() { if (fill) fill.enabled = false; }
}
