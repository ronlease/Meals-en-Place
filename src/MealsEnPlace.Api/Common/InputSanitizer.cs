using System.Text.RegularExpressions;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// Shared input sanitization utility for user-supplied strings.
/// Provides methods for cleaning strings before storage and before logging.
/// </summary>
public static partial class InputSanitizer
{
    /// <summary>
    /// Sanitizes a user-supplied string for safe storage.
    /// Trims whitespace, removes control characters (except spaces),
    /// and strips HTML tags to prevent stored XSS.
    /// Returns null if the input is null; empty string if the result is blank.
    /// </summary>
    public static string? SanitizeForStorage(string? input)
    {
        if (input is null) return null;

        // Strip HTML tags
        var noHtml = HtmlTagRegex().Replace(input, string.Empty);

        // Remove control characters (keep normal whitespace: space, tab)
        var cleaned = ControlCharRegex().Replace(noHtml, string.Empty);

        return cleaned.Trim();
    }

    /// <summary>
    /// Sanitizes a user-supplied string for safe inclusion in structured log messages.
    /// Removes carriage returns, newlines, and other control characters that could
    /// enable log injection or forge additional log lines.
    /// </summary>
    public static string SanitizeForLogging(string? input)
    {
        if (input is null) return string.Empty;

        return LogUnsafeCharRegex().Replace(input, string.Empty).Trim();
    }

    /// <summary>
    /// Sanitizes and enforces a maximum length on a user-supplied string for storage.
    /// </summary>
    public static string? SanitizeForStorage(string? input, int maxLength)
    {
        var sanitized = SanitizeForStorage(input);
        if (sanitized is null) return null;
        return sanitized.Length > maxLength ? sanitized[..maxLength] : sanitized;
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]")]
    private static partial Regex ControlCharRegex();

    [GeneratedRegex(@"[\x00-\x1F\x7F]")]
    private static partial Regex LogUnsafeCharRegex();
}
