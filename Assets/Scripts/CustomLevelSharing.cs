using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

[Serializable]
public readonly struct SharedLevelMetadata
{
    public readonly string FilePath;
    public readonly string LevelId;
    public readonly string Title;
    public readonly uint Checksum;
    public readonly bool IsFavorite;

    public SharedLevelMetadata(string filePath, string levelId, string title, uint checksum, bool isFavorite)
    {
        FilePath = filePath ?? string.Empty;
        LevelId = levelId ?? "custom-draft-1";
        Title = title ?? "Custom Quarry Draft";
        Checksum = checksum;
        IsFavorite = isFavorite;
    }
}

public static class CustomLevelSharing
{
    public const string FileExtension = ".qqlevel";
    public const string EnvelopePrefix = "QQLEVEL1|";
    public const int MaxImportChars = 262144;

    static readonly HashSet<string> sessionFavorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static uint ComputeFnv1aChecksum(string payload)
    {
        uint hash = 2166136261u;
        if (string.IsNullOrEmpty(payload)) return hash;
        for (int i = 0; i < payload.Length; i++)
        {
            hash ^= payload[i];
            hash = unchecked(hash * 16777619u);
        }
        return hash;
    }

    public static string PackShareableString(CustomLevelDocument doc)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        List<string> errors = doc.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException($"Cannot export invalid level: {errors[0]}");
        string json = doc.ToJson();
        uint crc = ComputeFnv1aChecksum(json);
        return string.Format(CultureInfo.InvariantCulture, "{0}{1:X8}|{2}", EnvelopePrefix, crc, json);
    }

    public static bool TryUnpackShareableString(string rawText, out CustomLevelDocument doc, out string error)
    {
        doc = null;
        error = null;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            error = "Empty level package.";
            return false;
        }
        if (rawText.Length > MaxImportChars)
        {
            error = "Level file exceeds maximum 256 KB size limit.";
            return false;
        }
        string trimmed = rawText.Trim();
        if (!trimmed.StartsWith(EnvelopePrefix, StringComparison.Ordinal))
        {
            error = "Missing QQLEVEL1 header.";
            return false;
        }
        int secondPipe = trimmed.IndexOf('|', EnvelopePrefix.Length);
        if (secondPipe < 0)
        {
            error = "Malformed checksum header.";
            return false;
        }
        string hexChecksum = trimmed.Substring(EnvelopePrefix.Length, secondPipe - EnvelopePrefix.Length);
        string jsonPayload = trimmed.Substring(secondPipe + 1);
        if (!uint.TryParse(hexChecksum, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint expectedCrc))
        {
            error = "Invalid checksum hex format.";
            return false;
        }
        uint actualCrc = ComputeFnv1aChecksum(jsonPayload);
        if (actualCrc != expectedCrc)
        {
            error = $"Checksum mismatch (expected {expectedCrc:X8}, got {actualCrc:X8}).";
            return false;
        }
        try
        {
            CustomLevelDocument parsed = CustomLevelDocument.FromJson(jsonPayload);
            List<string> validationErrors = parsed.Validate();
            if (validationErrors.Count > 0)
            {
                error = validationErrors[0];
                return false;
            }
            doc = parsed;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool ExportToFile(CustomLevelDocument doc, string filePath, out string error)
    {
        error = null;
        if (doc == null)
        {
            error = "Document is null.";
            return false;
        }
        List<string> errs = doc.Validate();
        if (errs.Count > 0)
        {
            error = errs[0];
            return false;
        }
        try
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, PackShareableString(doc), Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool ImportFromFile(string filePath, out CustomLevelDocument doc, out string error)
    {
        doc = null;
        error = null;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            error = "Shareable level file not found.";
            return false;
        }
        string content = File.ReadAllText(filePath, Encoding.UTF8);
        return TryUnpackShareableString(content, out doc, out error);
    }

    public static bool ToggleFavorite(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId)) return false;
        if (sessionFavorites.Contains(levelId))
        {
            sessionFavorites.Remove(levelId);
            return false;
        }
        sessionFavorites.Add(levelId);
        return true;
    }

    public static bool IsFavorite(string levelId)
    {
        return !string.IsNullOrWhiteSpace(levelId) && sessionFavorites.Contains(levelId);
    }
}
