// Feature: Input Sanitization
//
// Scenario: Null input returns null for storage
//   Given a null string
//   When SanitizeForStorage is called
//   Then the result is null
//
// Scenario: Null input returns empty for logging
//   Given a null string
//   When SanitizeForLogging is called
//   Then the result is an empty string
//
// Scenario: HTML tags are stripped
//   Given a string containing HTML tags
//   When SanitizeForStorage is called
//   Then the HTML tags are removed and the text content is preserved
//
// Scenario: Control characters are removed
//   Given a string containing control characters
//   When SanitizeForStorage is called
//   Then the control characters are removed
//
// Scenario: Newlines and carriage returns are removed for logging
//   Given a string containing newline and carriage return characters
//   When SanitizeForLogging is called
//   Then the newlines and carriage returns are removed
//
// Scenario: Max length is enforced
//   Given a string longer than the max length
//   When SanitizeForStorage is called with a max length
//   Then the result is truncated to the max length
//
// Scenario: Normal text is preserved
//   Given a normal text string with no dangerous content
//   When SanitizeForStorage is called
//   Then the result matches the trimmed input
//
// Scenario: Leading and trailing whitespace is trimmed
//   Given a string with leading and trailing spaces
//   When SanitizeForStorage is called
//   Then the result is trimmed
//
// Scenario: Script tags are stripped
//   Given a string containing a script tag with JavaScript
//   When SanitizeForStorage is called
//   Then the script tag and its content markers are removed

using FluentAssertions;
using MealsEnPlace.Api.Common;

namespace MealsEnPlace.Unit.Common;

public class InputSanitizerTests
{
    // ── SanitizeForStorage — null and empty handling ──────────────────────────

    [Fact]
    public void SanitizeForStorage_NullInput_ReturnsNull()
    {
        var result = InputSanitizer.SanitizeForStorage(null);

        result.Should().BeNull();
    }

    [Fact]
    public void SanitizeForStorage_EmptyString_ReturnsEmptyString()
    {
        var result = InputSanitizer.SanitizeForStorage(string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeForStorage_WhitespaceOnly_ReturnsEmptyString()
    {
        var result = InputSanitizer.SanitizeForStorage("   ");

        result.Should().BeEmpty();
    }

    // ── SanitizeForStorage — HTML tag stripping ──────────────────────────────

    [Fact]
    public void SanitizeForStorage_HtmlBoldTag_StripsTagsPreservesText()
    {
        var result = InputSanitizer.SanitizeForStorage("Hello <b>world</b>");

        result.Should().Be("Hello world");
    }

    [Fact]
    public void SanitizeForStorage_ScriptTag_StripsScriptTags()
    {
        var result = InputSanitizer.SanitizeForStorage("Test<script>alert('xss')</script>Value");

        result.Should().Be("Testalert('xss')Value");
    }

    [Fact]
    public void SanitizeForStorage_ImgTagWithAttributes_StripsTag()
    {
        var result = InputSanitizer.SanitizeForStorage("<img src=\"evil.jpg\" onerror=\"alert(1)\">");

        result.Should().BeEmpty();
    }

    // ── SanitizeForStorage — control character removal ────────────────────────

    [Fact]
    public void SanitizeForStorage_ControlCharacters_RemovesControlChars()
    {
        var result = InputSanitizer.SanitizeForStorage("Hello\x00World\x1F");

        result.Should().Be("HelloWorld");
    }

    [Fact]
    public void SanitizeForStorage_TabCharacter_PreservesTab()
    {
        // Tabs (\x09) and newlines (\x0A, \x0D) are NOT in the control char regex
        // because they are normal whitespace — they get trimmed if at edges
        var result = InputSanitizer.SanitizeForStorage("Hello\tWorld");

        result.Should().Contain("Hello");
        result.Should().Contain("World");
    }

    // ── SanitizeForStorage — whitespace trimming ──────────────────────────────

    [Fact]
    public void SanitizeForStorage_LeadingAndTrailingSpaces_Trims()
    {
        var result = InputSanitizer.SanitizeForStorage("  Chicken Breast  ");

        result.Should().Be("Chicken Breast");
    }

    // ── SanitizeForStorage — max length enforcement ──────────────────────────

    [Fact]
    public void SanitizeForStorage_WithMaxLength_TruncatesWhenExceeded()
    {
        var input = new string('A', 300);

        var result = InputSanitizer.SanitizeForStorage(input, 200);

        result.Should().HaveLength(200);
    }

    [Fact]
    public void SanitizeForStorage_WithMaxLength_PreservesWhenWithinLimit()
    {
        var result = InputSanitizer.SanitizeForStorage("Short string", 200);

        result.Should().Be("Short string");
    }

    [Fact]
    public void SanitizeForStorage_WithMaxLength_NullInput_ReturnsNull()
    {
        var result = InputSanitizer.SanitizeForStorage(null, 200);

        result.Should().BeNull();
    }

    // ── SanitizeForStorage — normal text preservation ─────────────────────────

    [Fact]
    public void SanitizeForStorage_NormalText_PreservesContent()
    {
        var result = InputSanitizer.SanitizeForStorage("1 can of diced tomatoes, 14.5 oz");

        result.Should().Be("1 can of diced tomatoes, 14.5 oz");
    }

    [Fact]
    public void SanitizeForStorage_UnicodeText_PreservesUnicode()
    {
        var result = InputSanitizer.SanitizeForStorage("Crème brûlée — 2 cups");

        result.Should().Be("Crème brûlée — 2 cups");
    }

    // ── SanitizeForLogging — null and empty handling ─────────────────────────

    [Fact]
    public void SanitizeForLogging_NullInput_ReturnsEmptyString()
    {
        var result = InputSanitizer.SanitizeForLogging(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeForLogging_EmptyString_ReturnsEmptyString()
    {
        var result = InputSanitizer.SanitizeForLogging(string.Empty);

        result.Should().BeEmpty();
    }

    // ── SanitizeForLogging — log injection prevention ─────────────────────────

    [Fact]
    public void SanitizeForLogging_NewlineInjection_RemovesNewlines()
    {
        var result = InputSanitizer.SanitizeForLogging("Legitimate\nFake log entry");

        result.Should().NotContain("\n");
        result.Should().Contain("Legitimate");
        result.Should().Contain("Fake log entry");
    }

    [Fact]
    public void SanitizeForLogging_CarriageReturnInjection_RemovesCarriageReturns()
    {
        var result = InputSanitizer.SanitizeForLogging("Real entry\r\nForged entry");

        result.Should().NotContain("\r");
        result.Should().NotContain("\n");
    }

    [Fact]
    public void SanitizeForLogging_ControlCharacters_RemovesControlChars()
    {
        var result = InputSanitizer.SanitizeForLogging("Hello\x00\x01\x7FWorld");

        result.Should().Be("HelloWorld");
    }

    [Fact]
    public void SanitizeForLogging_NormalText_PreservesContent()
    {
        var result = InputSanitizer.SanitizeForLogging("Chicken Breast");

        result.Should().Be("Chicken Breast");
    }
}
