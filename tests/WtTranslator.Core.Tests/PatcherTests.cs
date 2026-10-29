using WtTranslator.Core;

namespace WtTranslator.Core.Tests;

public class PatcherTests
{
    static string Csv(string ruA, string ruB) =>
        $"\"<ID>\";\"<English>\";\"<Russian>\"\r\n\"a\";\"Hi\";\"{ruA}\"\r\n\"b\";\"Two\";\"{ruB}\"\r\n";

    static string Dir(string csv, string file = "t.csv")
    {
        var d = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(d, file), csv);
        return d;
    }

    [Fact]
    public void Import_finds_only_edits_with_same_english()
    {
        var clean = Dir(Csv("Привет", "Два"));
        var patched = Dir("\"<ID>\";\"<English>\";\"<Russian>\"\r\n\"a\";\"Hi\";\"Здарова\"\r\n\"b\";\"CHANGED\";\"Другое\"\r\n");
        var o = Patcher.Import(clean, patched);
        Assert.Equal(1, o.Count);
        Assert.Equal("Здарова", o.Files["t.csv"]["a"].Value);
    }

    [Fact]
    public void Apply_patches_only_targets_and_reports_conflict()
    {
        var clean = Dir(Csv("Привет", "Два"));
        var o = Patcher.Import(clean, Dir(Csv("Здарова", "Два")));
        var updated = Dir(Csv("Приветствую", "Два"));           // game changed original of "a"
        var rep = Patcher.Apply(updated, o);
        Assert.Equal(1, rep.Applied);
        Assert.Equal(new[] { "t.csv:a" }, rep.Conflicts);
        Assert.Equal(Csv("Здарова", "Два"), File.ReadAllText(Path.Combine(updated, "t.csv")));
    }

    [Fact]
    public void Overlay_json_roundtrip()
    {
        var o = Patcher.Import(Dir(Csv("Привет", "Два")), Dir(Csv("Здарова", "Два")));
        Assert.Equal(o.Files["t.csv"]["a"], Overlay.FromJson(o.ToJson()).Files["t.csv"]["a"]);
    }

    // WT_CLEAN_DIR = fresh lang, WT_LANG_DIR = patched lang: importing, applying to a copy of clean, re-importing must be stable.
    [Fact]
    public void Real_import_apply_import_is_stable()
    {
        var clean = Environment.GetEnvironmentVariable("WT_CLEAN_DIR");
        var patched = Environment.GetEnvironmentVariable("WT_LANG_DIR");
        if (clean is null || patched is null) return;
        var o = Patcher.Import(clean, patched);
        Assert.True(o.Count > 0);
        var copy = Directory.CreateTempSubdirectory().FullName;
        foreach (var f in Directory.GetFiles(clean)) File.Copy(f, Path.Combine(copy, Path.GetFileName(f)));
        var rep = Patcher.Apply(copy, o);
        Assert.Equal(o.Count, rep.Applied);
        Assert.Empty(rep.Conflicts);
        Assert.Equal(o.Count, Patcher.Import(clean, copy).Count);
    }
}

public class LangIndexTests
{
    [Fact]
    public void Search_finds_by_text_then_key()
    {
        var d = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(d, "t.csv"),
            "\"<ID>\";\"<English>\";\"<Russian>\"\r\n\"k1\";\"Carrier\";\"Авианосец\"\r\n\"carrier_key\";\"Other\";\"Иное\"\r\n");
        var idx = LangIndex.Load(d);
        Assert.Equal(new[] { "k1", "carrier_key" }, idx.Search("carr").Select(r => r.Key));
        Assert.Equal(new[] { "k1" }, idx.Search("АВИАН").Select(r => r.Key));
        Assert.Empty(idx.Search(" "));
    }
}
