using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GhostReplayRunner : MonoBehaviour
{
    GameSession session;
    PlayerMovement player;
    GhostReplayData activeRecording;
    GhostReplayData bestGhost;
    GameObject ghostAvatar;
    SpriteRenderer ghostRenderer;
    TextMeshProUGUI splitLabel;
    string boundLevelId;

    public GhostReplayData ActiveRecording => activeRecording;
    public GhostReplayData BestGhost => bestGhost;

    public static void EnsureForScene(GameSession owner)
    {
        if (!GameSession.IsGameplayScene(SceneManager.GetActiveScene().name) || FindObjectOfType<GhostReplayRunner>())
            return;
        var go = new GameObject("GhostReplayRunner");
        var runner = go.AddComponent<GhostReplayRunner>();
        runner.session = owner;
    }

    void Start()
    {
        if (!session) session = FindObjectOfType<GameSession>();
        QuarrySettings.ApplyRuntimeAudio();
        RebindLevel();
        BuildSplitsBadge();
    }

    void RebindLevel()
    {
        boundLevelId = session && !string.IsNullOrEmpty(session.ActiveLevelId)
            ? session.ActiveLevelId
            : LevelMedals.NormalizeLevelId(SceneManager.GetActiveScene().name) ?? "level-1";

        activeRecording = new GhostReplayData { levelId = boundLevelId };
        bestGhost = QuarryProfileManager.LoadBestGhost(boundLevelId);
        player = FindObjectOfType<PlayerMovement>();

        if (ghostAvatar) Destroy(ghostAvatar);
        if (bestGhost != null && bestGhost.frames != null && bestGhost.frames.Count > 0 && player)
        {
            ghostAvatar = new GameObject("SpeedrunGhostAvatar");
            ghostRenderer = ghostAvatar.AddComponent<SpriteRenderer>();
            SpriteRenderer playerSr = player.GetComponent<SpriteRenderer>();
            if (playerSr)
            {
                ghostRenderer.sprite = playerSr.sprite;
                ghostRenderer.sortingLayerID = playerSr.sortingLayerID;
                ghostRenderer.sortingOrder = playerSr.sortingOrder - 1;
            }
            ghostRenderer.color = new Color(0.45f, 0.92f, 1f, 0.42f);

            var tagObj = new GameObject("GhostLabel");
            tagObj.transform.SetParent(ghostAvatar.transform, false);
            tagObj.transform.localPosition = Vector3.up * 0.85f;
            TextMeshPro tmp = tagObj.AddComponent<TextMeshPro>();
            tmp.text = "GHOST";
            tmp.fontSize = 2.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.45f, 0.92f, 1f, 0.75f);
            if (ghostRenderer) tmp.sortingLayerID = ghostRenderer.sortingLayerID;
            tmp.sortingOrder = 95;
        }
    }

    void BuildSplitsBadge()
    {
        var canvasObj = new GameObject("SpeedrunSplitsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 49;
        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("SplitsPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObj.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-142f, -18f);
        rect.sizeDelta = new Vector2(210f, 46f);
        Image bg = panel.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.08f, 0.12f, 0.82f);
        bg.raycastTarget = false;

        var labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(panel.transform, false);
        splitLabel = labelObj.GetComponent<TextMeshProUGUI>();
        splitLabel.rectTransform.anchorMin = Vector2.zero;
        splitLabel.rectTransform.anchorMax = Vector2.one;
        splitLabel.rectTransform.offsetMin = new Vector2(6f, 2f);
        splitLabel.rectTransform.offsetMax = new Vector2(-6f, -2f);
        splitLabel.alignment = TextAlignmentOptions.Center;
        splitLabel.enableAutoSizing = true;
        splitLabel.fontSizeMin = 9f;
        splitLabel.fontSizeMax = 14f;
        splitLabel.color = new Color(0.78f, 0.95f, 1f);
        splitLabel.raycastTarget = false;
    }

    void Update()
    {
        if (!session) return;
        if (!player) player = FindObjectOfType<PlayerMovement>();

        int elapsedMs = session.GetLevelElapsedMs();
        if (player && player.IsAlive && Time.timeScale > 0f && activeRecording != null)
        {
            Vector3 p = player.transform.position;
            int facing = player.transform.localScale.x >= 0f ? 1 : -1;
            activeRecording.RecordSample(elapsedMs, p.x, p.y, facing);
        }

        if (ghostAvatar && bestGhost != null)
        {
            GhostSampleFrame frame = bestGhost.EvaluateAt(elapsedMs);
            ghostAvatar.transform.position = new Vector3(frame.x, frame.y, 0f);
            ghostAvatar.transform.localScale = new Vector3(frame.facing, 1f, 1f);
        }

        if (splitLabel)
        {
            bool show = QuarrySettings.ShowSpeedrunSplits && !session.IsPlaytestMode;
            splitLabel.transform.parent.gameObject.SetActive(show);
            if (show)
            {
                LevelPersonalBest pb = GameSession.GetPersonalBest(boundLevelId);
                string cur = LevelMedals.FormatDuration(elapsedMs);
                if (pb.Completed && pb.BestTimeMs < int.MaxValue)
                {
                    int deltaMs = elapsedMs - pb.BestTimeMs;
                    string sign = deltaMs <= 0 ? "-" : "+";
                    string deltaStr = LevelMedals.FormatDuration(Mathf.Abs(deltaMs));
                    splitLabel.text = $"TIME {cur}\nPB {LevelMedals.FormatDuration(pb.BestTimeMs)} ({sign}{deltaStr})";
                }
                else
                {
                    splitLabel.text = $"TIME {cur}\nPB --";
                }
            }
        }
    }

    public void CommitRunIfCompleted()
    {
        if (!session || session.IsPlaytestMode || activeRecording == null) return;
        activeRecording.totalElapsedMs = session.GetLevelElapsedMs();
        if (player)
        {
            Vector3 p = player.transform.position;
            activeRecording.RecordSample(activeRecording.totalElapsedMs, p.x, p.y,
                player.transform.localScale.x >= 0f ? 1 : -1);
        }
        QuarryProfileManager.SaveGhostIfFaster(activeRecording);
    }
}
