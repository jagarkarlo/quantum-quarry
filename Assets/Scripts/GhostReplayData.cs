using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

[Serializable]
public struct GhostSampleFrame
{
    public int elapsedMs;
    public float x;
    public float y;
    public int facing;

    public GhostSampleFrame(int elapsedMs, float x, float y, int facing)
    {
        this.elapsedMs = Math.Max(0, elapsedMs);
        this.x = x;
        this.y = y;
        this.facing = facing >= 0 ? 1 : -1;
    }
}

[Serializable]
public sealed class GhostReplayData
{
    public const int MaxFrames = 1200;
    public const int SampleIntervalMs = 100;

    public string levelId = "level-1";
    public int totalElapsedMs;
    public List<GhostSampleFrame> frames = new List<GhostSampleFrame>();

    public void RecordSample(int elapsedMs, float x, float y, int facing)
    {
        if (frames.Count >= MaxFrames) return;
        int safeMs = Math.Max(0, elapsedMs);
        if (frames.Count > 0 && safeMs - frames[frames.Count - 1].elapsedMs < SampleIntervalMs)
            return;
        frames.Add(new GhostSampleFrame(safeMs, x, y, facing));
        totalElapsedMs = Math.Max(totalElapsedMs, safeMs);
    }

    public GhostSampleFrame EvaluateAt(int elapsedMs)
    {
        if (frames == null || frames.Count == 0)
            return new GhostSampleFrame(0, 0f, 0f, 1);
        if (elapsedMs <= frames[0].elapsedMs) return frames[0];
        int last = frames.Count - 1;
        if (elapsedMs >= frames[last].elapsedMs) return frames[last];

        for (int i = 0; i < last; i++)
        {
            GhostSampleFrame a = frames[i];
            GhostSampleFrame b = frames[i + 1];
            if (elapsedMs >= a.elapsedMs && elapsedMs <= b.elapsedMs)
            {
                int span = Math.Max(1, b.elapsedMs - a.elapsedMs);
                float t = (elapsedMs - a.elapsedMs) / (float)span;
                return new GhostSampleFrame(
                    elapsedMs,
                    a.x + (b.x - a.x) * t,
                    a.y + (b.y - a.y) * t,
                    b.facing);
            }
        }
        return frames[last];
    }

    public string ToCompactString()
    {
        var sb = new StringBuilder();
        sb.Append(levelId ?? "level-1").Append('|').Append(totalElapsedMs).Append('|');
        for (int i = 0; i < frames.Count; i++)
        {
            if (i > 0) sb.Append(';');
            GhostSampleFrame f = frames[i];
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:0.##},{2:0.##},{3}",
                f.elapsedMs, f.x, f.y, f.facing);
        }
        return sb.ToString();
    }

    public static GhostReplayData FromCompactString(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        string[] parts = data.Split('|');
        if (parts.Length < 3) return null;
        var replay = new GhostReplayData
        {
            levelId = parts[0],
            totalElapsedMs = int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int total) ? total : 0
        };
        if (!string.IsNullOrWhiteSpace(parts[2]))
        {
            string[] items = parts[2].Split(';');
            for (int i = 0; i < items.Length && replay.frames.Count < MaxFrames; i++)
            {
                string[] fields = items[i].Split(',');
                if (fields.Length < 4) continue;
                if (int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ms) &&
                    float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dir))
                {
                    replay.frames.Add(new GhostSampleFrame(ms, x, y, dir));
                }
            }
        }
        return replay;
    }
}
