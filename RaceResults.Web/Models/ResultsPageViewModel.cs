namespace RaceResults.Web.Models;

public class ResultsPageViewModel
{
    public List<ResultRecord> Results { get; set; } = new();
    public List<Entrant> DnfEntrants { get; set; } = new();

    /// <summary>Did Not Start entrants (US16) — managed on screen, excluded from stats and the PDF.</summary>
    public List<Entrant> DnsEntrants { get; set; } = new();

    /// <summary>Disqualified finishers (US16) — shown with their original position and reason.</summary>
    public List<ResultRecord> DsqResults { get; set; } = new();

    /// <summary>Category winners whose time beats the stored course record, awaiting confirmation (US22).</summary>
    public List<PendingCourseRecord> PendingCourseRecords { get; set; } = new();
}
