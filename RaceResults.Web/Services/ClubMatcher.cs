using System.Text;
using System.Text.RegularExpressions;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Fuzzy matcher used by the Online Registration generator (US45) to snap a raw
/// club string from the CSV to a canonical <see cref="Club"/> record. Combines token-based
/// matching with a running-club abbreviation table (RC → Running Club, AC → Athletics Club, …)
/// and a Levenshtein fallback for typos. Deactivated clubs are matched (so historic misspellings
/// still resolve) but are not offered as suggestions.</summary>
public class ClubMatcher
{
    /// <summary>Confident-match threshold: at or above this score the matcher snaps automatically.</summary>
    public const double ConfidentThreshold = 0.90;

    /// <summary>Suggestion threshold: candidates at or above this show in the preview dropdown.</summary>
    public const double SuggestionThreshold = 0.55;

    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rc"] = "runningclub",
        ["ac"] = "athleticsclub",
        ["cc"] = "cyclingclub",
        ["tri"] = "triathlon",
        ["prc"] = "pitsearunningclub",
    };

    public static ClubMatchResult Match(string raw, IReadOnlyList<Club> canonicalClubs)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new ClubMatchResult { Raw = raw ?? string.Empty };

        var rawNorm = Normalize(raw);
        var rawTokens = Tokenize(raw);
        var scored = new List<ClubMatchCandidate>();

        foreach (var club in canonicalClubs)
        {
            var canonNorm = Normalize(club.Name);
            double score;
            if (canonNorm == rawNorm)
            {
                score = 1.0;
            }
            else
            {
                var tokenScore = TokenSimilarity(rawTokens, Tokenize(club.Name));
                var editScore = LevenshteinRatio(rawNorm, canonNorm);
                score = Math.Max(tokenScore, editScore);
            }

            if (score >= SuggestionThreshold)
                scored.Add(new ClubMatchCandidate { Club = club, Score = score });
        }

        scored = scored.OrderByDescending(s => s.Score).ThenBy(s => s.Club.Name).ToList();

        var result = new ClubMatchResult
        {
            Raw = raw,
            Candidates = scored.Take(5).ToList(),
        };

        var top = scored.FirstOrDefault();
        if (top is not null && top.Score >= ConfidentThreshold)
        {
            result.IsConfident = true;
            result.ConfidentMatch = top.Club;
        }
        return result;
    }

    // ----- Normalisation -----

    /// <summary>Lowercase, strip punctuation, expand known running-club abbreviations, and remove all
    /// remaining whitespace. Produces a comparable key like "pitsearunningclub".</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var tokens = Tokenize(s);
        var sb = new StringBuilder(s.Length);
        foreach (var t in tokens) sb.Append(t);
        return sb.ToString();
    }

    /// <summary>Lowercased word tokens with punctuation stripped; known abbreviations expanded.</summary>
    public static List<string> Tokenize(string s)
    {
        if (string.IsNullOrEmpty(s)) return new List<string>();
        var lower = s.ToLowerInvariant();
        var cleaned = new StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c)) cleaned.Append(c);
            else cleaned.Append(' ');
        }
        var raw = Regex.Split(cleaned.ToString().Trim(), @"\s+")
            .Where(t => t.Length > 0)
            .ToList();

        var expanded = new List<string>(raw.Count);
        foreach (var t in raw)
        {
            if (Abbreviations.TryGetValue(t, out var full))
                expanded.Add(full);
            else
                expanded.Add(t);
        }
        return expanded;
    }

    // ----- Similarity -----

    private static double TokenSimilarity(List<string> a, List<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var setA = new HashSet<string>(a, StringComparer.Ordinal);
        var setB = new HashSet<string>(b, StringComparer.Ordinal);
        var intersect = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();
        return union == 0 ? 0 : (double)intersect / union;
    }

    private static double LevenshteinRatio(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1;
        if (a.Length == 0 || b.Length == 0) return 0;
        var maxLen = Math.Max(a.Length, b.Length);
        var distance = Levenshtein(a, b);
        return 1.0 - (double)distance / maxLen;
    }

    private static int Levenshtein(string a, string b)
    {
        var m = a.Length;
        var n = b.Length;
        var prev = new int[n + 1];
        var curr = new int[n + 1];
        for (var j = 0; j <= n; j++) prev[j] = j;
        for (var i = 1; i <= m; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= n; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[n];
    }
}

public class ClubMatchResult
{
    public string Raw { get; set; } = string.Empty;
    public bool IsConfident { get; set; }
    public Club? ConfidentMatch { get; set; }
    public List<ClubMatchCandidate> Candidates { get; set; } = new();
}

public class ClubMatchCandidate
{
    public Club Club { get; set; } = null!;
    public double Score { get; set; }
}
