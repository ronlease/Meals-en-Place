// Feature: FoldGroupResolver groups candidates by normalized key and picks a survivor
//
// Scenario: Single-member groups are skipped (nothing to fold)
//   Given one candidate with NormalizedKey "onion"
//   Then Resolve returns no fold groups
//
// Scenario: Shortest name wins the survivor slot
//   Given three candidates normalizing to "onion": "onion", "chopped onion", "fresh chopped onion"
//   Then the survivor is "onion" and the losers are the other two
//
// Scenario: On shortest-name tie, highest reference count wins
//   Given two 6-char candidates "onions" (ref count 10) and "garlic" cannot collide on key,
//   use two distinct 5-char names that normalize to the same key — not natural in prose,
//   so test via two "onion"-normalizing names of equal length: "onion" (ref 10) and "oniom" (ref 1) forced to same key via custom stopword set
//   Then the higher-ref-count name wins
//
// Scenario: Empty normalized keys are dropped, not folded
//   Given candidates whose names are all-stopwords
//   Then those candidates are skipped entirely
//
// Scenario: RequiredTypoCorrection=false wins over RequiredTypoCorrection=true even when the misspelled
//   candidate is shorter and has a higher reference count -- MEP-053 regression
//   Given a group containing "mayonaise" (9 chars, RequiredTypoCorrection=true, refs=50)
//   And "mayonnaise" (10 chars, RequiredTypoCorrection=false, refs=5)
//   When Resolve picks the survivor
//   Then "mayonnaise" is the survivor because RequiredTypoCorrection=false is checked before Name.Length
//   And "mayonaise" is the loser
//
// Scenario: When no candidate in a group requires typo correction, the pre-existing shortest-name
//   rule is unchanged -- regression guard to ensure the new ordering did not break existing behaviour
//   Given two candidates both with RequiredTypoCorrection=false normalizing to the same key
//   And the shorter-named candidate has fewer references than the longer one
//   When Resolve picks the survivor
//   Then the shorter name wins as before
//
// Scenario: When every candidate in a group requires typo correction, the resolver falls through
//   to shortest name then highest reference count, same as before the MEP-053 fix
//   Given two candidates both with RequiredTypoCorrection=true normalizing to the same key
//   And one candidate has a shorter name than the other
//   When Resolve picks the survivor
//   Then the shortest name wins regardless of reference count

using FluentAssertions;
using MealsEnPlace.Tools.Dedup;

namespace MealsEnPlace.Unit.Tools.Dedup;

