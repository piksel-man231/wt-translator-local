using System.Text;

namespace WtTranslator.Core;

public sealed record LangRow(string File, string Key, string En, string Ru)
{
    public string Title => $"{File} : {Key}";
}

/// <summary>In-memory (file, key, English, Russian) list of a lang folder for substring search.</summary>
public sealed class LangIndex
{
    public List<LangRow> Rows { get; } = new();

    public static LangIndex Load(string langDir)
    {
        var idx = new LangIndex();
        foreach (var path in Directory.GetFiles(langDir, "*.csv").Order())
        {
            var csv = LocCsv.Parse(File.ReadAllText(path, new UTF8Encoding(false)));
            if (csv.Records.Count == 0) continue;
            int en = csv.ColumnIndex("<English>"), ru = csv.ColumnIndex("<Russian>");
            if (en < 0 || ru < 0) continue;
            var name = Path.GetFileName(path);
            foreach (var r in csv.Records.Skip(1))
                if (r.Fields.Length > Math.Max(en, ru)) idx.Rows.Add(new LangRow(name, r.Key, r.Fields[en], r.Fields[ru]));
        }
        return idx;
    }

    /// <summary>Text matches (English or Russian) first, then key matches.</summary>
    public List<LangRow> Search(string query, int max = 100)
    {
        var res = new List<LangRow>();
        if (string.IsNullOrWhiteSpace(query)) return res;
        var c = StringComparison.OrdinalIgnoreCase;
        foreach (var r in Rows)
            if (r.Ru.Contains(query, c) || r.En.Contains(query, c)) { res.Add(r); if (res.Count >= max) return res; }
        foreach (var r in Rows)
            if (r.Key.Contains(query, c) && !res.Contains(r)) { res.Add(r); if (res.Count >= max) break; }
        return res;
    }
}
