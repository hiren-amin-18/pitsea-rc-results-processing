namespace RaceResults.Web.Models;

/// <summary>A persistent person who may volunteer at events (US28). Optionally linked to a <see cref="Runner"/> record;
/// many volunteers also run, some never run again, some never run at all — the link is always optional.</summary>
public class Volunteer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>"Male" / "Female" / empty. Used by US32 to aim for a mix at marshal points.</summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>Drives London Marathon ballot eligibility in US29 — non-members earn zero ballot entries.</summary>
    public bool IsClubMember { get; set; } = true;

    /// <summary>Required for First Aid roles (see <see cref="VolunteerRole.RequiresFirstAid"/>).</summary>
    public bool IsFirstAidTrained { get; set; }

    /// <summary>Optional link to the persistent runner record (US15).</summary>
    public int? RunnerId { get; set; }
    public Runner? Runner { get; set; }

    /// <summary>Deactivated volunteers are kept (history) but excluded from assignment pickers (US28 AC1).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Usual preferences (US40): pre-fill the allocate grid and the add-assignment form.
    // Per-event overrides live on the assignment / saved grid; these are the starting values. ----

    public int? DefaultPreferredRoleId { get; set; }
    public VolunteerRole? DefaultPreferredRole { get; set; }
    public bool DefaultWantsToRunAfter { get; set; }
    public bool DefaultWantsNearFinish { get; set; }
    public bool DefaultCantWalkFar { get; set; }
    public bool DefaultWantsSeated { get; set; }
    public bool DefaultWantsRaceHq { get; set; }
    public bool DefaultAnyRole { get; set; }
}
