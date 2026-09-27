// Feature: CanonicalNameNormalizer produces fold-group keys for dedup
//
// Scenario: Exact match on the generic name normalizes to itself
//   Given input "onion"
//   Then Normalize returns "onion"
//
// Scenario: Prep-cut modifiers strip out so "chopped onion" folds with "onion"
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
// Scenario: Preservation-state words are preserved (NOT stopwords) -- MEP-050
//   Given "fresh peas" and "frozen peas" and "peas"
//   Then Normalize returns three different keys
//
// Scenario: Word order does not matter
//   Given "black pepper" and "pepper black"
//   Then Normalize returns the same key
//
// Scenario: Mixed case, tabs, and punctuation normalize out
//   Given "CHOPPED  Onions", the tokens sort and punctuation strips
//   Then the key matches plain "onion"
//
// Scenario: Empty or whitespace input returns empty string
//   Given "" or "   "
//   Then Normalize returns ""
//
// Scenario: Brand-name phrases strip out entirely -- MEP-050
//   Given "lesueur peas", "del monte peas", "campbell's pea soup",
//     "birds eye sweet peas", "green giant baby early peas", "knorr green peas"
//   Then the brand phrase is removed before tokenization
//
// Scenario: A brand word that overlaps a real descriptor is only stripped as
//   part of the full brand phrase -- MEP-050
//   Given "green giant peas" (brand) and "green peas" (color descriptor)
//   Then "green giant peas" folds to "pea" but "green peas" folds to "green pea"
//
// Scenario: Curated typo corrections apply before the fold key is built -- MEP-050
//   Given "frozed peas", "spit peas", "slit peas", "splitt peas", "sping peas",
//     "earlie peas", "pidgeaon peas", "lesuer peas", "leseur peas", "lesueuer peas"
//   Then each normalizes as if the typo were corrected
//
// Scenario: Compound-word and split-word synonyms share a fold key -- MEP-050
//   Given "chickpea" and "chick pea", and "blackeyed peas" / "black eye peas" /
//     "back eyed peas" / "blacck eyed peas" / "black-eyed peas"
//   Then each group normalizes to the same key
//
// Scenario: No fuzzy/edit-distance matching -- "pea" and "pear" stay distinct -- MEP-050
//   Given "pea" and "pear"
//   Then Normalize returns different keys despite being one edit apart
//
// Scenario: Filler, quantity, and authoring-artifact words strip out -- MEP-050
//   Given "handful of peas", "bags of frozen peas", "packets frozen peas",
//     "mugful frozen peas", "kilogram snow peas", "gallon peas", "pints peas",
//     "peas optional", "peas and/or", "choice of peas", "either peas",
//     "peas etc", "peas - if", "e.g. peas"
//   Then the filler words strip out and the remaining tokens fold as expected
//
// Scenario: Forward slash is a split delimiter -- MEP-050
//   Given "peas/carrots"
//   Then the tokens are "carrot" and "pea", not one glued token
//
// Scenario: All 26 confirmed mayonnaise misspellings fold to same key as "mayonnaise" -- MEP-053
//   Given the TypoAndSynonymPhraseReplacements dictionary includes all 26 confirmed misspellings
//   When CanonicalNameNormalizer processes any of them
//   Then each normalizes to the same fold-group key as "mayonnaise"
//
// Scenario: Compound-concatenation mayonnaise forms split and fold correctly -- MEP-053
//   Given "cupmayonnaise" is mapped to "cup mayonnaise" in the phrase dictionary
//   And "lightmayonnaise" is mapped to "light mayonnaise" in the phrase dictionary
//   When CanonicalNameNormalizer processes each concatenated form
//   Then the key for "cupmayonnaise" matches the key for "cup mayonnaise"
//   And the key for "lightmayonnaise" matches the key for "light mayonnaise"
//
// Scenario: Abbreviation "mayo" stays distinct from "mayonnaise" -- MEP-053
//   Given "mayo" is a legitimate abbreviation, not a typo
//   When CanonicalNameNormalizer processes "mayo"
//   Then the fold-group key for "mayo" is not the same as the key for "mayonnaise"
//
// Scenario: "mayocoba" is not modified and stays distinct from "mayonnaise" -- MEP-053
//   Given "mayocoba" is a bean variety unrelated to mayonnaise
//   When CanonicalNameNormalizer processes "mayocoba beans"
//   Then the fold-group key is distinct from "mayonnaise"
//   And the result is non-empty (the word is preserved as-is by the dictionary)
//
// Scenario: "mayorelli" stays distinct from "mayonnaise" -- MEP-053
//   Given "mayorelli" is a cheese name unrelated to mayonnaise
//   When CanonicalNameNormalizer processes "mayorelli cheese"
//   Then the fold-group key is distinct from "mayonnaise"
//
// Scenario: RequiresTypoCorrection returns true for a known typo dictionary entry -- MEP-053
//   Given the input is a string present in TypoAndSynonymPhraseReplacements (e.g. "mayonaise", "leseur")
//   When RequiresTypoCorrection is called
//   Then the method returns true for each input
//
// Scenario: RequiresTypoCorrection returns false for a correctly-spelled name -- MEP-053
//   Given the input is "mayonnaise" or "onion", neither of which appears in the typo dictionary
//   When RequiresTypoCorrection is called
//   Then the method returns false for each input
//
// Scenario: RequiresTypoCorrection returns false for a name that differs only by casing or pluralisation -- MEP-053
//   Given the input "Onions" which differs from "onion" only by capitalisation and plural suffix
//   And neither capitalisation nor plural removal is a typo-dictionary replacement
//   When RequiresTypoCorrection is called
//   Then the method returns false, because casing and pluralisation are cosmetic and do not count as typo correction
//
// Scenario: RequiresTypoCorrection returns false for null or whitespace input -- MEP-053
//   Given a null, empty, or whitespace-only string
//   When RequiresTypoCorrection is called
//   Then the method returns false without throwing

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
    public void Normalize_PreservationStateWords_StayDistinctFromGeneric()
    {
        var fresh = _normalizer.Normalize("fresh peas");
        var frozen = _normalizer.Normalize("frozen peas");
        var plain = _normalizer.Normalize("peas");

        fresh.Should().NotBe(frozen);
        fresh.Should().NotBe(plain);
        frozen.Should().NotBe(plain);
    }

    [Theory]
    [InlineData("dried peas")]
    [InlineData("cooked peas")]
    [InlineData("raw peas")]
    [InlineData("uncooked peas")]
    public void Normalize_OtherPreservationStateWords_StayDistinctFromGeneric(string input)
    {
        _normalizer.Normalize(input).Should().NotBe(_normalizer.Normalize("peas"));
    }

    [Fact]
    public void Normalize_PeaAndPear_AreNotFoldedTogether()
    {
        _normalizer.Normalize("pea").Should().NotBe(_normalizer.Normalize("pear"));
    }

    [Fact]
    public void Normalize_WordOrder_DoesNotAffectKey()
    {
        _normalizer.Normalize("black pepper").Should().Be(_normalizer.Normalize("pepper black"));
    }

    [Theory]
    [InlineData("CHOPPED  Onions", "onion")]
    [InlineData("Sliced, diced-onion", "onion")]
    [InlineData("\tdiced\tonions\t", "onion")]
    public void Normalize_MixedCaseAndPunctuation_ReducesToCleanKey(string input, string expected)
    {
        _normalizer.Normalize(input).Should().Be(expected);
    }

    // ── MEP-050: brand-name phrase stripping ──────────────────────────────

    [Theory]
    [InlineData("lesueur peas", "pea")]
    [InlineData("lesueur green peas", "green pea")]
    [InlineData("del monte peas", "pea")]
    [InlineData("campbell's pea soup", "pea soup")]
    [InlineData("birds eye sweet peas", "pea sweet")]
    [InlineData("green giant baby early peas", "baby early pea")]
    [InlineData("knorr green peas", "green pea")]
    public void Normalize_BrandNamePhrases_StripOutEntirely(string input, string expected)
    {
        _normalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_BrandWordOverlappingRealDescriptor_OnlyStripsFullPhrase()
    {
        // "green giant" (brand) strips entirely; plain "green" (color descriptor)
        // survives because only the full two-word brand phrase is a stopword.
        _normalizer.Normalize("green giant peas").Should().Be("pea");
        _normalizer.Normalize("green peas").Should().Be(_normalizer.Normalize("green pea"));
        _normalizer.Normalize("green peas").Should().NotBe(_normalizer.Normalize("pea"));
    }

    // ── MEP-050: curated typo corrections ──────────────────────────────────

    [Theory]
    [InlineData("frozed peas", "frozen peas")]
    [InlineData("spit peas", "split peas")]
    [InlineData("slit peas", "split peas")]
    [InlineData("yellow splitt peas", "yellow split peas")]
    [InlineData("earlie peas", "early peas")]
    [InlineData("pidgeaon peas", "pigeon peas")]
    [InlineData("sping peas", "spring peas")]
    [InlineData("lesuer peas", "lesueur peas")]
    [InlineData("leseur peas", "lesueur peas")]
    [InlineData("lesueuer peas", "lesueur peas")]
    public void Normalize_KnownTypo_NormalizesSameAsCorrectSpelling(string typo, string correctSpelling)
    {
        _normalizer.Normalize(typo).Should().Be(_normalizer.Normalize(correctSpelling));
    }

    // ── MEP-050: compound-word / split-word synonyms ───────────────────────

    [Fact]
    public void Normalize_ChickpeaCompoundWord_FoldsWithSplitWordForm()
    {
        _normalizer.Normalize("chickpea").Should().Be(_normalizer.Normalize("chick pea"));
        _normalizer.Normalize("chickpeas").Should().Be(_normalizer.Normalize("chick pea"));
        _normalizer.Normalize("chickpea flour").Should().Be(_normalizer.Normalize("chick pea flour"));
    }

    [Theory]
    [InlineData("blackeyed peas")]
    [InlineData("black eye peas")]
    [InlineData("back eyed peas")]
    [InlineData("blacck eyed peas")]
    [InlineData("black-eyed peas")]
    public void Normalize_BlackEyedPeaVariant_FoldsToSameKey(string input)
    {
        _normalizer.Normalize(input).Should().Be(_normalizer.Normalize("black eyed peas"));
    }

    [Fact]
    public void Normalize_BlackEyedPhrase_DoesNotDoubleTransformAlreadyCorrectSpelling()
    {
        // Regression guard: the "black eye" -> "black eyed" phrase replacement
        // must not re-match inside an already-correct "black eyed" (no word
        // boundary between "eye" and the "d" that follows it).
        _normalizer.Normalize("black eyed peas").Should().Be("black eyed pea");
    }

    // ── MEP-050: filler / quantity / authoring-artifact stopwords ──────────

    [Theory]
    [InlineData("bags of frozen peas")]
    [InlineData("packets frozen peas")]
    [InlineData("mugful frozen peas")]
    public void Normalize_FillerWordsAroundFrozenPeas_FoldToFrozenPea(string input)
    {
        _normalizer.Normalize(input).Should().Be(_normalizer.Normalize("frozen peas"));
    }

    [Theory]
    [InlineData("kilogram snow peas")]
    public void Normalize_FillerWordsAroundSnowPeas_FoldToSnowPea(string input)
    {
        _normalizer.Normalize(input).Should().Be(_normalizer.Normalize("snow peas"));
    }

    [Theory]
    [InlineData("handful of peas")]
    [InlineData("gallon peas")]
    [InlineData("pints peas")]
    [InlineData("peas optional")]
    [InlineData("choice of peas")]
    [InlineData("either peas")]
    [InlineData("peas etc")]
    [InlineData("peas - if")]
    [InlineData("e.g. peas")]
    public void Normalize_FillerWordsAroundPlainPeas_FoldToPea(string input)
    {
        _normalizer.Normalize(input).Should().Be("pea");
    }

    [Fact]
    public void Normalize_AndOrConnective_StripsOutViaSlashDelimiterAndStopwords()
    {
        _normalizer.Normalize("peas and/or").Should().Be("pea");
    }

    // ── MEP-050: forward slash as a split delimiter ────────────────────────

    [Fact]
    public void Normalize_ForwardSlash_SplitsIntoSeparateTokens()
    {
        _normalizer.Normalize("peas/carrots").Should().Be("carrot pea");
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
        _normalizer.Normalize("chopped diced sliced").Should().Be(string.Empty);
    }

    [Fact]
    public void Normalize_AllTokensAreFillerStopwords_ReturnsEmpty()
    {
        _normalizer.Normalize("handful of choice").Should().Be(string.Empty);
    }

    // ── MEP-053: mayonnaise misspellings ──────────────────────────────────────

    [Fact]
    public void Normalize_MayoAbbreviation_RemainsDistinctFromMayonnaise()
    {
        // "mayo" is a legitimate abbreviation in wide use (52 occurrences in the
        // Kaggle catalog), not a typo for "mayonnaise". It must stay its own
        // fold-group key so recipes using "mayo" stay linked to the correct
        // CanonicalIngredient.
        _normalizer.Normalize("mayo").Should().NotBe(_normalizer.Normalize("mayonnaise"));
    }

    [Fact]
    public void Normalize_MayocobaBean_RemainsDistinctFromMayonnaise()
    {
        // "mayocoba" is a bean variety (mayocoba beans) that shares a substring
        // prefix with "mayonnaise" but is an entirely different ingredient.
        // The typo dictionary must not touch it, and the result must be non-empty.
        var key = _normalizer.Normalize("mayocoba beans");

        key.Should().NotBe(_normalizer.Normalize("mayonnaise"));
        key.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("cupmayonnaise", "cup mayonnaise")]
    [InlineData("lightmayonnaise", "light mayonnaise")]
    public void Normalize_MayonnaiseConcatenation_FoldsToSameKeyAsSplitForm(string concatenated, string splitForm)
    {
        // Compound-concatenation artifacts from ingest (missing inter-word space)
        // must expand to their split-word forms before normalization so they share
        // a fold-group key with correctly-spaced variants.
        _normalizer.Normalize(concatenated).Should().Be(_normalizer.Normalize(splitForm));
    }

    [Theory]
    [InlineData("mayoaise")]
    [InlineData("mayomaise")]
    [InlineData("mayonaiese")]
    [InlineData("mayonais")]
    [InlineData("mayonaise")]        // high-frequency: 15 occurrences in catalog
    [InlineData("mayonaisse")]       // 2 occurrences in catalog
    [InlineData("mayonasie")]
    [InlineData("mayonassaise")]
    [InlineData("mayoneise")]
    [InlineData("mayonesa")]
    [InlineData("mayonese")]
    [InlineData("mayoniase")]
    [InlineData("mayoniasse")]
    [InlineData("mayonise")]
    [InlineData("mayonnaiae")]
    [InlineData("mayonnaiase")]
    [InlineData("mayonnaiie")]
    [InlineData("mayonnais")]
    [InlineData("mayonnaisee")]
    [InlineData("mayonnaisel")]
    [InlineData("mayonnase")]
    [InlineData("mayonnasie")]
    [InlineData("mayonnasise")]
    [InlineData("mayonniase")]
    [InlineData("mayonnnaise")]
    [InlineData("mayonnoise")]
    public void Normalize_MayonnaiseMisspelling_FoldsToSameKeyAsMayonnaise(string misspelling)
    {
        // Every confirmed misspelling of "mayonnaise" found in the Kaggle catalog
        // must normalize to the same fold-group key as the correctly-spelled word
        // so recipe matching works across all variants.
        _normalizer.Normalize(misspelling).Should().Be(_normalizer.Normalize("mayonnaise"));
    }

    [Fact]
    public void Normalize_MayorelliCheese_RemainsDistinctFromMayonnaise()
    {
        // "mayorelli" is a cheese name that shares a substring prefix with
        // "mayonnaise". The typo dictionary must not touch it.
        _normalizer.Normalize("mayorelli cheese").Should().NotBe(_normalizer.Normalize("mayonnaise"));
    }

    // ── MEP-053: RequiresTypoCorrection ────────────────────────────────────

    [Theory]
    [InlineData("mayonaise")]        // 9-char misspelling that started the MEP-053 investigation
    [InlineData("leseur")]           // brand misspelling also used in normalize tests above
    public void RequiresTypoCorrection_KnownTypoEntry_ReturnsTrue(string input)
    {
        // Arrange / Act / Assert
        _normalizer.RequiresTypoCorrection(input).Should().BeTrue();
    }

    [Theory]
    [InlineData("mayonnaise")]
    [InlineData("onion")]
    public void RequiresTypoCorrection_CorrectSpelling_ReturnsFalse(string input)
    {
        // Correctly-spelled names are not in the typo dictionary so the method
        // must return false without altering them.
        _normalizer.RequiresTypoCorrection(input).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RequiresTypoCorrection_NullOrWhitespace_ReturnsFalse(string? input)
    {
        // Guard: the method must not throw on null or whitespace and must return false.
        _normalizer.RequiresTypoCorrection(input).Should().BeFalse();
    }

    [Fact]
    public void RequiresTypoCorrection_PluralOrCasingVariant_ReturnsFalse()
    {
        // "Onions" differs from "onion" only by capitalisation and the plural -s
        // suffix. Neither change is a typo-dictionary replacement, so the method
        // must return false. This confirms that RequiresTypoCorrection measures only
        // substantive dictionary corrections, not cosmetic normalisation steps such
        // as lower-casing, stopword removal, or singularisation.
        _normalizer.RequiresTypoCorrection("Onions").Should().BeFalse();
    }
}
