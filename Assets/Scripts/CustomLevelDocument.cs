using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

[Serializable]
public struct CustomLevelCell
{
    public int x;
    public int y;
    public string id;

    public CustomLevelCell(int x, int y, string id)
    {
        this.x = x;
        this.y = y;
        this.id = id ?? string.Empty;
    }
}

[Serializable]
public sealed class CustomLevelDocument
{
    public const int CurrentFormatVersion = 1;
    public const int MaxWidth = 64;
    public const int MaxHeight = 32;
    public const int MaxObjects = 256;

    public static readonly string[] AllowedTileIds = { "ground", "hazard_spike", "lava" };
    public static readonly string[] AllowedObjectIds =
    {
        "coin_100", "coin_150", "crate_250", "bank", "vent", "gate_t1", "enemy", "stabilization"
    };

    public int formatVersion = CurrentFormatVersion;
    public string levelId = "custom-draft-1";
    public string title = "Custom Quarry Draft";
    public int width = 32;
    public int height = 18;
    public int spawnX = 2;
    public int spawnY = 2;
    public int exitX = 20;
    public int exitY = 2;
    public List<CustomLevelCell> tiles = new List<CustomLevelCell>();
    public List<CustomLevelCell> objects = new List<CustomLevelCell>();

    public static CustomLevelDocument CreateStarterDraft()
    {
        var doc = new CustomLevelDocument
        {
            formatVersion = CurrentFormatVersion,
            levelId = "custom-draft-1",
            title = "Quarry Creator Draft",
            width = 32,
            height = 18,
            spawnX = 2,
            spawnY = 2,
            exitX = 22,
            exitY = 2
        };

        for (int x = 0; x <= 25; x++) doc.SetTile(x, 1, "ground");
        for (int x = 7; x <= 11; x++) doc.SetTile(x, 4, "ground");
        for (int x = 14; x <= 18; x++) doc.SetTile(x, 6, "ground");

        doc.SetObject(5, 2, "coin_100");
        doc.SetObject(8, 5, "coin_100");
        doc.SetObject(10, 5, "coin_150");
        doc.SetObject(12, 2, "bank");
        doc.SetObject(15, 7, "gate_t1");
        doc.SetObject(17, 7, "crate_250");
        doc.SetObject(19, 2, "vent");
        return doc;
    }

