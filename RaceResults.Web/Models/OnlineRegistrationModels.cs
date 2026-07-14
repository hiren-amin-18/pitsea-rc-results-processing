namespace RaceResults.Web.Models;

/// <summary>One parsed row from either CSV (US45). Names are as-typed at this stage (PROPER casing
/// happens at generation time so the preview shows what the CSV actually contained).</summary>
public class OnlineRegistrationRow
{
    public string SourceFile { get; set; } = string.Empty;
    public int SourceRowNumber { get; set; }
    public bool IsU18 { get; set; }
    public string Forename { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string RawGender { get; set; } = string.Empty;
    public string NormalisedGender { get; set; } = string.Empty;
    public string RawClub { get; set; } = string.Empty;
    public int? Age { get; set; }
}

/// <summary>Dry-run preview of the Online Registration generation (US45 AC3). Shown to the organiser
/// so they can resolve every unconfident club match and confirm duplicates / unrecognised genders
/// before the .xlsx is written.</summary>
public class OnlineRegistrationPreview
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }

    public int AdultCount { get; set; }
    public int U18Count { get; set; }
    public int TotalCount => AdultCount + U18Count;

    /// <summary>Parsed rows (all of them, including the ones with confident club matches).</summary>
    public List<OnlineRegistrationRow> Rows { get; set; } = new();

    /// <summary>Raw club strings from the CSVs that matched a canonical club confidently — no action needed
    /// but shown at the top of the preview for reassurance.</summary>
    public List<string> ConfidentlyMatchedClubs { get; set; } = new();

    /// <summary>Distinct raw club strings that need the organiser's decision (no confident match).</summary>
    public List<UnresolvedClub> UnresolvedClubs { get; set; } = new();

    /// <summary>(NormalisedName + Gender) that appears more than once — across files or within one.</summary>
    public List<string> Duplicates { get; set; } = new();

    /// <summary>Gender values that weren't recognised as Male/Female (blocks generation).</summary>
    public List<string> UnrecognisedGenders { get; set; } = new();

    /// <summary>Hard errors (missing required column, unreadable file, both files look U18, etc.).</summary>
    public List<string> Errors { get; set; } = new();

    public bool CanGenerate =>
        Errors.Count == 0
        && UnrecognisedGenders.Count == 0
        && TotalCount > 0;
}

/// <summary>A raw club string that didn't match confidently — the organiser picks a canonical club,
/// asks to add it as a new club, or asks to leave the raw value as typed.</summary>
public class UnresolvedClub
{
    /// <summary>The raw value as it appeared in the CSV (used as the key when the organiser posts back).</summary>
    public string Raw { get; set; } = string.Empty;
    /// <summary>Suggested canonical clubs, best first (up to 5).</summary>
    public List<ClubSuggestion> Suggestions { get; set; } = new();
    /// <summary>How many CSV rows will pick up this resolution.</summary>
    public int OccurrenceCount { get; set; }
}

public class ClubSuggestion
{
    public int ClubId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Score { get; set; }
}

/// <summary>Posted back from the preview view to actually generate the file (US45 AC3/AC9).</summary>
public class OnlineRegistrationGenerateInput
{
    public int EventId { get; set; }
    /// <summary>Serialised <see cref="OnlineRegistrationPreview"/> from the preview page — the source of
    /// truth for the rows so we don't re-parse the CSVs across the request boundary.</summary>
    public string PreviewJson { get; set; } = string.Empty;
    /// <summary>Map of raw club string → resolution instruction ("club:{id}", "add:{name}", or "leave").</summary>
    public Dictionary<string, string> ClubResolutions { get; set; } = new();
}