public class FoldGroupResolverTests
{
    [Fact]
    public void Resolve_SingleMemberGroup_SkipsIt()
    {
        var candidates = new[]
        {
            Candidate("onion", "onion", references: 5)
        };

        FoldGroupResolver.Resolve(candidates).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ShortestNameWinsSurvivor()
    {
        var candidates = new[]
        {
            Candidate("fresh chopped onion", "onion", references: 2),
            Candidate("chopped onion", "onion", references: 2),
            Candidate("onion", "onion", references: 2)
        };

        var groups = FoldGroupResolver.Resolve(candidates);

        groups.Should().HaveCount(1);
        groups[0].Survivor.Name.Should().Be("onion");
        groups[0].Losers.Select(l => l.Name)
            .Should().BeEquivalentTo(["chopped onion", "fresh chopped onion"]);
    }

    [Fact]
    public void Resolve_EqualLengthNames_HighestReferenceCountWinsSurvivor()
    {
        // Two 6-char names both normalizing to the same (synthetic) key "k".
        var candidates = new[]
        {
            Candidate("name-a", "k", references: 1),
            Candidate("name-b", "k", references: 99)
        };

        var groups = FoldGroupResolver.Resolve(candidates);

        groups.Should().HaveCount(1);
        groups[0].Survivor.Name.Should().Be("name-b");
    }

    [Fact]
    public void Resolve_EqualLengthAndReferenceCount_BreaksAlphabeticalLast()
    {
        var candidates = new[]
        {
            Candidate("zebra", "z", references: 5),
            Candidate("apple", "z", references: 5)
        };

        var groups = FoldGroupResolver.Resolve(candidates);

        groups[0].Survivor.Name.Should().Be("apple");
    }

    [Fact]
    public void Resolve_EmptyNormalizedKey_CandidatesAreDropped()
    {
        var candidates = new[]
        {
            Candidate("chopped fresh diced", normalizedKey: string.Empty, references: 0),
            Candidate("sliced raw", normalizedKey: string.Empty, references: 0)
        };

        FoldGroupResolver.Resolve(candidates).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_MultipleGroups_OrderedByLargestFoldFirst()
    {
        var candidates = new[]
        {
            Candidate("onion", "onion", references: 1),
            Candidate("chopped onion", "onion", references: 1),
            Candidate("tomato", "tomato", references: 1),
            Candidate("tomatoes", "tomato", references: 1),
            Candidate("fresh tomatoes", "tomato", references: 1),
            Candidate("diced tomatoes", "tomato", references: 1)
        };

        var groups = FoldGroupResolver.Resolve(candidates);

        groups.Should().HaveCount(2);
        groups[0].NormalizedKey.Should().Be("tomato");
        groups[0].Losers.Should().HaveCount(3);
        groups[1].NormalizedKey.Should().Be("onion");
        groups[1].Losers.Should().HaveCount(1);
    }

    // ── MEP-053: RequiredTypoCorrection tie-break ──────────────────────────

    [Fact]
    public void Resolve_CorrectlySpelledCandidateWinsOverShorterMisspelledWithHigherReferenceCount()
    {
        // Adversarial regression test for MEP-053. The old code sorted by Name.Length
        // first, so "mayonaise" (9 chars, misspelled) beat "mayonnaise" (10 chars,
        // correct). The fix places RequiredTypoCorrection as the top-priority tie-break:
        // false (correctly-spelled) always wins over true (misspelled) regardless of
        // how much shorter or how much more referenced the misspelling is.
        var candidates = new[]
        {
            Candidate("mayonaise",   "mayonnaise", references: 50, requiresTypoCorrection: true),
            Candidate("mayonnaise",  "mayonnaise", references:  5, requiresTypoCorrection: false)
        };

        var groups = FoldGroupResolver.Resolve(candidates);

        groups.Should().HaveCount(1);
        groups[0].Survivor.Name.Should().Be("mayonnaise");
        groups[0].Losers.Should().ContainSingle(l => l.Name == "mayonaise");
    }

    [Fact]
    public void Resolve_NoCandidateRequiresTypoCorrection_ShortestNameRuleUnchanged()
    {
        // Regression guard: when no candidate requires typo correction the pre-existing
        // shortest-name rule must hold exactly as before, confirming the new ordering
        // has no side-effects on clean data.
        var candidates = new[]
        {
            Candidate("fresh chopped onion", "onion", references: 10, requiresTypoCorrection: false),
            Candidate("onion",               "onion", references:  2, requiresTypoCorrection: false)
        };

        var groups = FoldGroupResolver.Resolve(candidates);

        groups.Should().HaveCount(1);
        groups[0].Survivor.Name.Should().Be("onion");
    }

    [Fact]
    public void Resolve_AllCandidatesRequireTypoCorrection_FallsThroughToShortestNameThenReferenceCount()
    {
        // When every candidate is a misspelling (no clean spelling exists in the
        // group), RequiredTypoCorrection is the same for all and the resolver falls
        // through to the next tie-break: shortest Name. The higher-referenced but
        // longer misspelling must NOT override the shorter one.
        var candidates = new[]
        {
            Candidate("mayonaise", "mayonnaise", references: 50, requiresTypoCorrection: true),
            Candidate("mayonese",  "mayonnaise", references:  1, requiresTypoCorrection: true)
        };

        var groups = FoldGroupResolver.Resolve(candidates);

        groups.Should().HaveCount(1);
        // "mayonese" is 8 chars; "mayonaise" is 9 chars — shortest wins.
        groups[0].Survivor.Name.Should().Be("mayonese");
    }

    private static CanonicalIngredientFoldCandidate Candidate(
        string name,
        string normalizedKey,
        int references,
        bool requiresTypoCorrection = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedKey = normalizedKey,
            ReferenceCount = references,
            RequiredTypoCorrection = requiresTypoCorrection
        };
}
