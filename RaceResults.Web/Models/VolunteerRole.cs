namespace RaceResults.Web.Models;

/// <summary>Configurable volunteer role for an event type (US28). The C2C complement is seeded; the organiser can
/// rename, retire, reorder, or add roles.</summary>
public class VolunteerRole
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public RoleCategory Category { get; set; }

    /// <summary>Event type this role applies to. C2C is seeded; Bluebell 5 will have its own seed in a later story.</summary>
    public EventType EventType { get; set; } = EventType.CrownToCrown;

    /// <summary>How many people the role usually takes.</summary>
    public int DefaultCount { get; set; }

    /// <summary>Inclusive lower/upper bound for the override; the roster will reject assignments outside this band.</summary>
    public int MinCount { get; set; }
    public int MaxCount { get; set; }

    /// <summary>Roles that are not always needed (e.g. Photographer, Metal Gate, Shadow Lead).</summary>
    public bool IsOptional { get; set; }

    /// <summary>How many of the assigned people may run after their duty (US32 rotates these above all else).</summary>
    public int RunAfterCapacity { get; set; }

    /// <summary>When true, only first-aid-trained volunteers may be assigned.</summary>
    public bool RequiresFirstAid { get; set; }

    /// <summary>When true, only volunteers in <see cref="VolunteerRoleEligibility"/> may be assigned
    /// (e.g. Lead = Hiren or Michael; Results = Hiren). Empty allow-list until the organiser populates it.</summary>
    public bool HasEligibilityRestriction { get; set; }

    /// <summary>Standing pre-placement (e.g. Ian at Marshal Point 7). The allocator (US32) pins this person
    /// automatically; the roster shows them as the first assignee on creation.</summary>
    public int? PrePlacedVolunteerId { get; set; }
    public Volunteer? PrePlacedVolunteer { get; set; }

    /// <summary>Display order within the category. Lower first.</summary>
    public int SortOrder { get; set; }

    /// <summary>Retired roles are kept (history) but hidden from new assignments.</summary>
    public bool IsActive { get; set; } = true;
}
