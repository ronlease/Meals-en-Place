// Feature: NER Token Normalization at Ingest Time
//
// Scenario: Leading and trailing punctuation and brackets are stripped
//   Given a Kaggle NER token "apple ["
//   When the normalization step runs
//   Then the normalized value is "apple"
//   And the CanonicalIngredient row is stored with name "apple"
//
// Scenario: Trailing punctuation variants collapse to the base name
//   Given Kaggle NER tokens "apple.", "apple/", and "apple"
//   When each token is normalized
//   Then all three produce "apple"
//
// Scenario: Internal whitespace is collapsed
//   Given a Kaggle NER token "  red   bell   pepper  "
//   When the normalization step runs
//   Then the normalized value is "red bell pepper"
//
// Scenario: Trailing connective is stripped rather than rejecting the token
//   Given Kaggle NER tokens "apple and" and "apple add" and "apple and or"
//   When the normalization step runs
//   Then all three normalize to "apple"
//
// Scenario: Token normalizing to empty is rejected
//   Given a Kaggle NER token consisting only of punctuation (e.g., "[", "//")
//   When the normalization step runs
//   Then the token is rejected with EmptyAfterCleanup
//
// Scenario: Token containing no letters is rejected
//   Given a Kaggle NER token "1/2" or "3.5"
//   When the normalization step runs
//   Then the token is rejected with NoLetters
//
// Scenario: Stopword-only token is rejected
//   Given a Kaggle NER token from the stopword set (a, add, an, and, for, of, or, plus, the, to, with)
//   When the normalization step runs
//   Then the token is rejected with StopwordsOnly
//   Note: StripTrailingStopwords never removes the last remaining word, so a lone stopword
//   survives to rule 7 (StopwordsOnly) rather than collapsing to empty (EmptyAfterCleanup).
//   A multi-word all-stopword token such as "of the" has its trailing "the" stripped by
//   rule 4, leaving "of" as a single-word token that rule 7 rejects as StopwordsOnly.
//
// Scenario: Leading stopword is NOT stripped
//   Given a NER token "the apple"
//   When the normalization step runs
//   Then the normalized value is "the apple"
//   Note: only trailing connectives are removed; a leading stopword is preserved as part
//   of the ingredient name.
//
// Scenario: Bracketed single-word stopword rejected as StopwordsOnly
//   Given a NER token "(and)"
//   When the normalization step runs
//   Then the edge brackets are stripped leaving "and"
//   And the token is rejected with StopwordsOnly (not EmptyAfterCleanup)
//   Note: StripTrailingStopwords never strips the last word, so "and" reaches rule 7.
//
// Scenario: Mixed-case trailing stopword stripped case-insensitively
//   Given a NER token "Apple AND"
//   When the normalization step runs
//   Then the normalized value is "Apple"
//
// Scenario: Token whose letters are only in a trailing stopword is rejected as NoLetters
//   Given a NER token "12 and"
//   When the normalization step runs
//   Then the trailing "and" is stripped leaving "12"
//   And the token is rejected with NoLetters
//
// Scenario: Digits and punctuation with internal whitespace are rejected as NoLetters
//   Given a NER token "1 / 2"
//   When the normalization step runs
//   Then the token is rejected with NoLetters
//
// Scenario: Non-ASCII letters survive intact
//   Given NER tokens "jalapeño" and "crème fraîche"
//   When the normalization step runs
//   Then both tokens are accepted with their accented characters preserved
//   Note: char.IsLetter is Unicode-aware and accepts accented characters.
//
// Scenario: Whitespace-only token is rejected as EmptyAfterCleanup
//   Given a NER token containing only spaces or tabs
//   When the normalization step runs
//   Then the token is rejected with EmptyAfterCleanup
//
// Scenario: Leading and trailing apostrophes are NOT stripped (rule gap)
//   Given a NER token "'apple'"
//   When the normalization step runs
//   Then the normalized value is "apple" (edge apostrophes stripped)
//   Note: only the edges are scanned, so an internal apostrophe (confectioners' sugar)
//   is preserved while a wrapping apostrophe is treated as noise like any other punctuation.
//
// Scenario: Internal apostrophe survives
//   Given a NER token "confectioners' sugar"
//   When the normalization step runs
//   Then the normalized value is "confectioners' sugar" unchanged
//
// Scenario: Leading hyphen is stripped; internal hyphen is kept
//   Given NER tokens "-apple" and "apple-almond filling"
//   When the normalization step runs
//   Then "-apple" normalizes to "apple"
//   And "apple-almond filling" normalizes to "apple-almond filling"
//
// Scenario: Normalization is idempotent
//   Given an already-normalized token
//   When Normalize is called again on the result
//   Then the output is identical to the first call's output
//
// Non-unit-testable scenarios (require a real Kaggle CSV and a running Postgres database):
//   - Full re-ingest produces no punctuation-fragment ingredient names
//   - Full database reset procedure is documented in the README
//   - README warns against running the ingest tool twice without resetting

using FluentAssertions;
using MealsEnPlace.Tools.Ingest;

