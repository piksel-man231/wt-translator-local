using System.Text;

namespace WtTranslator.Core;

/// <summary>One CSV record. Untouched records write back as the original raw text (byte-exact).</summary>
public sealed class LocRecord
{
    string _raw;
    readonly string _eol;
    public string[] Fields { get; private set; }
    public string Key => Fields[0];

    internal LocRecord(string raw, string eol, string[] fields) { _raw = raw; _eol = eol; Fields = fields; }

    public string Raw => _raw;

    public void SetField(int i, string value)
    {
        if (Fields[i] == value) return;
        Fields[i] = value;
        _raw = string.Join(';', Fields.Select(Quote)) + _eol;
    }

    static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
}

/// <summary>War Thunder lang CSV: ';' separated, quoted, CRLF, multiline fields, tolerant of broken rows.</summary>
public sealed class LocCsv
{
    public List<LocRecord> Records { get; } = new();

    public static LocCsv Parse(string text)
    {
        var csv = new LocCsv();
        int start = 0;
        bool inQ = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"') inQ = !inQ;
            else if (c == '\n' && !inQ) { csv.Add(text, start, i + 1); start = i + 1; }
        }
        if (start < text.Length) csv.Add(text, start, text.Length);
        return csv;
    }

    void Add(string text, int start, int end)
    {
        string raw = text[start..end];
        string eol = raw.EndsWith("\r\n") ? "\r\n" : raw.EndsWith('\n') ? "\n" : "";
        Records.Add(new LocRecord(raw, eol, SplitFields(raw[..^eol.Length])));
    }

    static string[] SplitFields(string s)
    {
        var res = new List<string>();
        var sb = new StringBuilder();
        bool inQ = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inQ)
            {
                if (c == '"') { if (i + 1 < s.Length && s[i + 1] == '"') { sb.Append('"'); i++; } else inQ = false; }
                else sb.Append(c);
            }
            else if (c == '"') inQ = true;
            else if (c == ';') { res.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        res.Add(sb.ToString());
        return res.ToArray();
    }

    public int ColumnIndex(string header) => Array.IndexOf(Records[0].Fields, header);

    public override string ToString() => string.Concat(Records.Select(r => r.Raw));
}
