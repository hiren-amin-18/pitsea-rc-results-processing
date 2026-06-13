namespace RaceResults.Web.Models;

/// <summary>An entrant's finishing status (US16). The default for someone with no finish row is DNF.</summary>
public enum FinishStatus
{
    /// <summary>Has (or is expected to have) a finish position — the default.</summary>
    Finished = 0,

    /// <summary>Did Not Start — registered but never started; excluded from DNF list, stats, and the PDF.</summary>
    DidNotStart = 1,

    /// <summary>Did Not Finish — started but did not finish; listed in the DNF section.</summary>
    DidNotFinish = 2,

    /// <summary>Disqualified — finished but disqualified; removed from results, positions below close up.</summary>
    Disqualified = 3
}
