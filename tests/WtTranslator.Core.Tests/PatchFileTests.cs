using System.Text.Json;
using WtTranslator.Core;

namespace WtTranslator.Core.Tests;

public class PatchFileTests
{
    [Fact]
    public void Export_import_apply_preserves_translation_and_other_columns()
    {
        const string original = "Исходный текст";
        const string translation = "Танк \"Победа\"; № 1\nВторая строка 🚀";
        var patch = new Overlay();
        patch.Files["units.csv"] = new()
        {
            ["tank"] = new OverlayEntry(translation, Patcher.Hash(original))
        };
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var exported = Path.Combine(directory.FullName, "my-patch.json");
            File.WriteAllText(exported, patch.ToJson());
            var imported = Overlay.FromJson(File.ReadAllText(exported));
            Assert.Equal(patch.Files["units.csv"]["tank"], imported.Files["units.csv"]["tank"]);
            var lang = Directory.CreateDirectory(Path.Combine(directory.FullName, "lang")).FullName;
            var csv = Path.Combine(lang, "units.csv");
            File.WriteAllText(csv, $"\"<ID>\";\"<English>\";\"<Russian>\"\r\n\"tank\";\"Tank\";\"{original}\"\r\n");
            var report = Patcher.Apply(lang, imported);
            Assert.Equal(1, report.Applied);
            Assert.Empty(report.Conflicts);
            var fields = LocCsv.Parse(File.ReadAllText(csv)).Records[1].Fields;
            Assert.Equal(new[] { "tank", "Tank", translation }, fields);
        }
        finally { directory.Delete(true); }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"Files\":null}")]
    [InlineData("{\"Files\":[]}")]
    [InlineData("{\"Files\":{\"units.csv\":null}}")]
    [InlineData("{\"Files\":{\"units.csv\":{\"tank\":null}}}")]
    [InlineData("{\"Files\":{\"units.csv\":{\"tank\":{}}}}")]
    [InlineData("{\"Files\":{\"units.csv\":{\"tank\":{\"Value\":\"x\",\"BaseHash\":\"invalid\"}}}}")]
    [InlineData("not json")]
    public void Invalid_patch_is_rejected(string json) =>
        Assert.ThrowsAny<JsonException>(() => Overlay.FromJson(json));

    [Theory]
    [InlineData("../outside.csv")]
    [InlineData("..\\outside.csv")]
    [InlineData("/tmp/outside.csv")]
    [InlineData("C:\\outside.csv")]
    [InlineData("subdir/units.csv")]
    [InlineData("config.blk")]
    public void Imported_patch_cannot_target_paths_outside_lang(string name)
    {
        var patch = new Overlay();
        patch.Files[name] = new() { ["tank"] = new OverlayEntry("x", Patcher.Hash("original")) };
        Assert.ThrowsAny<JsonException>(() => Overlay.FromJson(patch.ToJson()));
        // The same check runs before any files are changed, even for programmatically built patches.
        Assert.ThrowsAny<JsonException>(() => Patcher.Apply("unused", patch));
    }

    [Fact]
    public void Empty_patch_and_empty_translation_are_valid()
    {
        Assert.Equal(0, Overlay.FromJson(new Overlay().ToJson()).Count);
        var patch = new Overlay();
        patch.Files["units.csv"] = new() { ["tank"] = new OverlayEntry("", Patcher.Hash("original")) };
        Assert.Equal("", Overlay.FromJson(patch.ToJson()).Files["units.csv"]["tank"].Value);
    }
}
