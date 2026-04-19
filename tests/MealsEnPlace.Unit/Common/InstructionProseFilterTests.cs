// Feature: Instruction Prose Filter (MEP-026)
//
// Scenario: Simple imperative is retained
//   Given the step "Melt butter in a saucepan."
//   When InstructionProseFilter.Evaluate is called
//   Then Retained is true
//
// Scenario: Preposition-lead imperative is retained (new behavior)
//   Given the step "In a bowl, combine flour and sugar."
//   When InstructionProseFilter.Evaluate is called
//   Then Retained is true
//
// Scenario: Subordinator-lead imperative is retained (new behavior)
//   Given the step "When the mixture bubbles, remove from heat."
//   When InstructionProseFilter.Evaluate is called
//   Then Retained is true
//
// Scenario: First-person pronoun drops the step
//   Given the step "I always use salted butter here."
//   When InstructionProseFilter.Evaluate is called
//   Then Retained is false
//   And DropReason is FirstPersonPronoun
//
// Scenario: First-person pronoun anywhere in the step drops it
//   Given the step "Melt butter. My grandmother used salted."
//   When InstructionProseFilter.Evaluate is called
//   Then Retained is false
//   And DropReason is FirstPersonPronoun
//
// Scenario: Long parenthetical is stripped before word count is measured
//   Given a step with a 50-character parenthetical aside
//   And the non-parenthetical content is only 5 words
//   When InstructionProseFilter.Evaluate is called
//   Then Retained is true
//
// Scenario: Step exceeding the word limit after parenthetical strip is dropped
//   Given a step of 45 real words (no parentheticals)
//   When InstructionProseFilter.Evaluate is called
//   Then Retained is false
//   And DropReason is TooManyWords
//
// Scenario: Empty step is dropped
//   Given an empty string or whitespace-only step
//   When InstructionProseFilter.Evaluate is called
//   Then Retained is false
//   And DropReason is Empty
//
// Scenario: Contraction with a first-person pronoun is dropped
//   Given the step "I'll add the garlic next."
//   When InstructionProseFilter.Evaluate is called
//   Then Retained is false
//   And DropReason is FirstPersonPronoun
//
// Scenario: Words containing "i" as a substring are NOT treated as first-person
//   Given the step "Italian sausage goes in last."
//   When InstructionProseFilter.Evaluate is called
//   Then Retained is true
//
// Scenario: FilterRetained preserves order and text of retained steps
//   Given a mixed list of retained and dropped steps
//   When FilterRetained is called
//   Then retained steps are yielded in original order
//   And the text of each retained step is unchanged

using FluentAssertions;
using MealsEnPlace.Api.Common;

namespace MealsEnPlace.Unit.Common;

public class InstructionProseFilterTests
{
    // ── Retained: simple and preposition / subordinator leads ────────────────

    [Fact]
    public void Evaluate_SimpleImperative_IsRetained()
    {
        var result = InstructionProseFilter.Evaluate("Melt butter in a saucepan.");

        result.Retained.Should().BeTrue();
        result.DropReason.Should().BeNull();
    }