namespace MealsEnPlace.Unit.Tools.Ingest;

public class NerTokenNormalizerTests
{
    // ── Five apple variants ───────────────────────────────────────────────────

    [Theory]
    [InlineData("apple")]
    [InlineData("apple [")]
    [InlineData("apple.")]
    [InlineData("apple/")]
    [InlineData("apple add")]
    public void Normalize_AllAppleVariants_NormalizeToApple(string rawToken)
    {
        var result = NerTokenNormalizer.Normalize(rawToken);

        result.IsRejected.Should().BeFalse();
        result.NormalizedValue.Should().Be("apple");
    }

    // ── Trailing compound connective stripped iteratively ─────────────────────

    [Fact]
    public void Normalize_AppleAndOr_NormalizesToApple()
    {
        var result = NerTokenNormalizer.Normalize("apple and or");

        result.IsRejected.Should().BeFalse();
        result.NormalizedValue.Should().Be("apple");
    }

    // ── Apostrophe inside a token survives ────────────────────────────────────

    [Fact]
    public void Normalize_ApostropheInsideToken_SurvivesIntact()
    {
        var result = NerTokenNormalizer.Normalize("confectioners' sugar");

        result.IsRejected.Should().BeFalse();
        result.NormalizedValue.Should().Be("confectioners' sugar");
    }

    // ── Bracketed single-word stopword rejected as StopwordsOnly ─────────────
    //
    // "(and)" edge-strips to "and".  StripTrailingStopwords never removes the
    // last remaining word, so "and" reaches rule 7 and is rejected as
    // StopwordsOnly — not collapsed to empty and misreported as EmptyAfterCleanup.

    [Theory]
    [InlineData("(and)")]
    [InlineData("(a)")]
    [InlineData("[with]")]
    public void Normalize_BracketedStopword_RejectedAsStopwordsOnly(string rawToken)
    {
        var result = NerTokenNormalizer.Normalize(rawToken);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Be(NerTokenRejectionReason.StopwordsOnly);
    }

    // ── Digits and punctuation with internal whitespace → NoLetters ───────────
    //
    // "1 / 2" retains its edge digits; "/" is internal and not a stopword,
    // so the token survives stripping but contains no alphabetic character.

    [Fact]
    public void Normalize_DigitsAndPunctuationWithSpaces_RejectedAsNoLetters()
    {
        var result = NerTokenNormalizer.Normalize("1 / 2");

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Be(NerTokenRejectionReason.NoLetters);
    }

    // ── Idempotence across 20 varied tokens ───────────────────────────────────

    [Theory]
    [InlineData("apple")]
    [InlineData("apple [")]
    [InlineData("apple.")]
    [InlineData("confectioners' sugar")]
    [InlineData("  red   bell   pepper  ")]
    [InlineData("-apple")]
    [InlineData("apple-almond filling")]
    [InlineData("jalapeño")]
    [InlineData("crème fraîche")]
    [InlineData("olive oil")]
    [InlineData("the apple")]
    [InlineData("Apple AND")]
    [InlineData("brown sugar, packed")]
    [InlineData("all-purpose flour")]
    [InlineData("low-sodium soy sauce")]
    [InlineData("fresh ground pepper")]
    [InlineData("unsalted butter")]
    [InlineData("apple add")]
    [InlineData("'apple'")]
    [InlineData("apple and or")]
    public void Normalize_Idempotent_SecondCallProducesSameOutput(string rawToken)
    {
        var first = NerTokenNormalizer.Normalize(rawToken);
        if (first.IsRejected)
        {
            // Rejected tokens have no normalized value to re-normalize; skip.
            return;
        }

        var second = NerTokenNormalizer.Normalize(first.NormalizedValue!);

        second.IsRejected.Should().BeFalse();
        second.NormalizedValue.Should().Be(first.NormalizedValue);
    }

    // ── Hyphen handling ───────────────────────────────────────────────────────

    [Fact]
    public void Normalize_InternalHyphenPreserved_LeadingHyphenStripped()
    {
        var internalHyphenResult = NerTokenNormalizer.Normalize("apple-almond filling");
        var leadingHyphenResult = NerTokenNormalizer.Normalize("-apple");

        internalHyphenResult.IsRejected.Should().BeFalse();
        internalHyphenResult.NormalizedValue.Should().Be("apple-almond filling");

        leadingHyphenResult.IsRejected.Should().BeFalse();
        leadingHyphenResult.NormalizedValue.Should().Be("apple");
    }

    // ── Internal whitespace collapsed ─────────────────────────────────────────

    [Fact]
    public void Normalize_InternalWhitespace_CollapsedToSingleSpaces()
    {
        var result = NerTokenNormalizer.Normalize("  red   bell   pepper  ");

        result.IsRejected.Should().BeFalse();
        result.NormalizedValue.Should().Be("red bell pepper");
    }

    // ── Apostrophes at the edge are NOT stripped (rule gap) ───────────────────
    //
    // Edge scanning stops at the first letter or digit, so an internal apostrophe
    // ("confectioners' sugar") survives while a wrapping one ("'apple'") is stripped.

