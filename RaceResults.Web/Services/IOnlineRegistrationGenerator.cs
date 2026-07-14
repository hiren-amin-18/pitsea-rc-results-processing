using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Turns the two registration-platform CSVs (adults + U18) into the
/// <c>&lt;event&gt; Online Registration.xlsx</c> that feeds US01 entrant upload (US45).</summary>
public interface IOnlineRegistrationGenerator
{
    /// <summary>Parse and validate the two CSVs and return a preview for the organiser to review.</summary>
    OnlineRegistrationPreview BuildPreview(int eventId, Stream? adultsCsv, Stream? u18Csv);

    /// <summary>Apply the organiser's club resolutions and produce the .xlsx bytes.</summary>
    OnlineRegistrationGenerateResult Generate(OnlineRegistrationGenerateInput input);
}

public class OnlineRegistrationGenerateResult
{
    public bool Success { get; set; }
    public byte[]? Bytes { get; set; }
    public string? FileName { get; set; }
    public List<string> Errors { get; set; } = new();
}
