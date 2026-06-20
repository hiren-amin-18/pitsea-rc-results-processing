namespace RaceResults.Web.Models;

public class RaceStats
{
    public int TotalMales { get; set; }
    public int TotalFemales { get; set; }
    public int TotalMalesU18 { get; set; }
    public int TotalFemalesU18 { get; set; }
    public int TotalMalesUnaffiliatedExcludingU18 { get; set; }
    public int TotalFemalesUnaffiliatedExcludingU18 { get; set; }

    // Bluebell-only splits (US33): U18 doesn't apply; the meaningful breakdown is vet vs non-vet.
    public int TotalMalesVet { get; set; }
    public int TotalFemalesVet { get; set; }
    public int TotalMalesNonVet { get; set; }
    public int TotalFemalesNonVet { get; set; }
}
