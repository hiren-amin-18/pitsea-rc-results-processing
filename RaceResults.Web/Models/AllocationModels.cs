namespace RaceResults.Web.Models;

public enum AllocationReason
{
    PrePlaced = 0,
    Eligibility = 1,
    RunAfterRotation = 2,
    Preference = 3,
    Mix = 4,
    Fill = 5,
    /// <summary>Secondary assignment: volunteer also covers a finish line role after their primary OTD/NC duty.</summary>
    FinishLine = 6
}

public class AllocationCandidate
{
    public int VolunteerId { get; set; }
    public int? PreferredRoleId { get; set; }
    public bool WantsToRunAfter { get; set; }
    public bool WantsNearFinish { get; set; }
    public bool CantWalkFar { get; set; }
    public bool WantsSeated { get; set; }
    public bool WantsRaceHq { get; set; }
    public bool AnyRole { get; set; }
}

public class ProposedAssignment
{
    public int VolunteerId { get; set; }
    public string VolunteerName { get; set; } = string.Empty;
    public int VolunteerRoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public RoleCategory Category { get; set; }
    public bool WillRunAfter { get; set; }
    public AllocationReason Reason { get; set; }

    // Preferences carried through so applying preserves them on the persisted assignment.
    public int? PreferredRoleId { get; set; }
    public bool WantsToRunAfter { get; set; }
    public bool WantsNearFinish { get; set; }
    public bool CantWalkFar { get; set; }
    public bool WantsSeated { get; set; }
    public bool WantsRaceHq { get; set; }
    public bool AnyRole { get; set; }
}

public class AllocationReport
{
    public List<UnfilledRole> UnfilledRoles { get; set; } = new();
    public List<UnplacedCandidate> UnplacedCandidates { get; set; } = new();
    public List<string> PreferencesNotHonoured { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public class UnfilledRole
{
    public string RoleName { get; set; } = string.Empty;
    public int Assigned { get; set; }
    public int Default { get; set; }
    public int Min { get; set; }
}

public class UnplacedCandidate
{
    public int VolunteerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class AllocationDraft
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public List<ProposedAssignment> Proposals { get; set; } = new();
    public AllocationReport Report { get; set; } = new();
}

public class AllocationFormInput
{
    public int EventId { get; set; }
    public List<int> SelectedVolunteerIds { get; set; } = new();
    public List<AllocationCandidate> Candidates { get; set; } = new();
}
