namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Maps each <see cref="ClaudeModel"/> to its Anthropic API model ID and display
/// name, and resolves persisted or user-supplied values back to a <see cref="ClaudeModel"/>.
/// </summary>
public static class ClaudeModelCatalog
{
    /// <summary>The model used when no preference has been saved, or a saved value is unrecognized.</summary>
    public const ClaudeModel Default = ClaudeModel.Sonnet5;

    /// <summary>Returns the Anthropic API model ID for <paramref name="model"/> (e.g., <c>"claude-sonnet-5"</c>).</summary>
    public static string AnthropicModelId(ClaudeModel model) => model switch
    {
        ClaudeModel.Fable51 => "claude-fable-5-1",
        ClaudeModel.Haiku45 => "claude-haiku-4-5-20251001",
        ClaudeModel.Opus5 => "claude-opus-5",
        ClaudeModel.Sonnet5 => "claude-sonnet-5",
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, message: null)
    };

    /// <summary>Returns the human-readable label for <paramref name="model"/> (e.g., <c>"Sonnet 5"</c>).</summary>
    public static string DisplayName(ClaudeModel model) => model switch
    {
        ClaudeModel.Fable51 => "Fable 5.1",
        ClaudeModel.Haiku45 => "Haiku 4.5",
        ClaudeModel.Opus5 => "Opus 5",
        ClaudeModel.Sonnet5 => "Sonnet 5",
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, message: null)
    };

    /// <summary>
    /// Attempts to resolve <paramref name="value"/> (a <see cref="ClaudeModel"/> member
    /// name, case-insensitive) to a <see cref="ClaudeModel"/>. Returns false for null,
    /// empty, or unrecognized input — callers should fall back to <see cref="Default"/>
    /// rather than erroring, since a persisted value may reference a model retired in a
    /// newer app version.
    /// </summary>
    public static bool TryParse(string? value, out ClaudeModel model)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse(value, ignoreCase: true, out ClaudeModel parsed)
            && Enum.IsDefined(parsed))
        {
            model = parsed;
            return true;
        }

        model = Default;
        return false;
    }
}
