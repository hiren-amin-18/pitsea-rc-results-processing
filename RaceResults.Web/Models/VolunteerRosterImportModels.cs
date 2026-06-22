namespace RaceResults.Web.Models;

/// <summary>One parsed assignment row from the uploaded xlsx (US35). One volunteer name + the role
/// they were listed under + any annotation flags pulled from parentheses in the cell.</summary>
public class RosterImportParsedAssignment
{
    public string RawRoleName { get; set; } = string.Empty;
    public string RawVolunteerText { get; set; } = string.Empty;
    public string VolunteerName { get; set; } = string.Empty;
    public bool WillRunAfter { get; set; }
    public string? Note { get; set; }
}

/// <summary>Preview of what an import will do, before the organiser confirms (US35 AC2).</summary>
public class RosterImportPreview
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public EventType EventType { get; set; }
    public bool RosterAlreadyHasAssignments { get; set; }

    /// <summary>Names not yet in the register that will be created; the organiser picks gender per name.</summary>
    public List<RosterImportNewVolunteer> NewVolunteers { get; set; } = new();

    /// <summary>Names already in the register that will be reused (no field changed).</summary>
    public List<RosterImportMatchedVolunteer> MatchedVolunteers { get; set; } = new();

    /// <summary>Assignments to be created on commit.</summary>
    public List<RosterImportAssignmentPlan> Assignments { get; set; } = new();

    /// <summary>Role names in the file that didn't match any role in this event type's catalogue.</summary>
    public List<string> UnmatchedRoles { get; set; } = new();

    /// <summary>Hard errors that prevent the import (file unreadable, wrong shape, etc).</summary>
    public List<string> Errors { get; set; } = new();

    public bool CanCommit => Errors.Count == 0 && !RosterAlreadyHasAssignments;
}

public class RosterImportNewVolunteer
{
    /// <summary>Lower-cased, whitespace-collapsed key used to dedupe.</summary>
    public string NameKey { get; set; } = string.Empty;
    /// <summary>Display name as it appeared in the file (first occurrence, trimmed + whitespace-collapsed).</summary>
    public string Name { get; set; } = string.Empty;
}

public class RosterImportMatchedVolunteer
{
    public int VolunteerId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class RosterImportAssignmentPlan
{
    public string VolunteerNameKey { get; set; } = string.Empty;
    public string VolunteerDisplayName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool WillRunAfter { get; set; }
    public string? Note { get; set; }
}

/// <summary>Posted from the preview view to commit the import.</summary>
public class RosterImportCommitInput
{
    public int EventId { get; set; }
    /// <summary>Serialised <see cref="RosterImportPreview"/> from the preview page (the source of truth for the import).</summary>
    public string PreviewJson { get; set; } = string.Empty;
    /// <summary>Map of NameKey → chosen gender ("Male"/"Female"/"") for newly-created volunteers.</summary>
    public Dictionary<string, string> NewVolunteerGenders { get; set; } = new();
}