    public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    public static bool IsAllowedTileId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        for (int i = 0; i < AllowedTileIds.Length; i++)
            if (AllowedTileIds[i] == id) return true;
        return false;
    }

    public static bool IsAllowedObjectId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        for (int i = 0; i < AllowedObjectIds.Length; i++)
            if (AllowedObjectIds[i] == id) return true;
        return false;
    }

    public string GetTile(int x, int y)
    {
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i].x == x && tiles[i].y == y) return tiles[i].id;
        return null;
    }

    public string GetObject(int x, int y)
    {
        for (int i = 0; i < objects.Count; i++)
            if (objects[i].x == x && objects[i].y == y) return objects[i].id;
        return null;
    }

    public bool SetTile(int x, int y, string id)
    {
        if (!InBounds(x, y)) return false;
        if (string.IsNullOrEmpty(id))
        {
            return RemoveAt(tiles, x, y);
        }
        if (!IsAllowedTileId(id)) return false;
        RemoveAt(objects, x, y);
        Upsert(tiles, x, y, id);
        return true;
    }

    public bool SetObject(int x, int y, string id)
    {
        if (!InBounds(x, y)) return false;
        if (string.IsNullOrEmpty(id))
        {
            return RemoveAt(objects, x, y);
        }
        if (!IsAllowedObjectId(id)) return false;
        if ((x == spawnX && y == spawnY) || (x == exitX && y == exitY)) return false;
        RemoveAt(tiles, x, y);
        if (GetObject(x, y) == null && objects.Count >= MaxObjects) return false;
        Upsert(objects, x, y, id);
        return true;
    }

    public bool SetSpawn(int x, int y)
    {
        if (!InBounds(x, y) || (x == exitX && y == exitY)) return false;
        RemoveAt(tiles, x, y);
        RemoveAt(objects, x, y);
        spawnX = x;
        spawnY = y;
        return true;
    }

    public bool SetExit(int x, int y)
    {
        if (!InBounds(x, y) || (x == spawnX && y == spawnY)) return false;
        RemoveAt(tiles, x, y);
        RemoveAt(objects, x, y);
        exitX = x;
        exitY = y;
        return true;
    }

    public bool ClearCell(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        bool removedTile = RemoveAt(tiles, x, y);
        bool removedObj = RemoveAt(objects, x, y);
        return removedTile || removedObj;
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (formatVersion != CurrentFormatVersion)
            errors.Add($"Unsupported formatVersion {formatVersion} (expected {CurrentFormatVersion}).");
        if (string.IsNullOrWhiteSpace(levelId))
            errors.Add("levelId is required.");
        if (width < 8 || width > MaxWidth || height < 6 || height > MaxHeight)
            errors.Add($"Grid dimensions {width}x{height} are outside allowed bounds (8..{MaxWidth} x 6..{MaxHeight}).");
        if (!InBounds(spawnX, spawnY))
            errors.Add($"Spawn ({spawnX},{spawnY}) is out of bounds.");
        if (!InBounds(exitX, exitY))
            errors.Add($"Exit ({exitX},{exitY}) is out of bounds.");
        if (spawnX == exitX && spawnY == exitY)
            errors.Add("Spawn and Exit cannot occupy the same cell.");
        if (tiles == null || tiles.Count == 0)
            errors.Add("Level requires at least one terrain tile.");
        if (objects != null && objects.Count > MaxObjects)
            errors.Add($"Object count {objects.Count} exceeds maximum {MaxObjects}.");

        var occupied = new HashSet<long>();
        if (tiles != null)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                CustomLevelCell cell = tiles[i];
                if (!InBounds(cell.x, cell.y))
                    errors.Add($"Tile at ({cell.x},{cell.y}) is out of bounds.");
                if (!IsAllowedTileId(cell.id))
                    errors.Add($"Tile ID '{cell.id}' is not allowlisted.");
                long key = ((long)cell.x << 32) | (uint)cell.y;
                if (!occupied.Add(key))
                    errors.Add($"Duplicate cell entry at ({cell.x},{cell.y}).");
            }
        }

        if (objects != null)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                CustomLevelCell cell = objects[i];
                if (!InBounds(cell.x, cell.y))
                    errors.Add($"Object at ({cell.x},{cell.y}) is out of bounds.");
                if (!IsAllowedObjectId(cell.id))
                    errors.Add($"Object ID '{cell.id}' is not allowlisted.");
                long key = ((long)cell.x << 32) | (uint)cell.y;
                if (!occupied.Add(key))
                    errors.Add($"Cell ({cell.x},{cell.y}) has overlapping tile or object entries.");
            }
        }

        return errors;
    }

    public string ToJson()
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"formatVersion\":{0},", formatVersion);
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"levelId\":\"{0}\",", Escape(levelId));
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"title\":\"{0}\",", Escape(title));
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"width\":{0},\"height\":{1},", width, height);
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"spawnX\":{0},\"spawnY\":{1},", spawnX, spawnY);
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"exitX\":{0},\"exitY\":{1},", exitX, exitY);
        AppendCells(sb, "tiles", tiles);
        sb.Append(',');
        AppendCells(sb, "objects", objects);
        sb.Append('}');
        return sb.ToString();
    }

    public static CustomLevelDocument FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Level JSON cannot be empty.");
        if (json.Length > 131072)
            throw new ArgumentException("Level JSON exceeds the 128 KB safety limit.");

        var doc = new CustomLevelDocument
        {
            formatVersion = ReadInt(json, "formatVersion", CurrentFormatVersion),
            levelId = ReadString(json, "levelId", "custom-draft-1"),
            title = ReadString(json, "title", "Custom Quarry Draft"),
            width = ReadInt(json, "width", 32),
            height = ReadInt(json, "height", 18),
            spawnX = ReadInt(json, "spawnX", 2),
            spawnY = ReadInt(json, "spawnY", 2),
            exitX = ReadInt(json, "exitX", 20),
            exitY = ReadInt(json, "exitY", 2),
            tiles = ReadCells(json, "tiles"),
            objects = ReadCells(json, "objects")
        };
        return doc;
    }

    public CustomLevelDocument Clone() => FromJson(ToJson());

    static void Upsert(List<CustomLevelCell> list, int x, int y, string id)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].x == x && list[i].y == y)
            {
                list[i] = new CustomLevelCell(x, y, id);
                return;
            }
        }
        list.Add(new CustomLevelCell(x, y, id));
    }

    static bool RemoveAt(List<CustomLevelCell> list, int x, int y)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].x == x && list[i].y == y)
            {
                list.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    static void AppendCells(StringBuilder sb, string field, List<CustomLevelCell> list)
    {
        sb.Append('"').Append(field).Append("\":[");
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{{\"x\":{0},\"y\":{1},\"id\":\"{2}\"}}",
                    list[i].x, list[i].y, Escape(list[i].id));
            }
        }
        sb.Append(']');
    }

    static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    static int ReadInt(string json, string key, int fallback)
    {
        string token = "\"" + key + "\":";
        int idx = json.IndexOf(token, StringComparison.Ordinal);
        if (idx < 0) return fallback;
        idx += token.Length;
        while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
        int start = idx;
        if (idx < json.Length && (json[idx] == '-' || json[idx] == '+')) idx++;
        while (idx < json.Length && char.IsDigit(json[idx])) idx++;
        return int.TryParse(json.Substring(start, idx - start), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
    }

    static string ReadString(string json, string key, string fallback)
    {
        string token = "\"" + key + "\":\"";
        int idx = json.IndexOf(token, StringComparison.Ordinal);
        if (idx < 0) return fallback;
        idx += token.Length;
        var sb = new StringBuilder();
        while (idx < json.Length && json[idx] != '"')
        {
            if (json[idx] == '\\' && idx + 1 < json.Length) idx++;
            sb.Append(json[idx]);
            idx++;
        }
        return sb.ToString();
    }

    static List<CustomLevelCell> ReadCells(string json, string key)
    {
        var result = new List<CustomLevelCell>();
        string token = "\"" + key + "\":[";
        int idx = json.IndexOf(token, StringComparison.Ordinal);
        if (idx < 0) return result;
        idx += token.Length;
        int end = json.IndexOf(']', idx);
        if (end < 0) return result;
        string arrayBody = json.Substring(idx, end - idx);
        int pos = 0;
        while (pos < arrayBody.Length)
        {
            int open = arrayBody.IndexOf('{', pos);
            if (open < 0) break;
            int close = arrayBody.IndexOf('}', open);
            if (close < 0) break;
            string item = arrayBody.Substring(open, close - open + 1);
            result.Add(new CustomLevelCell(
                ReadInt(item, "x", 0),
                ReadInt(item, "y", 0),
                ReadString(item, "id", string.Empty)));
            pos = close + 1;
        }
        return result;
    }
}
