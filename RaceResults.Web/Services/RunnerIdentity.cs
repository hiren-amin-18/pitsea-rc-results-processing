namespace RaceResults.Web.Services;

/// <summary>
/// Shared runner-identity rules (US15): a single source of truth for the normalised name+club key
/// and for near-match detection, reused by upload matching, the registry service, and the startup backfill.
/// </summary>
public static class RunnerIdentity
{
    /// <summary>Normalised matching key: alphanumerics only, case-insensitive, name and club combined.</summary>
    public static string NormalizeKey(string? name, string? club) =>
        $"{NormalizePart(name)}|{NormalizePart(club)}";

    /// <summary>Normalised name only (alphanumerics, lower-case) — used for near-match comparisons.</summary>
    public static string NormalizeName(string? name) => NormalizePart(name);

    private static string NormalizePart(string? value) =>
        new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    /// <summary>Levenshtein edit distance between two strings (used to flag likely typos).</summary>
    public static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