    [Fact]
    public void Normalize_LeadingAndTrailingApostrophes_ApostrophesStripped()
    {
        var result = NerTokenNormalizer.Normalize("'apple'");

        result.IsRejected.Should().BeFalse();
        result.NormalizedValue.Should().Be("apple");
    }

    // ── Leading stopword is NOT stripped ─────────────────────────────────────
    //
    // Only trailing connectives are removed iteratively.  A leading stopword is
    // preserved as part of the ingredient name.

    [Fact]
    public void Normalize_LeadingStopword_NotStripped()
    {
        var result = NerTokenNormalizer.Normalize("the apple");

        result.IsRejected.Should().BeFalse();
        result.NormalizedValue.Should().Be("the apple");
    }

    // ── Letters only in trailing stopword → NoLetters after stripping ─────────
    //
    // "12 and": rule 4 strips the trailing "and", leaving "12".  Rule 6 then
    // rejects "12" as NoLetters because no alphabetic character remains.

    [Fact]
    public void Normalize_LettersOnlyInTrailingStopword_RejectedAsNoLettersAfterStripping()
    {
        var result = NerTokenNormalizer.Normalize("12 and");

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Be(NerTokenRejectionReason.NoLetters);
    }

    // ── Mixed-case trailing stopword stripped case-insensitively ─────────────

    [Fact]
    public void Normalize_MixedCaseTrailingStopword_StrippedCaseInsensitively()
    {
        var result = NerTokenNormalizer.Normalize("Apple AND");

        result.IsRejected.Should().BeFalse();
        result.NormalizedValue.Should().Be("Apple");
    }

    // ── No-letter tokens rejected ─────────────────────────────────────────────

    [Theory]
    [InlineData("1/2")]
    [InlineData("3.5")]
    public void Normalize_NoLetters_RejectedWithNoLettersReason(string rawToken)
    {
        var result = NerTokenNormalizer.Normalize(rawToken);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Be(NerTokenRejectionReason.NoLetters);
    }

    // ── Non-ASCII letters survive ─────────────────────────────────────────────
    //
    // char.IsLetter is Unicode-aware: accented characters such as ñ and è pass
    // the letter check and are preserved through all normalization steps.

    [Theory]
    [InlineData("jalapeño")]
    [InlineData("crème fraîche")]
    public void Normalize_NonAsciiLetters_SurviveIntact(string rawToken)
    {
        var result = NerTokenNormalizer.Normalize(rawToken);

        result.IsRejected.Should().BeFalse();
        result.NormalizedValue.Should().Be(rawToken);
    }

    // ── Punctuation-only tokens rejected as empty ─────────────────────────────

    [Theory]
    [InlineData("[")]
    [InlineData("//")]
    public void Normalize_PunctuationOnly_RejectedAsEmptyAfterCleanup(string rawToken)
    {
        var result = NerTokenNormalizer.Normalize(rawToken);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Be(NerTokenRejectionReason.EmptyAfterCleanup);
    }

    // ── Stopword-only tokens rejected ─────────────────────────────────────────

    [Theory]
    [InlineData("and")]
    [InlineData("a")]
    [InlineData("of the")]
    [InlineData("for")]
    [InlineData("with")]
    public void Normalize_StopwordsOnly_IsRejected(string rawToken)
    {
        var result = NerTokenNormalizer.Normalize(rawToken);

        result.IsRejected.Should().BeTrue();
    }

    // ── Stopword-only tokens rejected with StopwordsOnly reason ──────────────
    //
    // All eleven stopwords from the backlog spec are covered: a, add, an, and, for,
    // of, or, plus, the, to, with.  The multi-word "of the" has its trailing "the"
    // stripped by rule 4, leaving "of" as a single-word stopword that rule 7 rejects
    // as StopwordsOnly.  StripTrailingStopwords never strips the last remaining word,
    // so a single-word stopword like "and" is not consumed by rule 4; it reaches
    // rule 7 and is correctly rejected as StopwordsOnly (not EmptyAfterCleanup).

    [Theory]
    [InlineData("a")]
    [InlineData("add")]
    [InlineData("an")]
    [InlineData("and")]
    [InlineData("for")]
    [InlineData("of")]
    [InlineData("of the")]
    [InlineData("or")]
    [InlineData("plus")]
    [InlineData("the")]
    [InlineData("to")]
    [InlineData("with")]
    public void Normalize_StopwordsOnly_RejectedAsStopwordsOnly(string rawToken)
    {
        var result = NerTokenNormalizer.Normalize(rawToken);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Be(NerTokenRejectionReason.StopwordsOnly);
    }

    // ── Whitespace-only tokens rejected as empty ──────────────────────────────

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Normalize_WhitespaceOnly_RejectedAsEmptyAfterCleanup(string rawToken)
    {
        var result = NerTokenNormalizer.Normalize(rawToken);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Be(NerTokenRejectionReason.EmptyAfterCleanup);
    }
}
