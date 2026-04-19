namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// CLI-parsed options for an ingest run. Hand-rolled rather than pulling in a
/// CLI-parsing library because the surface is small and stable.
/// </summary>
internal sealed class IngestOptions
{
    /// <summary>Path to the downloaded Kaggle recipes_data.csv file.</summary>
    public required string CsvPath { get; init; }

    /// <summary>
    /// When true, rows are read and measured but no database writes happen.
    /// Summary stats still print. Default false.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Optional cap on the number of rows processed (excluding skipped
    /// Recipes1M rows). Null means ingest every eligible row.
    /// </summary>
    public int? MaxRows { get; init; }

    /// <summary>
    /// Parses options from a raw argv array. Throws <see cref="ArgumentException"/>
    /// on unrecognized flags or missing required arguments. Returns null if
    /// --help was passed (caller should print usage and exit).
    /// </summary>
    public static IngestOptions? Parse(string[] args)
    {
        string? csvPath = null;
        var dryRun = false;
        int? maxRows = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    return null;

                case "--csv":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("--csv requires a path argument");
                    }
                    csvPath = args[++i];
                    break;

                case "--dry-run":
                    dryRun = true;
                    break;

                case "--max-rows":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("--max-rows requires an integer argument");
                    }
                    if (!int.TryParse(args[++i], out var parsed) || parsed <= 0)
                    {
                        throw new ArgumentException($"--max-rows must be a positive integer, got '{args[i]}'");
                    }
                    maxRows = parsed;
                    break;

                default:
                    throw new ArgumentException($"Unrecognized argument: '{arg}'");
            }
        }

        if (string.IsNullOrWhiteSpace(csvPath))
        {
            throw new ArgumentException("--csv <path> is required");
        }

        return new IngestOptions
        {
            CsvPath = csvPath,
            DryRun = dryRun,
            MaxRows = maxRows
        };
    }

    public static string UsageText =>
        """
        MealsEnPlace.Tools.Ingest

        Ingests recipes from the Kaggle "Recipe Dataset (over 2M)" CSV into the
        local MealsEnPlace database. Rows with source='Recipes1M' are skipped
        automatically per the MEP-025 spike decision.

        USAGE
          MealsEnPlace.Tools.Ingest --csv <path> [--dry-run] [--max-rows N]

        OPTIONS
          --csv <path>       Path to recipes_data.csv on the local machine.
                             The dataset must be downloaded from Kaggle by the
                             user; it is never committed to this repository.
          --dry-run          Read and count rows but do not write to the database.
                             Useful for validating a CSV before committing to a
                             full ingest run.
          --max-rows N       Cap the number of ingested rows (excluding skipped
                             Recipes1M rows). Useful for smoke-testing.
          --help, -h         Print this usage text.

        EXIT CODES
          0   Ingest completed, summary printed.
          1   Invalid arguments or CSV file not found.
          2   Ingest aborted due to a fatal error (connection failure, etc.).
        """;
}
