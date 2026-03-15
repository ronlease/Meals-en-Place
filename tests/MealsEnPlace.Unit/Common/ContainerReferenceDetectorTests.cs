// Feature: Container Reference Detection
//
// Scenario: Detect "can" as a container reference
//   Given an ingredient string containing the word "can"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is true
//   And DetectedKeyword is "can"
//
// Scenario: Detect "jar" as a container reference
//   Given an ingredient string containing the word "jar"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is true
//   And DetectedKeyword is "jar"
//
// Scenario: Detect "box" as a container reference
//   Given an ingredient string containing the word "box"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is true
//   And DetectedKeyword is "box"
//
// Scenario: Detect "packet" as a container reference
//   Given an ingredient string containing the word "packet"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is true
//   And DetectedKeyword is "packet"
//
// Scenario: Detect "bag" as a container reference
//   Given an ingredient string containing the word "bag"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is true
//   And DetectedKeyword is "bag"
//
// Scenario: Detect "bottle" as a container reference
//   Given an ingredient string containing the word "bottle"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is true
//   And DetectedKeyword is "bottle"
//
// Scenario: Detect "carton" as a container reference
//   Given an ingredient string containing the word "carton"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is true
//   And DetectedKeyword is "carton"
//
// Scenario: Detect "tube" as a container reference
//   Given an ingredient string containing the word "tube"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is true
//   And DetectedKeyword is "tube"
//
// Scenario: Detection is case-insensitive
//   Given an ingredient string containing "CAN" in uppercase
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is true
//
// Scenario: No false positive on substring match ("pecan" contains "can")
//   Given an ingredient string "1 cup of pecans"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is false
//
// Scenario: No false positive on "tin foil" (edge case — "tin" is not in the keyword list)
//   Given an ingredient string "tin foil"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is false
//
// Scenario: No false positive on "scanner" (contains "can" as substring)
//   Given an ingredient string "scanner"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is false
//
// Scenario: No false positive on "boxing" (contains "box" as substring)
//   Given an ingredient string "boxing day turkey"
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is false
//
// Scenario: Empty string returns no detection
//   Given an empty string
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is false
//
// Scenario: Whitespace-only string returns no detection
//   Given a whitespace-only string
//   When ContainerReferenceDetector.Detect is called
//   Then IsContainerReference is false
//
// Scenario: OriginalInput is preserved in both positive and negative results
//   Given any input string
//   When ContainerReferenceDetector.Detect is called
//   Then the OriginalInput on the result equals the original input
//
// Scenario: DetectedKeyword is null on a negative result
//   Given a non-container string
//   When ContainerReferenceDetector.Detect is called
//   Then DetectedKeyword is null

using FluentAssertions;
using MealsEnPlace.Api.Common;

namespace MealsEnPlace.Unit.Common;

public class ContainerReferenceDetectorTests
{
    // ── Positive detections — all eight canonical keywords ────────────────────

    [Fact]
    public void Detect_StringContainsBag_ReturnsContainerReferenceWithKeywordBag()
    {
        // Arrange
        const string input = "1 bag of frozen peas";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
        result.DetectedKeyword.Should().Be("bag");
    }

    [Fact]
    public void Detect_StringContainsBottle_ReturnsContainerReferenceWithKeywordBottle()
    {
        // Arrange
        const string input = "1 bottle of hot sauce";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
        result.DetectedKeyword.Should().Be("bottle");
    }

    [Fact]
    public void Detect_StringContainsBox_ReturnsContainerReferenceWithKeywordBox()
    {
        // Arrange
        const string input = "1 box of pasta";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
        result.DetectedKeyword.Should().Be("box");
    }

    [Fact]
    public void Detect_StringContainsCan_ReturnsContainerReferenceWithKeywordCan()
    {
        // Arrange
        const string input = "1 can of diced tomatoes";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
        result.DetectedKeyword.Should().Be("can");
    }

    [Fact]
    public void Detect_StringContainsCarton_ReturnsContainerReferenceWithKeywordCarton()
    {
        // Arrange
        const string input = "1 carton of chicken broth";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
        result.DetectedKeyword.Should().Be("carton");
    }

    [Fact]
    public void Detect_StringContainsJar_ReturnsContainerReferenceWithKeywordJar()
    {
        // Arrange
        const string input = "1 jar of marinara sauce";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
        result.DetectedKeyword.Should().Be("jar");
    }

