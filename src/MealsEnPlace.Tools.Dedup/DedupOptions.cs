namespace MealsEnPlace.Tools.Dedup;

/// <summary>
/// CLI-parsed options for a dedup run. Hand-rolled rather than pulling in a
/// CLI-parsing library because the surface is small and stable.
/// </summary>
internal sealed class DedupOptions
{
    /// <summary>
    /// When true, the resolver computes fold groups and prints them but no
    /// database writes occur. Default false.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Parses options from a raw argv array. Throws <see cref="ArgumentException"/>
    /// on unrecognized flags. Returns null if --help was passed.
    /// </summary>
    public static DedupOptions? Parse(string[] args)
    {
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    return null;

                case "--dry-run":
                    dryRun = true;
                    break;

                default:
                    throw new ArgumentException($"Unrecognized argument: '{arg}'");
            }
        }

        return new DedupOptions
        {
            DryRun = dryRun
        };
    }

    public static string UsageText =>
        """
        MealsEnPlace.Tools.Dedup

        Folds morphologically-equivalent CanonicalIngredient rows into a single
        survivor per fold group and reassigns every foreign key that pointed at
        a loser so recipe matching treats noisy NER variants ("chopped onion",
        "onions") as equivalent to the generic form ("onion"). Folded names are
        captured in the CanonicalIngredientAliases table so the fold is
        reversible and auditable. (MEP-038.)

        USAGE
          MealsEnPlace.Tools.Dedup [--dry-run]

        OPTIONS
          --dry-run          Compute and print the fold groups but do not write
                             to the database. Use this first to sanity-check
                             the fold counts on your real data before applying.
          --help, -h         Print this usage text.

        EXIT CODES
          0   Dedup completed, summary printed.
          1   Invalid arguments.
          2   Dedup aborted due to a fatal error (connection failure, etc.).
        """;
}
