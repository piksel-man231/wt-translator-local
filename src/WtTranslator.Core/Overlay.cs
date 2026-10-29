using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WtTranslator.Core;

/// <param name="Value">our Russian text</param>
/// <param name="BaseHash">hash of the game's original Russian text when we made the edit; a mismatch later means the game changed that string</param>
public sealed record OverlayEntry(string Value, string BaseHash);

/// <summary>Our edits: file name -> key -> entry. Only the Russian column is patched.</summary>
public sealed class Overlay
{
    public Dictionary<string, Dictionary<string, OverlayEntry>> Files { get; set; } = new();
    public int Count => Files.Values.Sum(f => f.Count);

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    public static Overlay FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("Files", out var files) || files.ValueKind != JsonValueKind.Object)
            throw new JsonException("В файле патча должен быть объект Files.");
        var overlay = JsonSerializer.Deserialize<Overlay>(json)
            ?? throw new JsonException("Файл патча пуст.");
        overlay.Validate();
        return overlay;
    }

    // Imported patches may only address CSV files directly inside lang.
    public void Validate()
    {
        if (Files is null) throw new JsonException("В файле патча отсутствует Files.");
        foreach (var (name, entries) in Files)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\') ||
                name.Contains(':') || name.Contains('\0') ||
                !name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                throw new JsonException("Недопустимое имя CSV-файла в патче: " + name);
            if (entries is null) throw new JsonException("Не указан список замен: " + name);
            foreach (var (key, entry) in entries)
                if (string.IsNullOrEmpty(key) || entry is null || entry.Value is null ||
                    entry.BaseHash is null || entry.BaseHash.Length != 12 ||
                    !entry.BaseHash.All(Uri.IsHexDigit))
                    throw new JsonException("Некорректная замена в патче: " + name + ":" + key);
        }
    }
}

public sealed record PatchReport(int Applied, List<string> Conflicts, List<string> MissingKeys);

public static class Patcher
{
    const string Ru = "<Russian>", En = "<English>";
    static readonly UTF8Encoding Utf8 = new(false);

    public static string Hash(string s) => Convert.ToHexString(SHA1.HashData(Utf8.GetBytes(s)))[..12];

    static LocCsv Read(string path) => LocCsv.Parse(File.ReadAllText(path, Utf8));

    /// <summary>Build an overlay from an already-patched lang folder: same key, same English, different Russian = our edit.</summary>
    public static Overlay Import(string cleanDir, string patchedDir)
    {
        var o = new Overlay();
        foreach (var cleanPath in Directory.GetFiles(cleanDir, "*.csv"))
        {
            var name = Path.GetFileName(cleanPath);
            var patchedPath = Path.Combine(patchedDir, name);
            if (!File.Exists(patchedPath)) continue;
            var clean = Read(cleanPath); var patched = Read(patchedPath);
            int ru = clean.ColumnIndex(Ru), en = clean.ColumnIndex(En);
            if (ru < 0 || en < 0 || patched.ColumnIndex(Ru) != ru || patched.ColumnIndex(En) != en) continue;
            var orig = new Dictionary<string, string[]>();
            foreach (var r in clean.Records.Skip(1)) if (r.Fields.Length > Math.Max(ru, en)) orig.TryAdd(r.Key, r.Fields);
            foreach (var r in patched.Records.Skip(1))
            {
                if (r.Fields.Length <= Math.Max(ru, en) || !orig.TryGetValue(r.Key, out var c)) continue;
                if (c[en] != r.Fields[en] || c[ru] == r.Fields[ru]) continue;
                if (!o.Files.TryGetValue(name, out var f)) o.Files[name] = f = new();
                f.TryAdd(r.Key, new OverlayEntry(r.Fields[ru], Hash(c[ru])));
            }
        }
        return o;
    }

    /// <summary>Apply overlay in place to a clean lang folder. Conflicting strings (original changed since our edit) are still applied but reported.</summary>
    public static PatchReport Apply(string langDir, Overlay o)
    {
        o.Validate();
        int applied = 0; var conflicts = new List<string>(); var missing = new List<string>();
        foreach (var (name, entries) in o.Files)
        {
            var path = Path.Combine(langDir, name);
            if (!File.Exists(path)) { missing.AddRange(entries.Keys.Select(k => $"{name}:{k}")); continue; }
            var csv = Read(path);
            int ru = csv.ColumnIndex(Ru);
            if (ru < 0) continue;
            var seen = new HashSet<string>();
            foreach (var r in csv.Records.Skip(1))
            {
                if (r.Fields.Length <= ru || !entries.TryGetValue(r.Key, out var e)) continue;
                seen.Add(r.Key);
                if (Hash(r.Fields[ru]) != e.BaseHash && r.Fields[ru] != e.Value) conflicts.Add($"{name}:{r.Key}");
                r.SetField(ru, e.Value); applied++;
            }
            missing.AddRange(entries.Keys.Where(k => !seen.Contains(k)).Select(k => $"{name}:{k}"));
            File.WriteAllText(path, csv.ToString(), Utf8);
        }
        return new PatchReport(applied, conflicts, missing);
    }
}
