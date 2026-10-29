using WtTranslator.Core;

namespace WtTranslator.Core.Tests;

public class LocCsvTests
{
    const string Sample = "\"<ID>\";\"<English>\";\"<Russian>\"\r\n\"a\";\"Hi\";\"Привет\"\r\n\"b\";\"Two\nlines \"\"q\"\"\";\"Два\"\r\nbroken\r\n\r\n\"last\";\"x\";\"y\"";

    [Fact]
    public void Roundtrip_is_exact() => Assert.Equal(Sample, LocCsv.Parse(Sample).ToString());

    [Fact]
    public void Parses_multiline_and_quotes()
    {
        var c = LocCsv.Parse(Sample);
        Assert.Equal(6, c.Records.Count);
        Assert.Equal("Two\nlines \"q\"", c.Records[2].Fields[1]);
        Assert.Equal(2, c.ColumnIndex("<Russian>"));
    }

    [Fact]
    public void SetField_rewrites_only_that_record()
    {
        var c = LocCsv.Parse(Sample);
        c.Records[1].SetField(2, "Здарова \"друг\"");
        var expected = Sample.Replace("\"Привет\"", "\"Здарова \"\"друг\"\"\"");
        Assert.Equal(expected, c.ToString());
    }

    // Set WT_LANG_DIR to a game's lang/ folder to check every real file.
    [Fact]
    public void Roundtrip_real_files()
    {
        var dir = Environment.GetEnvironmentVariable("WT_LANG_DIR");
        if (dir is null) return;
        foreach (var f in Directory.GetFiles(dir, "*.csv"))
        {
            var text = File.ReadAllText(f);
            Assert.True(text == LocCsv.Parse(text).ToString(), f);
        }
    }
}