    [Fact]
    public void Evaluate_PrepositionLeadImperative_IsRetained()
    {
        // "In a bowl, combine..." starts with a preposition, not a verb.
        // Relaxed filter retains it.
        var result = InstructionProseFilter.Evaluate("In a bowl, combine flour and sugar.");

        result.Retained.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_SubordinatorLeadImperative_IsRetained()
    {
        var result = InstructionProseFilter.Evaluate("When the mixture bubbles, remove from heat.");

        result.Retained.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_GerundLeadImperative_IsRetained()
    {
        // "Using 2 teaspoons, drop..." starts with a gerund — relaxed filter retains.
        var result = InstructionProseFilter.Evaluate("Using 2 teaspoons, drop dough onto the pan.");

        result.Retained.Should().BeTrue();
    }

    // ── First-person pronoun drops ───────────────────────────────────────────

    [Fact]
    public void Evaluate_FirstPersonI_IsDropped()
    {
        var result = InstructionProseFilter.Evaluate("I always use salted butter here.");

        result.Retained.Should().BeFalse();
        result.DropReason.Should().Be(ProseFilterDropReason.FirstPersonPronoun);
    }

    [Fact]
    public void Evaluate_FirstPersonPronounAnywhereInStep_IsDropped()
    {
        var result = InstructionProseFilter.Evaluate("Melt butter. My grandmother used salted.");

        result.Retained.Should().BeFalse();
        result.DropReason.Should().Be(ProseFilterDropReason.FirstPersonPronoun);
    }

    [Fact]
    public void Evaluate_Contraction_IsDroppedAsFirstPerson()
    {
        // "I'll" contains the pronoun "I" on a word boundary.
        var result = InstructionProseFilter.Evaluate("I'll add the garlic next.");

        result.Retained.Should().BeFalse();
        result.DropReason.Should().Be(ProseFilterDropReason.FirstPersonPronoun);
    }

    [Fact]
    public void Evaluate_WordContainingIAsSubstring_IsNotTreatedAsFirstPerson()
    {
        // "Italian" contains "i" as a substring but not on a word boundary.
        var result = InstructionProseFilter.Evaluate("Italian sausage goes in last.");

        result.Retained.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_PossessiveMy_IsDropped()
    {
        var result = InstructionProseFilter.Evaluate("Add salt to my taste.");

        result.Retained.Should().BeFalse();
        result.DropReason.Should().Be(ProseFilterDropReason.FirstPersonPronoun);
    }

    // ── Parenthetical handling ───────────────────────────────────────────────

    [Fact]
    public void Evaluate_LongParentheticalStrippedBeforeWordCount_IsRetained()
    {
        // The non-parenthetical content is "Add flour slowly." (3 words).
        // The parenthetical aside is >40 chars, stripped before counting.
        var result = InstructionProseFilter.Evaluate(
            "Add flour (about 2 cups, sifted and measured the traditional way) slowly.");

        result.Retained.Should().BeTrue();
    }

    // ── Word count threshold ─────────────────────────────────────────────────

    [Fact]
    public void Evaluate_StepExceedingWordLimit_IsDropped()
    {
        // 45 words, no parentheticals, no first-person pronouns.
        var step = string.Join(' ', Enumerable.Repeat("word", 45));

        var result = InstructionProseFilter.Evaluate(step);

        result.Retained.Should().BeFalse();
        result.DropReason.Should().Be(ProseFilterDropReason.TooManyWords);
    }

    [Fact]
    public void Evaluate_StepAtWordLimitBoundary_IsRetained()
    {
        // 40 words exactly — at the boundary, retained.
        var step = string.Join(' ', Enumerable.Repeat("word", 40));

        var result = InstructionProseFilter.Evaluate(step);

        result.Retained.Should().BeTrue();
    }

    // ── Empty / whitespace handling ─────────────────────────────────────────

    [Fact]
    public void Evaluate_EmptyString_IsDropped()
    {
        var result = InstructionProseFilter.Evaluate(string.Empty);

        result.Retained.Should().BeFalse();
        result.DropReason.Should().Be(ProseFilterDropReason.Empty);
    }

    [Fact]
    public void Evaluate_WhitespaceOnly_IsDropped()
    {
        var result = InstructionProseFilter.Evaluate("   \t\n  ");

        result.Retained.Should().BeFalse();
        result.DropReason.Should().Be(ProseFilterDropReason.Empty);
    }

    [Fact]
    public void Evaluate_NullInput_IsDropped()
    {
        var result = InstructionProseFilter.Evaluate(null);

        result.Retained.Should().BeFalse();
        result.DropReason.Should().Be(ProseFilterDropReason.Empty);
    }

    // ── FilterRetained convenience method ────────────────────────────────────

    [Fact]
    public void FilterRetained_PreservesOrderAndTextOfRetainedSteps()
    {
        var steps = new[]
        {
            "Preheat oven to 350.",
            "I like to start with butter.", // dropped — first person
            "In a bowl, combine flour and sugar.", // retained
            "Bake for 30 minutes.", // retained
            "" // dropped — empty
        };

        var retained = InstructionProseFilter.FilterRetained(steps).ToList();

        retained.Should().Equal(
            "Preheat oven to 350.",
            "In a bowl, combine flour and sugar.",
            "Bake for 30 minutes.");
    }

    [Fact]
    public void FilterRetained_EmptyInputSequence_YieldsEmpty()
    {
        var retained = InstructionProseFilter.FilterRetained(Array.Empty<string>());

        retained.Should().BeEmpty();
    }

    [Fact]
    public void FilterRetained_NullInputThrows()
    {
        Action act = () => InstructionProseFilter.FilterRetained(null!).ToList();

        act.Should().Throw<ArgumentNullException>();
    }
}
