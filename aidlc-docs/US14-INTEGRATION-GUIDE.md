# US14 Quick Reference Guide - Developer Integration

## Service Usage

### 1. Calculate Points After Event Completion
```csharp
// Inject the service
private readonly IChampionsOfChampionsService _championsService;

// After all race data (entrants, finish positions, timings) is uploaded:
await _championsService.CalculateAndSaveEventPointsAsync(eventId);
```

### 2. Recalculate Season After Result Edit
```csharp
// After a result is edited
var raceEvent = await db.Events.FindAsync(eventId);
var seasonYear = raceEvent.EventDate.Year;
await _championsService.RecalculateSeasonPointsAsync(seasonYear);
```

### 3. Display Leaderboard
```csharp
// Get current season leaderboard
var leaderboard = await _championsService.GetCurrentSeasonLeaderboardAsync();

// Get leaderboard as of specific event
var leaderboardAsOf = await _championsService.GetCurrentSeasonLeaderboardAsync(eventId: 3);
```

### 4. Check Eligibility
```csharp
// Check if runner scored points in an event
bool eligible = await _championsService.IsEligibleForPointsAsync(entrantId, eventId, "Male");
```

## Data Structures

### ChampionsLeaderboardEntry
```csharp
public class ChampionsLeaderboardEntry
{
    public int Rank { get; set; }                           // 1, 2, 3, ...
    public required Entrant Entrant { get; set; }           // Runner info
    public required string Category { get; set; }           // "Male", "Female", "Male U18", "Female U18"
    public int TotalPoints { get; set; }                    // Cumulative points
    public int RaceCount { get; set; }                      // Number of races participated in
    public bool IsPointsTied { get; set; }                  // True if tied on points with another runner (for † indicator)
    public string? HighlightClass { get; set; }             // "rank-gold", "rank-silver", "rank-bronze", or null
}
```

## Controller Route Mapping

```
GET  /Champions/Leaderboard              - View current year leaderboard
GET  /Champions/Leaderboard?year=2025    - View 2025 season leaderboard
GET  /Champions/Leaderboard?eventId=3    - View current year as of event 3
GET  /Champions/Leaderboard?year=2025&eventId=3 - View 2025 as of event 3
GET  /Champions/ExportPdf                - Export current year leaderboard to PDF
GET  /Champions/ExportPdf?year=2025      - Export 2025 season to PDF
GET  /Champions/ExportPdf?year=2025&eventId=3 - Export 2025 as of event 3 to PDF
```

**Year Parameter:**
- Optional `year` query parameter (defaults to current year if omitted)
- Accepts any 4-digit year (2024, 2025, 2026, etc.)
- Year selector dropdown in UI for quick navigation (defaults to 2024-current year range)

## View Parameters

```html
<!-- In View -->
@Model.Leaderboard              <!-- IReadOnlyList<ChampionsLeaderboardEntry> -->
@Model.CurrentEventId           <!-- int -->
@Model.CurrentEventName         <!-- string -->
@Model.AsOfDate                 <!-- DateTime -->
```

## Leaderboard Display

**Table Columns**: Rank | Name | Club | Events | Points

**Key Features**:
1. **Rank**: Numbered badge (1, 2, 3, ...)
2. **Name**: Runner name with age in parentheses (if available)
3. **Club**: Club name or "Unaffiliated" if no club
4. **Events**: Number of Crown to Crown races runner participated in
5. **Points**: Total cumulative points

**Tie Indicator**:
- When runners have the **same points**, they are marked with **† (dagger symbol)**
- Visual key explains: "Runners with equal points are ranked by number of events completed (more events = higher rank)"
- The `IsPointsTied` property is set to `true` for tied runners
- This applies to both web view and PDF export

