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
    public float exactX;
    public float exactY;
    public float spanX;
    public float spanY;

    public CustomLevelCell(int x, int y, string id, float exactX = float.NaN, float exactY = float.NaN,
        float spanX = 0f, float spanY = 0f)
    {
        this.x = x;
        this.y = y;
        this.id = id ?? string.Empty;
        this.exactX = float.IsNaN(exactX) ? x : exactX;
        this.exactY = float.IsNaN(exactY) ? y : exactY;
        this.spanX = spanX;
        this.spanY = spanY;
    }
}

[Serializable]
public sealed class CustomLevelDocument
{
    public const int CurrentFormatVersion = 1;
    public const int MaxWidth = 256;
    public const int MaxHeight = 128;
    public const int MinCoord = -128;
    public const int MaxCoord = 256;
    public const int MaxObjects = 512;

    public static readonly string[] AllowedTileIds =
    {
        "ground", "rock", "ladder", "bounce", "hazard_spike", "water_surface", "water", "lava_surface", "lava", "bg"
    };

    public static readonly string[] AllowedObjectIds =
    {
        "coin_100", "coin_150", "coin_200", "crate_250", "bank", "vent",
        "gate_t1", "gate_t2", "enemy", "red_enemy", "shielded_enemy", "sentry",
        "platform", "jump_pad", "stabilization"
    };

    public int formatVersion = CurrentFormatVersion;
    public string levelId = "custom-draft-1";
    public string title = "Custom Quarry Draft";
    public int sourceCampaignLevel = 4;
    public int width = 64;
    public int height = 36;
    public int spawnX = 2;
    public int spawnY = 2;
    public float exactSpawnX = 2f;
    public float exactSpawnY = 2f;
    public int exitX = 20;
    public int exitY = 2;
    public float exactExitX = 20f;
    public float exactExitY = 2f;
    public List<CustomLevelCell> tiles = new List<CustomLevelCell>();
    public List<CustomLevelCell> objects = new List<CustomLevelCell>();

    public static CustomLevelDocument CreateStarterDraft()
    {
        var doc = new CustomLevelDocument
        {
            formatVersion = CurrentFormatVersion,
            levelId = "custom-draft-1",
            title = "Quarry Creator Draft",
            sourceCampaignLevel = 4,
            width = 48,
            height = 24,
            spawnX = 2,
            spawnY = 2,
            exactSpawnX = 2f,
            exactSpawnY = 2f,
            exitX = 24,
            exitY = 2,
            exactExitX = 24f,
            exactExitY = 2f
        };

        for (int x = 0; x <= 26; x++) doc.SetTile(x, 1, "ground");
        for (int y = 2; y <= 5; y++) doc.SetTile(6, y, "ladder");
        for (int x = 7; x <= 12; x++) doc.SetTile(x, 5, "ground");
        doc.SetTile(13, 2, "bounce");
        for (int x = 15; x <= 20; x++) doc.SetTile(x, 6, "ground");

        doc.SetObject(4, 2, "coin_100");
        doc.SetObject(8, 6, "coin_150");
        doc.SetObject(10, 6, "red_enemy");
        doc.SetObject(12, 2, "bank");
        doc.SetObject(16, 7, "gate_t1");
        doc.SetObject(18, 7, "crate_250");
        doc.SetObject(20, 6, "stabilization");
        doc.SetObject(21, 2, "vent");
        return doc;
    }

    public bool InBounds(int x, int y) => x >= MinCoord && x <= MaxCoord && y >= MinCoord && y <= MaxCoord;

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

    public static bool IsBackgroundTileId(string id) => id == "bg";

    public static bool RequiresGroundSupport(string objectId)
    {
        return objectId == "enemy" || objectId == "red_enemy" || objectId == "shielded_enemy" ||
               objectId == "sentry" || objectId == "jump_pad" || objectId == "crate_250" ||
               objectId == "bank" || objectId == "vent" || objectId == "gate_t1" || objectId == "gate_t2";
    }

    public bool IsSolidSupportTile(int x, int y)
    {
        string t = GetTile(x, y);
        return t == "ground" || t == "rock" || t == "bounce";
    }

    public bool TryResolveGroundedCell(int x, int y, string objectId, out int resolvedY, out float supportTopY)
    {
        resolvedY = y;
        supportTopY = y - 0.5f;
        if (!RequiresGroundSupport(objectId)) return true;

        if (IsSolidSupportTile(x, y)) return false;
        for (int scanY = y; scanY >= y - 10 && scanY >= MinCoord; scanY--)
        {
            if (IsSolidSupportTile(x, scanY - 1) && !IsSolidSupportTile(x, scanY))
            {
                resolvedY = scanY;
                supportTopY = scanY - 0.5f;
                return true;
            }
        }
        return false;
    }

