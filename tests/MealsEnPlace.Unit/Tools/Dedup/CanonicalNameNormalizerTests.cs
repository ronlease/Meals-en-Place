// Feature: CanonicalNameNormalizer produces fold-group keys for dedup
//
// Scenario: Exact match on the generic name normalizes to itself
//   Given input "onion"
//   Then Normalize returns "onion"
//
// Scenario: Prep modifiers strip out so "chopped onion" folds with "onion"
//   Given inputs "chopped onion", "diced onion", "sliced onion", and "onion"
//   Then Normalize returns the same key for all four
//
// Scenario: Plurals collapse: -s, -es, -ies, -oes
//   Given "onions", "tomatoes", "berries", "potatoes"
//   Then Normalize returns singular forms
//
// Scenario: Size-significant modifiers are preserved (NOT stopwords)
//   Given "baby carrot" and "carrot"
//   Then Normalize returns different keys
//
// Scenario: Word order does not matter
//   Given "black pepper" and "pepper black"
//   Then Normalize returns the same key
//
// Scenario: Mixed case, tabs, and punctuation normalize out
//   Given "CHOPPED  Onions (fresh)", the tokens sort and punctuation strips
//   Then the key matches plain "onion"
//
// Scenario: Empty or whitespace input returns empty string
//   Given "" or "   "
//   Then Normalize returns ""

using FluentAssertions;
using MealsEnPlace.Tools.Dedup;

namespace MealsEnPlace.Unit.Tools.Dedup;

public class CanonicalNameNormalizerTests
{
    private readonly CanonicalNameNormalizer _normalizer = CanonicalNameNormalizer.Default;

    [Fact]
    public void Normalize_GenericName_ReturnsItself()
    {
        _normalizer.Normalize("onion").Should().Be("onion");
    }

    [Theory]
    [InlineData("chopped onion")]
    [InlineData("diced onion")]
    [InlineData("sliced onion")]
    [InlineData("onion")]
    [InlineData("onions")]
    [InlineData("fresh onions")]
    public void Normalize_PrepModifiersAndPlurals_FoldToOnion(string input)
    {
        _normalizer.Normalize(input).Should().Be("onion");
    }

    [Theory]
    [InlineData("tomatoes", "tomato")]
    [InlineData("potatoes", "potato")]
    [InlineData("berries", "berry")]
    [InlineData("pastries", "pastry")]
    [InlineData("dishes", "dish")]
    [InlineData("boxes", "box")]
    [InlineData("carrots", "carrot")]
    public void Normalize_PluralForms_CollapseToSingular(string input, string expected)
    {
        _normalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("glass", "glass")]   // -ss ending should NOT drop the s
    [InlineData("grass", "grass")]
    [InlineData("as", "as")]        // short word should NOT drop the s
    public void Normalize_EdgeEndings_AreNotOverSingularized(string input, string expected)
    {
        _normalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_SizeModifier_StaysDistinctFromGeneric()
    {
        _normalizer.Normalize("baby carrot").Should().NotBe(_normalizer.Normalize("carrot"));
        _normalizer.Normalize("jumbo shrimp").Should().NotBe(_normalizer.Normalize("shrimp"));
        _normalizer.Normalize("mini pepper").Should().NotBe(_normalizer.Normalize("pepper"));
    }

    [Fact]
    public void Normalize_WordOrder_DoesNotAffectKey()
    {
        _normalizer.Normalize("black pepper").Should().Be(_normalizer.Normalize("pepper black"));
    }

    [Theory]
    [InlineData("CHOPPED  Onions (fresh)", "onion")]
    [InlineData("Sliced, diced-onion", "onion")]
    [InlineData("\tdiced\tonions\t", "onion")]
    public void Normalize_MixedCaseAndPunctuation_ReducesToCleanKey(string input, string expected)
    {
        _normalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Normalize_EmptyOrWhitespace_ReturnsEmptyString(string? input)
    {
        _normalizer.Normalize(input).Should().Be(string.Empty);
    }

    [Fact]
    public void Normalize_AllTokensAreStopwords_ReturnsEmpty()
    {
        _normalizer.Normalize("chopped fresh diced").Should().Be(string.Empty);
    }
}