**Top 3 Highlighting**:
- Rank 1: Gold background (#ffd700)
- Rank 2: Silver background (#c0c0c0)
- Rank 3: Bronze background (#cd7f32)

## CSS Styling

```css
/* Top 3 highlighting */
.rank-gold  { background-color: #ffd700; color: #000; }  /* 1st place */
.rank-silver { background-color: #c0c0c0; color: #000; }  /* 2nd place */
.rank-bronze { background-color: #cd7f32; color: #fff; }  /* 3rd place */
```

## Database Query Examples

```csharp
// Get all audit logs for a season
var audits = db.PointsAuditLogs
    .Where(a => a.SeasonYear == 2026)
    .Include(a => a.Entrant)
    .ToList();

// Get scores for a specific runner
var scores = db.ChampionScores
    .Where(s => s.EntrantId == 5)
    .ToList();

// Find tied runners
var tiedScores = db.ChampionScores
    .Where(s => s.SeasonYear == 2026 && s.TotalPoints == 50)
    .ToList();
```

## Points Calculation Recap

| Position | Points |
|----------|--------|
| 1st      | 10     |
| 2nd      | 9      |
| 3rd      | 8      |
| 4th      | 7      |
| 5th      | 6      |
| 6th      | 5      |
| 7th      | 4      |
| 8th      | 3      |
| 9th      | 2      |
| 10th     | 1      |
| 11th+    | 0      |

## Tie-Breaking Rule

When two runners have the same total points:
1. Compare race participation count (higher = better rank)
2. If still tied, maintain alphabetical order by name (secondary)

## Audit Trail Actions

```csharp
public enum AuditAction
{
    Initial = 0,        // Points awarded after event
    Recalculated = 1,   // Points recalculated due to edit
    Voided = 2          // Points voided (event cancelled)
}
```

## Common Integration Locations

### 1. In RaceResultsService.UploadTimingsAsync()
```csharp
// After all data validated and saved
if (result.Success)
{
    // Check if event is Crown to Crown
    var evt = await db.Events.FirstAsync(e => e.Id == currentEventId);
    if (evt.EventType == EventType.CrownToCrown)
    {
        await _championsService.CalculateAndSaveEventPointsAsync(currentEventId);
    }
}
```

### 2. In RaceResultsService.UpdateResult()
```csharp
// After result is updated
if (successResult.Success && raceEvent?.EventType == EventType.CrownToCrown)
{
    var seasonYear = raceEvent.EventDate.Year;
    await _championsService.RecalculateSeasonPointsAsync(seasonYear);
}
```

### 3. In Navigation/Layout
```html
<!-- In Shared/_Layout.cshtml navigation -->
<li class="nav-item">
    <a class="nav-link" href="/Champions/Leaderboard">Champions</a>
</li>
```

## Testing

```bash
# Run Champions tests
dotnet test --filter "ChampionsOfChampionsServiceTests"

# Run with verbose output
dotnet test --filter "ChampionsOfChampionsServiceTests" -v detailed
```

## Logging Recommendations

```csharp
// When calculating points
_logger?.LogInformation(
    "Calculated Champions points for event {EventId}: {AffectedRunners} runners",
    eventId, affectedRunnerCount);

// When recalculating
_logger?.LogInformation(
    "Recalculated Champions season {Year} due to result edit",
    seasonYear);
```

## Error Handling

```csharp
try
{
    await _championsService.CalculateAndSaveEventPointsAsync(eventId);
}
catch (InvalidOperationException ex)
{
    // Event not found or not Crown to Crown
    _logger?.LogError(ex, "Cannot calculate Champions points for event {EventId}", eventId);
}
catch (Exception ex)
{
    // Unexpected error
    _logger?.LogError(ex, "Error calculating Champions points");
    throw;
}
```

## Performance Considerations

- **Leaderboard queries**: Indexed on (SeasonYear, EntrantId, Category) - O(1) lookup
- **Recalculation**: Full aggregation from audit logs - O(n) where n = number of audit records
- **Race participation**: Counted with `Distinct()` on EventId - efficient

For large seasons with 1000+ races, consider:
- Materialized views for audit aggregation
- Caching leaderboard for static seasons
- Pagination for large result sets
