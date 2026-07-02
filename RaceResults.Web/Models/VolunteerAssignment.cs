namespace RaceResults.Web.Models;

/// <summary>A volunteer assigned to a role at an event (US28). Carries both the allocation and the per-event
/// preferences that drive the US32 allocator. Double-booking (same volunteer twice at one event) is allowed
/// but warned — sometimes deliberate (e.g. Number Collection then Funnel).</summary>
public class VolunteerAssignment
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public RaceEvent? Event { get; set; }
    public int VolunteerId { get; set; }
    public Volunteer? Volunteer { get; set; }
    public int VolunteerRoleId { get; set; }
    public VolunteerRole? VolunteerRole { get; set; }

    /// <summary>Marks that this volunteer will run after their duty (counts against the role's <see cref="VolunteerRole.RunAfterCapacity"/>).</summary>
    public bool WillRunAfter { get; set; }

    /// <summary>Free-text note, e.g. marshal point sub-location.</summary>
    public string? Note { get; set; }

    /// <summary>Rostered but didn't turn up (US42). Kept on the roster for history, but excluded from
    /// stats, ballot entries, fill counts, and the allocator's season history.</summary>
    public bool IsNoShow { get; set; }

    // ---- Preferences (captured per-event by the organiser, used by US32 to draft a new roster). ----

    /// <summary>Volunteer asked for a specific role.</summary>
    public int? PreferredRoleId { get; set; }
    public VolunteerRole? PreferredRole { get; set; }

    /// <summary>Volunteer asked to run after — honoured only in roles with capacity.</summary>
    public bool WantsToRunAfter { get; set; }

    /// <summary>Volunteer prefers a Finish Area role.</summary>
    public bool WantsNearFinish { get; set; }

    /// <summary>Volunteer can only marshal at Points 1 or 2 (or Point 6 if necessary).</summary>
    public bool CantWalkFar { get; set; }

    /// <summary>Volunteer wants a seated role — Number Collection or On The Day Registration.</summary>
    public bool WantsSeated { get; set; }

    /// <summary>Bluebell 5: volunteer prefers a Race HQ role (Number Pick Up, On The Day Registration, Refreshments, Bag Drop, Car Park Marshal).</summary>
    public bool WantsRaceHq { get; set; }

    /// <summary>Volunteer has no preference; placed once specific requests are settled.</summary>
    public bool AnyRole { get; set; }
}
