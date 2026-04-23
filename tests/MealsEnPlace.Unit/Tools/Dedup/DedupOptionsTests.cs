// Feature: DedupOptions CLI parsing
//
// Scenario: No arguments returns defaults (live run, no dry-run)
// Scenario: --dry-run flag is parsed
// Scenario: --help / -h returns null (caller prints usage)
// Scenario: Unrecognized flag throws ArgumentException
// Scenario: UsageText is non-empty and describes the command

using FluentAssertions;
using MealsEnPlace.Tools.Dedup;

namespace MealsEnPlace.Unit.Tools.Dedup;

public class DedupOptionsTests
{
    [Fact]
    public void Parse_NoArguments_ReturnsDefaults()
    {
        var opts = DedupOptions.Parse([]);

        opts.Should().NotBeNull();
        opts!.DryRun.Should().BeFalse();
    }

    [Fact]
    public void Parse_DryRunFlag_IsSet()
    {
        var opts = DedupOptions.Parse(["--dry-run"]);

        opts!.DryRun.Should().BeTrue();
    }

    [Fact]
    public void Parse_HelpLongFlag_ReturnsNull()
    {
        var opts = DedupOptions.Parse(["--help"]);

        opts.Should().BeNull();
    }

    [Fact]
    public void Parse_HelpShortFlag_ReturnsNull()
    {
        var opts = DedupOptions.Parse(["-h"]);

        opts.Should().BeNull();
    }

    [Fact]
    public void Parse_UnrecognizedFlag_Throws()
    {
        Action act = () => DedupOptions.Parse(["--banana"]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UsageText_IsNonEmptyAndNamesCommand()
    {
        DedupOptions.UsageText.Should().NotBeNullOrWhiteSpace();
        DedupOptions.UsageText.Should().Contain("MealsEnPlace.Tools.Dedup");
        DedupOptions.UsageText.Should().Contain("--dry-run");
    }
}
