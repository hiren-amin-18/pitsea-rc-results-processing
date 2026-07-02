using System.ComponentModel.DataAnnotations;

namespace RaceResults.Web.Models;

public class VolunteerInput
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>"Male", "Female", or empty (unspecified).</summary>
    public string Gender { get; set; } = string.Empty;

    public bool IsClubMember { get; set; } = true;
    public bool IsFirstAidTrained { get; set; }
    public int? RunnerId { get; set; }

    // Usual preferences (US40) — pre-fill the allocate grid; per-event overrides never write back here.
    public int? DefaultPreferredRoleId { get; set; }
    public bool DefaultWantsToRunAfter { get; set; }
    public bool DefaultWantsNearFinish { get; set; }
    public bool DefaultCantWalkFar { get; set; }
    public bool DefaultWantsSeated { get; set; }
    public bool DefaultWantsRaceHq { get; set; }
    public bool DefaultAnyRole { get; set; }
}

public class VolunteerListItem
{
    public Volunteer Volunteer { get; set; } = null!;
    public string? RunnerName { get; set; }

    /// <summary>Assignments actually worked — no-shows excluded (US42/US43).</summary>
    public int AssignmentCount { get; set; }

    /// <summary>True when any assignment exists at all (including no-shows) — gates permanent deletion,
    /// which must refuse for anyone with history.</summary>
    public bool HasAnyAssignments { get; set; }

    /// <summary>Most recent event actually worked (no-shows excluded); null for never-assigned (US43).</summary>
    public DateTime? LastVolunteeredDate { get; set; }
    public string? LastVolunteeredEventName { get; set; }
}

public class VolunteerRoleInput
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public RoleCategory Category { get; set; }
    public EventType EventType { get; set; } = EventType.CrownToCrown;

    [Range(0, 50)] public int DefaultCount { get; set; }
    [Range(0, 50)] public int MinCount { get; set; }
    [Range(0, 50)] public int MaxCount { get; set; }

    public bool IsOptional { get; set; }
    public bool IsGenericPreference { get; set; }
    [Range(0, 50)] public int RunAfterCapacity { get; set; }
    public bool RequiresFirstAid { get; set; }
    public bool HasEligibilityRestriction { get; set; }
    public int? PrePlacedVolunteerId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Selected volunteer IDs for the role's eligibility allow-list (used by restricted roles).</summary>
    public List<int> EligibleVolunteerIds { get; set; } = new();
}

public class VolunteerAssignmentInput
{
    public int Id { get; set; }

    [Required] public int EventId { get; set; }
    [Required] public int VolunteerId { get; set; }
    [Required] public int VolunteerRoleId { get; set; }

    public bool WillRunAfter { get; set; }
    public string? Note { get; set; }

    public int? PreferredRoleId { get; set; }
    public bool WantsToRunAfter { get; set; }
    public bool WantsNearFinish { get; set; }
    public bool CantWalkFar { get; set; }
    public bool WantsSeated { get; set; }
    public bool WantsRaceHq { get; set; }
    public bool AnyRole { get; set; }
}

public class RosterRoleRow
{
    public VolunteerRole Role { get; set; } = null!;
    public List<RosterAssignmentRow> Assignments { get; set; } = new();
    /// <summary>People actually covering the role — no-shows (US42) don't count towards the complement.</summary>
    public int AssignedCount => Assignments.Count(a => !a.Assignment.IsNoShow);
    public int NoShowCount => Assignments.Count(a => a.Assignment.IsNoShow);
    public int DefaultCount => Role.DefaultCount;
    public int Shortfall => Math.Max(0, Role.MinCount - AssignedCount);
    public int Excess => Math.Max(0, AssignedCount - Role.MaxCount);
    public bool IsUnderComplement => AssignedCount < Role.MinCount;
    public bool IsOverComplement => AssignedCount > Role.MaxCount;
    public bool IsAtDefault => AssignedCount == Role.DefaultCount;
}

public class RosterAssignmentRow
{
    public VolunteerAssignment Assignment { get; set; } = null!;
    public Volunteer Volunteer { get; set; } = null!;
}

public class RosterViewModel
{
    public RaceEvent Event { get; set; } = null!;
    public Dictionary<RoleCategory, List<RosterRoleRow>> ByCategory { get; set; } = new();
    public int TotalAssigned { get; set; }
    public int DistinctVolunteers { get; set; }
    public List<int> DoubleBookedVolunteerIds { get; set; } = new();
    public List<RaceEvent> CopyableEvents { get; set; } = new();

    /// <summary>Per-event stats panel (US29 AC1) — populated by the roster service.</summary>
    public EventVolunteerStats? PerEventStats { get; set; }
}

public class CopyRosterResult
{
    public bool Success { get; set; }
    public int CopiedCount { get; set; }
    public List<string> SkippedItems { get; } = new();
    public string? ErrorMessage { get; set; }
}
