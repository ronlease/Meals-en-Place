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

    private static CanonicalIngredientFoldCandidate Candidate(string name, string normalizedKey, int references) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedKey = normalizedKey,
            ReferenceCount = references
        };
}
