# Champions of Champions

The Champions of Champions is a **yearly cumulative leaderboard** that ranks runners across all Crown to Crown races from May through September. It recognizes consistent top performers throughout the season.

## How It Works

**Automatic Scoring:**
- After each Crown to Crown race is fully uploaded (entrants + finish positions + timings), the top 10 finishers in each category automatically receive points
- Points are allocated on a 10→1 scale (1st place = 10 points, 2nd = 9, ..., 10th = 1; 11th+ = 0 points)
- Categories: Male (18+), Female (18+), Male U18, Female U18
- **Season window is enforced:** only Crown to Crown events dated May–September inclusive are scored; out-of-season or non-C2C events are rejected by the scoring service
- All scores are tracked with a complete, append-only audit trail

**Runner Identity:**
- Bib numbers change between races, and entrant rows are recreated per event, so cumulative scores cannot key on either
- Since US15, the same person is recognised across events by their persistent **`RunnerId`**. Entrant uploads match runners by normalised name + club (alphanumerics only, case-insensitive); the leaderboard aggregation then keys on `RunnerId`
- A runner who changes club mid-season stays a single runner; a typo that creates a duplicate can be merged under **Runners**, which recalculates affected seasons automatically
- The leaderboard displays each runner's most recent entrant record (latest name/club spelling on file)

**Leaderboard Display:**
- Accessible at `/Champions/Leaderboard`
- Shows cumulative scores across all events in the season, grouped by category
- **Default season** is derived from the current event's date (not the wall clock), so historical data views stay correct across calendar years; a year selector switches seasons
- **Ranking:** Runners ranked by total points, with ties broken by number of events completed (more events = higher rank)
- **Tie Indicator:** Runners are marked with † (dagger symbol) only when they are tied on **both** points and race count — i.e. the tie-breaker could not separate them
- **Top 3 Highlighting:**
  - 1st place: Gold background
  - 2nd place: Silver background
  - 3rd place: Bronze background

**Edit Handling:**
- If any past race result is edited (position, runner category, etc.), the entire season's points are **automatically recalculated**
- Recalculation **appends** a new `Recalculated` batch of audit entries per event — earlier batches are never deleted, preserving the full history of when points were awarded vs recalculated
- Aggregation always uses each event's **latest batch** (by timestamp), so superseded awards are excluded without being lost

**Data Isolation:**
- Each season year is completely isolated (2024 season, 2025 season, etc.)
- No cross-year contamination
- Supports multi-year historical data

**PDF Export:**
- `/Champions/ExportPdf` generates a branded PDF of the leaderboard
- Can export any season year via `?year=2025` parameter
- Includes:
  - Event date and season year
  - All categories with runners ranked
  - Tie-breaking indicators and colors
  - Top 3 highlighting with background colors
  - Run number column showing event participation count

## Database Schema

**PointsAuditLog Table (source of truth):**
- Append-only audit trail of every scoring action
- Tracks: SeasonYear, EventId, EntrantId, Category, PointsAwarded, Action (`Initial` / `Recalculated` / `Voided`), AuditTimestamp, Reason
- Each scoring pass for an event writes one timestamped **batch**; aggregation counts only the latest batch per event, so the full award/recalculation history is retained without double counting
- `Voided` entries are always excluded from aggregation; disqualifying a finisher (US16) appends `Voided` entries for their awards and recalculates the season

**ChampionOfChampionsScore Table (derived cache):**
- Rebuilt from the audit log after every scoring or recalculation pass
- Stores cumulative seasonal totals per runner per category: TotalPoints, RaceCount, LastUpdated
- Indexed by (SeasonYear, EntrantId, Category)
- Serves the default leaderboard view; "as of event" views aggregate directly from the audit log instead

## Service Layer

**IChampionsOfChampionsService Interface:**
- `CalculateAndSaveEventPointsAsync(eventId)` - Scores top 10 per category for an event; throws if the event is not Crown to Crown or falls outside the May–September window
- `RecalculateSeasonPointsAsync(seasonYear)` - Re-scores every in-season event, appending `Recalculated` audit batches (called on result edits)
- `GetLeaderboardAsync(seasonYear, asOfEventId?)` - Retrieves cumulative leaderboard; with `asOfEventId`, aggregates from the audit log including only events up to that event's date
- `GetCurrentSeasonLeaderboardAsync(asOfEventId?)` - Leaderboard for the season of the current event's date
- `IsEligibleForPointsAsync(entrantId, eventId, category)` - Checks if runner scored (excludes voided entries)

**Integration Points:**
- Triggered automatically after a successful timing upload when the current event is Crown to Crown (`RaceController.UploadTimings`)
- Triggered automatically after a successful result edit on a Crown to Crown event (`RaceController.EditResult` → season recalculation)
- Can be triggered manually from the leaderboard page (`ChampionsController.CalculatePoints`)
- Season year is always extracted from the event's date, never from the system clock

## URL Routes

```
GET  /Champions/Leaderboard                     → Leaderboard for the current event's season
GET  /Champions/Leaderboard?year=2025           → 2025 season leaderboard
GET  /Champions/Leaderboard?year=2025&eventId=5 → 2025 leaderboard as of event 5
POST /Champions/CalculatePoints                 → Manually score an event
GET  /Champions/ExportPdf                       → Export current season to PDF
GET  /Champions/ExportPdf?year=2025             → Export 2025 season to PDF
GET  /Champions/ExportPdf?year=2025&eventId=5   → Export 2025 as of event 5
GET  /Champions/ExportCsv?year=2025&eventId=5   → Export the same leaderboard view to CSV
```

Results CSV export is at `GET /Race/ExportCsv` (current event).

## UI Components

**Year Selector:**
- Dropdown menu showing available years (2024 to current year)
- Defaults to the current event's season
- Clicking a year navigates to `/Champions/Leaderboard?year=YYYY`

**Leaderboard Table:**
- Grouped by category (Male, Female, Male U18, Female U18)
- Columns: Rank | Name | Club | Events | Points
- Runners with tied points marked with † in rank column
- Mobile-responsive design

**Tie-Breaking Key:**
- Blue info banner explaining: "When runners have equal points, they are ranked by number of races completed (more events = higher rank)"
- Visual indicator † next to rank for tied runners

**PDF Styling:**
- Same tie-breaking indicators and colors as web view
- Professional branded layout
- Suitable for printing and distribution

## Test Coverage

- **7 comprehensive unit tests** covering:
  - Point calculation accuracy (top 10 allocation)
  - Cumulative scoring across events
  - Category differentiation
  - Audit log tracking
  - Tie-breaking logic
  - Visual highlighting for top 3
  - Tie detection and marking