    public string GetTile(int x, int y)
    {
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i].x == x && tiles[i].y == y && !IsBackgroundTileId(tiles[i].id)) return tiles[i].id;
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
        if (!IsBackgroundTileId(id))
        {
            RemoveForegroundTilesAt(x, y);
        }
        else
        {
            RemoveBackgroundTileAt(x, y);
        }
        tiles.Add(new CustomLevelCell(x, y, id));
        ReconcileGroundedObjectsInColumn(x);
        return true;
    }

    public void AddCapturedObject(int x, int y, string id, float exactX, float exactY,
        float spanX = 0f, float spanY = 0f)
    {
        if (!InBounds(x, y) || !IsAllowedObjectId(id) || objects.Count >= MaxObjects) return;
        objects.Add(new CustomLevelCell(x, y, id, exactX, exactY, spanX, spanY));
    }

    public int ReconcileGroundedObjectsInColumn(int x)
    {
        int adjusted = 0;
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            CustomLevelCell obj = objects[i];
            if (obj.x != x) continue;
            if (IsSolidSupportTile(obj.x, obj.y))
            {
                if (RequiresGroundSupport(obj.id) &&
                    TryResolveGroundedCell(obj.x, obj.y + 1, obj.id, out int upY, out float upTop))
                {
                    float ey = (obj.id == "gate_t1" || obj.id == "gate_t2") ? upTop + 1f : upTop + 0.5f;
                    objects[i] = new CustomLevelCell(obj.x, upY, obj.id, obj.exactX, ey, obj.spanX, obj.spanY);
                }
                else
                {
                    objects.RemoveAt(i);
                }
                adjusted++;
                continue;
            }
            if (!RequiresGroundSupport(obj.id)) continue;
            if (!IsSolidSupportTile(obj.x, obj.y - 1))
            {
                if (TryResolveGroundedCell(obj.x, obj.y, obj.id, out int groundedY, out float supportTopY))
                {
                    float ey = (obj.id == "gate_t1" || obj.id == "gate_t2") ? supportTopY + 1f : supportTopY + 0.5f;
                    objects[i] = new CustomLevelCell(obj.x, groundedY, obj.id, obj.exactX, ey, obj.spanX, obj.spanY);
                }
                else
                {
                    objects.RemoveAt(i);
                }
                adjusted++;
            }
        }
        return adjusted;
    }

    public bool SetObject(int x, int y, string id, float exactX = float.NaN, float exactY = float.NaN,
        float spanX = 0f, float spanY = 0f)
    {
        if (!InBounds(x, y)) return false;
        if (string.IsNullOrEmpty(id))
        {
            return RemoveAt(objects, x, y);
        }
        if (!IsAllowedObjectId(id)) return false;
        if ((x == spawnX && y == spawnY) || (x == exitX && y == exitY)) return false;
        if (GetObject(x, y) == null && objects.Count >= MaxObjects) return false;
        float ex = float.IsNaN(exactX) ? x : exactX;
        float ey = float.IsNaN(exactY) ? y : exactY;
        float sx = spanX;
        float sy = spanY;
        if (id == "platform" && Math.Abs(sx) < 0.01f && Math.Abs(sy) < 0.01f) sx = 6f;
        Upsert(objects, new CustomLevelCell(x, y, id, ex, ey, sx, sy));
        return true;
    }

    public bool SetSpawn(int x, int y, float exactX = float.NaN, float exactY = float.NaN)
    {
        if (!InBounds(x, y) || (x == exitX && y == exitY)) return false;
        RemoveForegroundTilesAt(x, y);
        RemoveAt(objects, x, y);
        spawnX = x;
        spawnY = y;
        exactSpawnX = float.IsNaN(exactX) ? x : exactX;
        exactSpawnY = float.IsNaN(exactY) ? y : exactY;
        return true;
    }

    public bool SetExit(int x, int y, float exactX = float.NaN, float exactY = float.NaN)
    {
        if (!InBounds(x, y) || (x == spawnX && y == spawnY)) return false;
        RemoveForegroundTilesAt(x, y);
        RemoveAt(objects, x, y);
        exitX = x;
        exitY = y;
        exactExitX = float.IsNaN(exactX) ? x : exactX;
        exactExitY = float.IsNaN(exactY) ? y : exactY;
        return true;
    }

    public bool ClearCell(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        bool removedTile = RemoveAt(tiles, x, y);
        bool removedObj = RemoveAt(objects, x, y);
        if (removedTile) ReconcileGroundedObjectsInColumn(x);
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

        if (tiles != null)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                CustomLevelCell cell = tiles[i];
                if (!InBounds(cell.x, cell.y))
                    errors.Add($"Tile at ({cell.x},{cell.y}) is out of bounds.");
                if (!IsAllowedTileId(cell.id))
                    errors.Add($"Tile ID '{cell.id}' is not allowlisted.");
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
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"sourceCampaignLevel\":{0},", sourceCampaignLevel);
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"width\":{0},\"height\":{1},", width, height);
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"spawnX\":{0},\"spawnY\":{1},", spawnX, spawnY);
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"exactSpawnX\":{0:R},\"exactSpawnY\":{1:R},", exactSpawnX, exactSpawnY);
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"exitX\":{0},\"exitY\":{1},", exitX, exitY);
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"exactExitX\":{0:R},\"exactExitY\":{1:R},", exactExitX, exactExitY);
        AppendCells(sb, "tiles", tiles, false);
        sb.Append(',');
        AppendCells(sb, "objects", objects, true);
        sb.Append('}');
        return sb.ToString();
    }

    public static CustomLevelDocument FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Level JSON cannot be empty.");
        if (json.Length > 262144)
            throw new ArgumentException("Level JSON exceeds the 256 KB safety limit.");

        int sx = ReadInt(json, "spawnX", 2);
        int sy = ReadInt(json, "spawnY", 2);
        int ex = ReadInt(json, "exitX", 20);
        int ey = ReadInt(json, "exitY", 2);
        return new CustomLevelDocument
        {
            formatVersion = ReadInt(json, "formatVersion", CurrentFormatVersion),
            levelId = ReadString(json, "levelId", "custom-draft-1"),
            title = ReadString(json, "title", "Custom Quarry Draft"),
            sourceCampaignLevel = ReadInt(json, "sourceCampaignLevel", 4),
            width = ReadInt(json, "width", 64),
            height = ReadInt(json, "height", 36),
            spawnX = sx,
            spawnY = sy,
            exactSpawnX = ReadFloat(json, "exactSpawnX", sx),
            exactSpawnY = ReadFloat(json, "exactSpawnY", sy),
            exitX = ex,
            exitY = ey,
            exactExitX = ReadFloat(json, "exactExitX", ex),
            exactExitY = ReadFloat(json, "exactExitY", ey),
            tiles = ReadCells(json, "tiles"),
            objects = ReadCells(json, "objects")
        };
    }

    public CustomLevelDocument Clone() => FromJson(ToJson());

    void RemoveForegroundTilesAt(int x, int y)
    {
        for (int i = tiles.Count - 1; i >= 0; i--)
            if (tiles[i].x == x && tiles[i].y == y && !IsBackgroundTileId(tiles[i].id))
                tiles.RemoveAt(i);
    }

    void RemoveBackgroundTileAt(int x, int y)
    {
        for (int i = tiles.Count - 1; i >= 0; i--)
            if (tiles[i].x == x && tiles[i].y == y && IsBackgroundTileId(tiles[i].id))
                tiles.RemoveAt(i);
    }

    static void Upsert(List<CustomLevelCell> list, CustomLevelCell cell)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].x == cell.x && list[i].y == cell.y)
            {
                list[i] = cell;
                return;
            }
        }
        list.Add(cell);
    }

    static bool RemoveAt(List<CustomLevelCell> list, int x, int y)
    {
        bool removed = false;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].x == x && list[i].y == y)
            {
                list.RemoveAt(i);
                removed = true;
            }
        }
        return removed;
    }

    static void AppendCells(StringBuilder sb, string field, List<CustomLevelCell> list, bool includeExactAndSpan)
    {
        sb.Append('"').Append(field).Append("\":[");
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                CustomLevelCell c = list[i];
                if (includeExactAndSpan)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "{{\"x\":{0},\"y\":{1},\"id\":\"{2}\",\"ex\":{3:R},\"ey\":{4:R},\"sx\":{5:R},\"sy\":{6:R}}}",
                        c.x, c.y, Escape(c.id), c.exactX, c.exactY, c.spanX, c.spanY);
                }
                else
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "{{\"x\":{0},\"y\":{1},\"id\":\"{2}\"}}",
                        c.x, c.y, Escape(c.id));
                }
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

    static float ReadFloat(string json, string key, float fallback)
    {
        string token = "\"" + key + "\":";
        int idx = json.IndexOf(token, StringComparison.Ordinal);
        if (idx < 0) return fallback;
        idx += token.Length;
        while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
        int start = idx;
        if (idx < json.Length && (json[idx] == '-' || json[idx] == '+')) idx++;
        while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '.' || json[idx] == 'e' || json[idx] == 'E' || json[idx] == '-' || json[idx] == '+')) idx++;
        return float.TryParse(json.Substring(start, idx - start), NumberStyles.Float,
            CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
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
            int cx = ReadInt(item, "x", 0);
            int cy = ReadInt(item, "y", 0);
            result.Add(new CustomLevelCell(
                cx,
                cy,
                ReadString(item, "id", string.Empty),
                ReadFloat(item, "ex", cx),
                ReadFloat(item, "ey", cy),
                ReadFloat(item, "sx", 0f),
                ReadFloat(item, "sy", 0f)));
            pos = close + 1;
        }
        return result;
    }
}
