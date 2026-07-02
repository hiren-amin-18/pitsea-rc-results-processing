namespace RaceResults.Web.Models;

/// <summary>Saved state of the allocate grid for one event (US40): who was ticked and their per-event
/// preference values. Restored whenever the allocate form is reopened, so Back from the draft (or a
/// revisit days later) doesn't wipe the organiser's work. One row per ticked volunteer.</summary>
public class AllocationCandidateRecord
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public RaceEvent? Event { get; set; }
    public int VolunteerId { get; set; }
    public Volunteer? Volunteer { get; set; }

    public int? PreferredRoleId { get; set; }
    public bool WantsToRunAfter { get; set; }
    public bool WantsNearFinish { get; set; }
    public bool CantWalkFar { get; set; }
    public bool WantsSeated { get; set; }
    public bool WantsRaceHq { get; set; }
    public bool AnyRole { get; set; }
}