    [Fact]
    public void Detect_StringContainsPacket_ReturnsContainerReferenceWithKeywordPacket()
    {
        // Arrange
        const string input = "1 packet of taco seasoning";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
        result.DetectedKeyword.Should().Be("packet");
    }

    [Fact]
    public void Detect_StringContainsTube_ReturnsContainerReferenceWithKeywordTube()
    {
        // Arrange
        const string input = "1 tube of tomato paste";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
        result.DetectedKeyword.Should().Be("tube");
    }

    // ── Case-insensitivity ────────────────────────────────────────────────────

    [Fact]
    public void Detect_KeywordInUppercase_ReturnsContainerReference()
    {
        // Arrange
        const string input = "1 CAN of diced tomatoes";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
    }

    [Fact]
    public void Detect_KeywordInMixedCase_ReturnsContainerReference()
    {
        // Arrange
        const string input = "1 Jar of peanut butter";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
    }

    // ── False positive prevention — whole-word matching ───────────────────────

    [Fact]
    public void Detect_PecanContainsCanSubstring_DoesNotReturnContainerReference()
    {
        // Arrange — "pecan" contains "can" but is not a container reference
        const string input = "1 cup of pecans";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeFalse();
    }

    [Fact]
    public void Detect_ScannerContainsCanSubstring_DoesNotReturnContainerReference()
    {
        // Arrange — "scanner" contains "can" as an interior substring
        const string input = "scanner";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeFalse();
    }

    [Fact]
    public void Detect_BoxingContainsBoxSubstring_DoesNotReturnContainerReference()
    {
        // Arrange — "boxing" contains "box" but is not a container keyword
        const string input = "boxing day turkey";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeFalse();
    }

    [Fact]
    public void Detect_TinFoilDoesNotContainAnyKeyword_DoesNotReturnContainerReference()
    {
        // Arrange — "tin foil" contains "tin" but "tin" is not in the keyword list;
        // this is the mandatory edge case from QA spec item #6
        const string input = "tin foil";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeFalse();
    }

    [Fact]
    public void Detect_TubularContainsTubeSubstring_DoesNotReturnContainerReference()
    {
        // Arrange — "tubular" contains "tube" as a prefix but is not a standalone word
        const string input = "tubular pasta";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeFalse();
    }

    // ── Non-container ingredient strings ──────────────────────────────────────

    [Fact]
    public void Detect_PlainIngredientWithNoKeyword_ReturnsNoContainerReference()
    {
        // Arrange
        const string input = "2 cups of flour";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeFalse();
    }

    [Fact]
    public void Detect_IngredientWithWeight_ReturnsNoContainerReference()
    {
        // Arrange
        const string input = "200g chicken breast";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeFalse();
    }

    // ── Empty / null / whitespace inputs ─────────────────────────────────────

    [Fact]
    public void Detect_EmptyString_ReturnsNoContainerReference()
    {
        // Arrange
        const string input = "";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeFalse();
    }

    [Fact]
    public void Detect_WhitespaceOnlyString_ReturnsNoContainerReference()
    {
        // Arrange
        const string input = "   ";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeFalse();
    }

    // ── OriginalInput and DetectedKeyword on result ───────────────────────────

    [Fact]
    public void Detect_PositiveResult_OriginalInputMatchesInput()
    {
        // Arrange
        const string input = "1 can of tomatoes";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.OriginalInput.Should().Be(input);
    }

    [Fact]
    public void Detect_NegativeResult_OriginalInputMatchesInput()
    {
        // Arrange
        const string input = "2 cups of sugar";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.OriginalInput.Should().Be(input);
    }

    [Fact]
    public void Detect_NegativeResult_DetectedKeywordIsNull()
    {
        // Arrange
        const string input = "2 cups of sugar";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.DetectedKeyword.Should().BeNull();
    }

    // ── Keyword at start and end of string ───────────────────────────────────

    [Fact]
    public void Detect_KeywordAtStartOfString_ReturnsContainerReference()
    {
        // Arrange — keyword "can" appears at position 0
        const string input = "can of chickpeas";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
        result.DetectedKeyword.Should().Be("can");
    }

    [Fact]
    public void Detect_KeywordAtEndOfString_ReturnsContainerReference()
    {
        // Arrange — keyword "bag" appears at the end with no trailing characters
        const string input = "frozen peas bag";

        // Act
        var result = ContainerReferenceDetector.Detect(input);

        // Assert
        result.IsContainerReference.Should().BeTrue();
        result.DetectedKeyword.Should().Be("bag");
    }
}
