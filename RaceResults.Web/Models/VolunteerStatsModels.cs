namespace RaceResults.Web.Models;

public class EventVolunteerStats
{
    public int EventId { get; set; }
    public int TotalAssignments { get; set; }
    public int DistinctVolunteers { get; set; }
    public int UnfilledRoleCount { get; set; }
    public List<RoleBreakdownRow> RoleBreakdown { get; set; } = new();
}

public class RoleBreakdownRow
{
    public string RoleName { get; set; } = string.Empty;
    public RoleCategory Category { get; set; }
    public int AssignedCount { get; set; }
    public int DefaultCount { get; set; }
    public int MinCount { get; set; }
    public int Shortfall => Math.Max(0, MinCount - AssignedCount);
}

public class SeasonVolunteerStats
{
    public int Year { get; set; }
    public int TotalInstances { get; set; }
    public int UniqueVolunteers { get; set; }
    public int TotalBallotEntries { get; set; }
    public int EventsCovered { get; set; }
    public List<VolunteerSeasonProfile> VolunteerProfiles { get; set; } = new();
    public List<VolunteerSeasonProfile> MostActive { get; set; } = new();
    public List<RoleCoverageTrendItem> RoleCoverageTrend { get; set; } = new();
    public List<int> AvailableYears { get; set; } = new();
}

public class VolunteerSeasonProfile
{
    public int VolunteerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsClubMember { get; set; }
    public int EventsAttended { get; set; }
    public int Assignments { get; set; }
    public int BallotEntries { get; set; }
    public int RunAfterCount { get; set; }
    public bool IsEverPresent { get; set; }
    public List<RolePerformedRow> RolesPerformed { get; set; } = new();
    public RunAndVolunteerSummary? RunAndVolunteer { get; set; }
}

public class RolePerformedRow
{
    public string RoleName { get; set; } = string.Empty;
    public int Times { get; set; }
}

public class RunAndVolunteerSummary
{
    public int RunCount { get; set; }
    public int VolunteerCount { get; set; }
    public int EventsInvolvedIn { get; set; }
}

public class RoleCoverageTrendItem
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public int TotalAssignments { get; set; }
    public int DistinctVolunteers { get; set; }
}
