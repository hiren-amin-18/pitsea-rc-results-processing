namespace RaceResults.Web.Models;

/// <summary>Allow-list entry for a restricted <see cref="VolunteerRole"/> (US28). Empty by default for seeded
/// restricted roles — the organiser fills the list when Hiren / Michael / etc. are added as volunteers.</summary>
public class VolunteerRoleEligibility
{
    public int Id { get; set; }
    public int VolunteerRoleId { get; set; }
    public VolunteerRole? VolunteerRole { get; set; }
    public int VolunteerId { get; set; }
    public Volunteer? Volunteer { get; set; }
}
