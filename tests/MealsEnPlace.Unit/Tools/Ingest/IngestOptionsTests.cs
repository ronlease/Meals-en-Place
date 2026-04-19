// Feature: IngestOptions CLI parsing
//
// Scenario: Required --csv produces a valid options instance
//   Given args "--csv path.csv"
//   When Parse is called
//   Then CsvPath is "path.csv", DryRun is false, MaxRows is null
//
// Scenario: --dry-run flag is parsed
//   When args include "--dry-run"
//   Then DryRun is true on the returned options
//
// Scenario: --max-rows integer is parsed
//   When args include "--max-rows 500"
//   Then MaxRows is 500
//
// Scenario: --max-rows rejects non-positive integers
//   Given args with "--max-rows 0" or "--max-rows -5"
//   When Parse is called
//   Then ArgumentException is thrown
//
// Scenario: --max-rows rejects non-integer
//   Given args with "--max-rows abc"
//   When Parse is called
//   Then ArgumentException is thrown
//
// Scenario: Missing --csv value is an error
//   Given args ending with "--csv" and no following token
//   When Parse is called
//   Then ArgumentException is thrown
//
// Scenario: Unrecognized flag is an error
//   Given args with "--banana"
//   When Parse is called
//   Then ArgumentException is thrown
//
// Scenario: --help returns null (caller prints usage)
//   Given args including "--help"
//   When Parse is called
//   Then the return value is null
//
// Scenario: No --csv at all is an error
//   Given empty args or args with only --dry-run
//   When Parse is called
//   Then ArgumentException is thrown

using FluentAssertions;
using MealsEnPlace.Tools.Ingest;

namespace MealsEnPlace.Unit.Tools.Ingest;

public class IngestOptionsTests
{
    [Fact]
    public void Parse_RequiredCsvOnly_ReturnsDefaults()
    {
        var opts = IngestOptions.Parse(["--csv", "path.csv"]);

        opts.Should().NotBeNull();
        opts!.CsvPath.Should().Be("path.csv");
        opts.DryRun.Should().BeFalse();
        opts.MaxRows.Should().BeNull();
    }

    [Fact]
    public void Parse_DryRunFlag_IsSet()
    {
        var opts = IngestOptions.Parse(["--csv", "path.csv", "--dry-run"]);

        opts!.DryRun.Should().BeTrue();
    }

    [Fact]
    public void Parse_MaxRowsPositiveInteger_IsSet()
    {
        var opts = IngestOptions.Parse(["--csv", "path.csv", "--max-rows", "500"]);

        opts!.MaxRows.Should().Be(500);
    }

    [Fact]
    public void Parse_MaxRowsZero_Throws()
    {
        Action act = () => IngestOptions.Parse(["--csv", "path.csv", "--max-rows", "0"]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_MaxRowsNegative_Throws()
    {
        Action act = () => IngestOptions.Parse(["--csv", "path.csv", "--max-rows", "-5"]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_MaxRowsNonInteger_Throws()
    {
        Action act = () => IngestOptions.Parse(["--csv", "path.csv", "--max-rows", "abc"]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_CsvWithoutValue_Throws()
    {
        Action act = () => IngestOptions.Parse(["--csv"]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_MaxRowsWithoutValue_Throws()
    {
        Action act = () => IngestOptions.Parse(["--csv", "path.csv", "--max-rows"]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_UnrecognizedFlag_Throws()
    {
        Action act = () => IngestOptions.Parse(["--csv", "path.csv", "--banana"]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_HelpShortFlag_ReturnsNull()
    {
        var opts = IngestOptions.Parse(["-h"]);

        opts.Should().BeNull();
    }

    [Fact]
    public void Parse_HelpLongFlag_ReturnsNull()
    {
        var opts = IngestOptions.Parse(["--help"]);

        opts.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyArgs_Throws()
    {
        Action act = () => IngestOptions.Parse([]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_DryRunWithoutCsv_Throws()
    {
        Action act = () => IngestOptions.Parse(["--dry-run"]);

        act.Should().Throw<ArgumentException>();
    }
}
