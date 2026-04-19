using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;

namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// Streams rows from the Kaggle recipes_data.csv file with RFC 4180-correct
/// CSV parsing (via CsvHelper), skipping rows whose source column is
/// "Recipes1M" per the MEP-025 decision to respect MIT's access restriction
/// on the underlying Recipe1M+ subset.
/// <para>
/// The CSV is ~2.3 GB uncompressed; this reader is a pull-based enumerable
/// that never holds more than one row in memory.
/// </para>
/// </summary>
internal static class KaggleRowReader
{
    /// <summary>The source label for MIT-gated Recipe1M+ rows to skip on ingest.</summary>
    public const string ExcludedSourceLabel = "Recipes1M";

    /// <summary>
    /// Streams eligible rows from the given CSV path. Skipped Recipes1M rows
    /// are not yielded; callers can distinguish skip-count via the returned
    /// <see cref="StreamResult.SkippedCount"/> once enumeration completes.
    /// </summary>
    /// <param name="csvPath">Absolute or relative path to the CSV file.</param>
    /// <returns>
    /// A <see cref="StreamResult"/> wrapping the enumerable and post-iteration
    /// counters.
    /// </returns>
    public static StreamResult Stream(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"CSV file not found: {csvPath}", csvPath);
        }

        var counters = new StreamCounters();

        return new StreamResult(StreamInternal(csvPath, counters), counters);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static IEnumerable<KaggleRow> StreamInternal(string csvPath, StreamCounters counters)
    {
        using var reader = new StreamReader(csvPath);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            // Ingest is resilient to malformed rows -- we count them and carry on.
            BadDataFound = null,
            MissingFieldFound = null
        };

        using var csv = new CsvReader(reader, config);
        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            counters.TotalRowsRead++;

            var source = csv.GetField("source") ?? string.Empty;

            if (source == ExcludedSourceLabel)
            {
                counters.SkippedRecipes1M++;
                continue;
            }

            KaggleRow? row;
            try
            {
                row = new KaggleRow
                {
                    Directions = ParseJsonStringArray(csv.GetField("directions")),
                    Ingredients = ParseJsonStringArray(csv.GetField("ingredients")),
                    Link = csv.GetField("link") ?? string.Empty,
                    Ner = ParseJsonStringArray(csv.GetField("NER")),
                    Source = source,
                    Site = csv.GetField("site") ?? string.Empty,
                    Title = csv.GetField("title") ?? string.Empty
                };
            }
            catch (JsonException)
            {
                counters.MalformedRowsSkipped++;
                continue;
            }

            yield return row;
        }
    }

    /// <summary>
    /// Parses a CSV field whose content is a JSON-style string array, e.g.
    /// <c>["1 cup flour", "2 tsp salt"]</c>. After CsvHelper has un-escaped
    /// the outer CSV quoting, the field content is valid JSON in this dataset.
    /// </summary>
    private static IReadOnlyList<string> ParseJsonStringArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        // The dataset embeds double-quoted strings inside double-quoted CSV
        // fields, which CsvHelper already unescapes for us. The result is a
        // JSON array of strings.
        var parsed = JsonSerializer.Deserialize<string[]>(raw);
        return parsed ?? Array.Empty<string>();
    }
}

/// <summary>
/// Wraps the row enumerable with post-iteration counters so callers can report
/// stream-level statistics (total rows read, skipped, malformed).
/// </summary>
internal sealed class StreamResult
{
    public StreamResult(IEnumerable<KaggleRow> rows, StreamCounters counters)
    {
        Counters = counters;
        Rows = rows;
    }

    public StreamCounters Counters { get; }

    public IEnumerable<KaggleRow> Rows { get; }
}

/// <summary>
/// Mutable counter bag updated by <see cref="KaggleRowReader.Stream"/> during
/// enumeration. Read after enumeration completes.
/// </summary>
internal sealed class StreamCounters
{
    public int MalformedRowsSkipped { get; set; }

    public int SkippedRecipes1M { get; set; }

    public int TotalRowsRead { get; set; }
}
