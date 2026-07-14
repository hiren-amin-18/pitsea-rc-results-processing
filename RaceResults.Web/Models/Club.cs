namespace RaceResults.Web.Models;

/// <summary>Canonical running-club name (US45). The fuzzy matcher on the Online Registration
/// generator snaps raw CSV values to one of these; the admin page lets the organiser add /
/// rename / deactivate entries. Never hard-deleted, so historic entrants keep matching.</summary>
public class Club
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Deactivated clubs are hidden from preview suggestions but never deleted, so their name
    /// is still recognised when it appears in historic entrant records.</summary>
    public bool IsActive { get; set; } = true;
}
