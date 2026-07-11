namespace RaceResults.Web.Services;

/// <summary>
/// The detailed per-event view of the Champions of Champions leaderboard (US44): the summary rows
/// plus one column per scored event, showing the points each runner scored in each event.
/// </summary>
public class ChampionsDetail
{
    /// <summary>Scored events, in date order, that make up the per-event columns.</summary>
    public IReadOnlyList<ChampionsDetailColumn> Columns { get; init; } = new List<ChampionsDetailColumn>();

    /// <summary>One row per runner (ranked exactly as the summary), with per-event points attached.</summary>
    public IReadOnlyList<ChampionsDetailRow> Rows { get; init; } = new List<ChampionsDetailRow>();
}

/// <summary>A scored event shown as a column in the detailed breakdown.</summary>
public class ChampionsDetailColumn
{
    public int EventId { get; init; }

    /// <summary>1-based position of this event among the scored events shown, in date order.</summary>
    public int Round { get; init; }

    /// <summary>Column heading, e.g. "Round 1 – May".</summary>
    public required string Label { get; init; }

    /// <summary>The event's own name, for a tooltip/title without widening the header.</summary>
    public required string EventName { get; init; }

    public DateTime EventDate { get; init; }
}

/// <summary>A leaderboard row plus the points that runner scored in each scored event.</summary>
public class ChampionsDetailRow
{
    /// <summary>The ranked summary entry (rank, runner, category, total, race count, tie, highlight).</summary>
    public required ChampionsLeaderboardEntry Entry { get; init; }

    /// <summary>Points scored keyed by event id. An event absent from the map means no points (renders blank).</summary>
    public IReadOnlyDictionary<int, int> PointsByEventId { get; init; } = new Dictionary<int, int>();

    /// <summary>Points scored in the given event, or null if none (outside top 10 or did not run).</summary>
    public int? PointsFor(int eventId) => PointsByEventId.TryGetValue(eventId, out var p) ? p : null;
}
