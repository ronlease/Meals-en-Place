// Feature: KaggleRowReader streaming CSV parser
//
// Scenario: Valid non-Recipes1M row is yielded with parsed arrays
//   Given a CSV with one "Gathered" source row
//   When Stream().Rows is enumerated
//   Then the row exposes title, link, site, source, ingredients[], directions[], NER[]
//
// Scenario: Recipes1M rows are skipped
//   Given a CSV with two rows: one Gathered, one Recipes1M
//   When Stream().Rows is enumerated
//   Then only the Gathered row is yielded
//   And Counters.SkippedRecipes1M reflects the skipped row
//   And Counters.TotalRowsRead reflects both rows
//
// Scenario: Malformed JSON in an array column skips the row without halting
//   Given a CSV row whose ingredients column contains invalid JSON
//   When Stream().Rows is enumerated
//   Then that row is not yielded
//   And Counters.MalformedRowsSkipped is incremented
//   And subsequent valid rows are yielded normally
//
// Scenario: Missing CSV file throws FileNotFoundException
//   When Stream is called with a non-existent path
//   Then FileNotFoundException is thrown
//
// Scenario: Empty CSV (header only) yields no rows and leaves counters at zero
//   Given a CSV with only the header line
//   When Stream().Rows is enumerated
//   Then no rows are yielded and counters are 0 across the board
//
// Scenario: Early break mid-enumeration leaves counters reflecting consumed rows
//   Given a CSV with 3 rows, caller breaks after the first
//   When enumeration stops
//   Then Counters.TotalRowsRead reflects only the consumed prefix

using FluentAssertions;
using MealsEnPlace.Tools.Ingest;

namespace MealsEnPlace.Unit.Tools.Ingest;

public class KaggleRowReaderTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { /* best-effort */ }
        }
    }

    private string WriteCsv(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private const string Header = "title,ingredients,directions,link,source,NER,site";

    // Row-building helper: double-quotes inside strings get doubled per RFC 4180
    // so CsvHelper un-quotes them back to single double-quotes, yielding JSON.
    private static string BuildRow(
        string title, string[] ingredients, string[] directions,
        string link, string source, string[] ner, string site)
    {
        var ingredientsJson = "[" + string.Join(",", ingredients.Select(i => $"\"{i}\"")) + "]";
        var directionsJson = "[" + string.Join(",", directions.Select(d => $"\"{d}\"")) + "]";
        var nerJson = "[" + string.Join(",", ner.Select(n => $"\"{n}\"")) + "]";

        // Escape quotes for CSV by doubling them, then wrap each field in quotes.
        string q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        return string.Join(",",
            q(title), q(ingredientsJson), q(directionsJson),
            q(link), q(source), q(nerJson), q(site));
    }

    // ── Yielding and parsing ──────────────────────────────────────────────────

    [Fact]
    public void Stream_ValidGatheredRow_YieldsParsedRow()
    {
        var csv = Header + "\n" + BuildRow(
            "Test Recipe",
            ["1 cup flour", "2 eggs"],
            ["Mix dry", "Mix wet"],
            "https://example.com/recipe",
            "Gathered",
            ["flour", "eggs"],
            "example.com") + "\n";

        var path = WriteCsv(csv);

        var streamResult = KaggleRowReader.Stream(path);
        var rows = streamResult.Rows.ToList();

        rows.Should().HaveCount(1);
        rows[0].Title.Should().Be("Test Recipe");
        rows[0].Source.Should().Be("Gathered");
        rows[0].Site.Should().Be("example.com");
        rows[0].Link.Should().Be("https://example.com/recipe");
        rows[0].Ingredients.Should().Equal("1 cup flour", "2 eggs");
        rows[0].Directions.Should().Equal("Mix dry", "Mix wet");
        rows[0].Ner.Should().Equal("flour", "eggs");
    }

    // ── Recipes1M filter ──────────────────────────────────────────────────────

    [Fact]
    public void Stream_Recipes1MRow_IsSkipped()
    {
        var csv = Header + "\n"
            + BuildRow("Gathered One", ["a"], ["step"], "u1", "Gathered", ["a"], "site1") + "\n"
            + BuildRow("Mit One", ["b"], ["step"], "u2", "Recipes1M", ["b"], "site2") + "\n";
        var path = WriteCsv(csv);

        var streamResult = KaggleRowReader.Stream(path);
        var rows = streamResult.Rows.ToList();

        rows.Should().HaveCount(1);
        rows[0].Title.Should().Be("Gathered One");
        streamResult.Counters.SkippedRecipes1M.Should().Be(1);
        streamResult.Counters.TotalRowsRead.Should().Be(2);
    }

    // ── Malformed JSON ────────────────────────────────────────────────────────

    [Fact]
    public void Stream_MalformedJsonInIngredients_SkipsRowAndContinues()
    {
        var goodRow = BuildRow("Good", ["1 c flour"], ["mix"], "u1", "Gathered", ["flour"], "site1");

        // Manually compose a malformed row: ingredients column has invalid JSON.
        var badRow = string.Join(",",
            "\"Bad\"", "\"[this is not json]\"",
            "\"[]\"", "\"u2\"", "\"Gathered\"", "\"[]\"", "\"site2\"");

        var csv = Header + "\n" + goodRow + "\n" + badRow + "\n";
        var path = WriteCsv(csv);

        var streamResult = KaggleRowReader.Stream(path);
        var rows = streamResult.Rows.ToList();

        rows.Should().HaveCount(1);
        rows[0].Title.Should().Be("Good");
        streamResult.Counters.MalformedRowsSkipped.Should().Be(1);
    }

    // ── Missing file ──────────────────────────────────────────────────────────

    [Fact]
    public void Stream_MissingFile_Throws()
    {
        Action act = () => KaggleRowReader.Stream("/no/such/file/should/exist.csv");

        act.Should().Throw<FileNotFoundException>();
    }

    // ── Empty CSV ─────────────────────────────────────────────────────────────

    [Fact]
    public void Stream_HeaderOnly_YieldsNoRows()
    {
        var path = WriteCsv(Header + "\n");

        var streamResult = KaggleRowReader.Stream(path);
        var rows = streamResult.Rows.ToList();

        rows.Should().BeEmpty();
        streamResult.Counters.TotalRowsRead.Should().Be(0);
        streamResult.Counters.SkippedRecipes1M.Should().Be(0);
        streamResult.Counters.MalformedRowsSkipped.Should().Be(0);
    }

    // ── Early break leaves partial counters ───────────────────────────────────

    [Fact]
    public void Stream_CallerBreaksMidEnumeration_CountersReflectConsumedRows()
    {
        var csv = Header + "\n"
            + BuildRow("First", ["a"], ["s"], "u1", "Gathered", ["a"], "s1") + "\n"
            + BuildRow("Second", ["b"], ["s"], "u2", "Gathered", ["b"], "s2") + "\n"
            + BuildRow("Third", ["c"], ["s"], "u3", "Gathered", ["c"], "s3") + "\n";
        var path = WriteCsv(csv);

        var streamResult = KaggleRowReader.Stream(path);
        var consumed = new List<KaggleRow>();

        foreach (var row in streamResult.Rows)
        {
            consumed.Add(row);
            if (consumed.Count == 1)
            {
                break;
            }
        }

        consumed.Should().HaveCount(1);
        streamResult.Counters.TotalRowsRead.Should().Be(1);
    }
}
