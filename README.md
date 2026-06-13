# Pitsea RC Race Result Processor

An ASP.NET Core MVC web application for processing race results, built for Pitsea Running Club. Race organisers upload entrant, finish, and timing data, then view, edit, and export the collated results. The app also maintains the club's yearly **Champions of Champions** leaderboard across the Crown to Crown race series.

---

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Domain Conventions](#domain-conventions)
- [Project Structure](#project-structure)
- [Architecture Notes](#architecture-notes)
- [Data Persistence](#data-persistence)
- [Upload File Formats](#upload-file-formats)
- [Workflow](#workflow)
- [Champions of Champions](#champions-of-champions)
- [Configuration](#configuration)
- [PDF Layout](#pdf-layout)
- [Running the Tests](#running-the-tests)
- [Technology Stack](#technology-stack)
- [User Stories](#user-stories)

---

## Features

| Feature | Description |
|---|---|
| **Entrant upload** | Upload one or more `.xlsx` files (e.g. online registration + on-the-day) |
| **Entrant validation** | Required fields checked; duplicate bib numbers rejected; duplicate names reported as a warning for review |
| **Finish bib upload** | Upload a finish-position-to-bib `.xlsx` file; duplicate positions and bibs rejected |
| **Unmatched bib flagging** | Bibs in the finish file with no matching entrant are warned at upload and highlighted on the Results page with a badge and warning banner |
| **Timing upload** | Upload a timing file as `.csv` or `.xlsx`; zero-based positions auto-remapped; `STARTOFEVENT`/`ENDOFEVENT` device rows ignored |
| **Example file links** | The Uploads page shows the expected columns and offers downloadable example files for each upload format |
| **Timing consistency check** | Timing positions must exactly match finish bib positions; missing and unexpected positions are itemised |
| **Collated results view** | All results in finish order, with name, club, gender, age, time, and gap to winner |
| **Validated finish times** | Finish times are validated at upload, stored as typed durations, normalised for display, and checked for out-of-order anomalies |
| **DNF indication** | Entrants without a finish row are listed separately |
| **Edit results** | Correct any result row (position, bib, time, runner details) without re-uploading files; edits to Crown to Crown events trigger a Champions season recalculation |
| **Race stats + graphs** | Totals plus chart breakdowns for Male/Female, category, club, and finishers per minute |
| **Top 10 by category** | Top 10 finishers for Male, Female, Male U18, Female U18 |
| **Champions leaderboard** | Yearly cumulative scoring across Crown to Crown races in the May–September season window; top 10 per category earn points (10→1); runners identified across events by name + club; tie-breaking by event participation; multi-year navigation |
| **Champions audit trail** | Append-only points audit log distinguishing initial awards from recalculations; full scoring history retained |
| **PDF export** | Download a branded, race-ready PDF: first page includes winners + course records (Crown to Crown events only), subsequent pages continue with results table |
| **CSV export** | Download collated results (finishers + DNF, with a Status column) and the Champions leaderboard as Excel-friendly UTF-8 CSV with descriptive filenames |
| **Champions PDF export** | Export Champions leaderboard to PDF with tie-breaking indicators (†) and gold/silver/bronze highlighting for top 3 |
| **Event management** | Create, edit, select current, and delete events (`Crown to Crown` / `Bluebell 5`) with event-scoped results |
| **Settings + dark mode** | Theme toggle in Settings and navbar; preference persisted in browser local storage |
| **Theme-aware branding** | App logo switches by theme (light uses white logo, dark uses black logo) at a fixed size |
| **Persistent storage** | All data saved to a SQLite database and survives app restarts |
| **Backup & restore** | Download a consistent database snapshot and restore from a backup file in Settings; restores are validated, keep the pre-restore database aside, and apply pending migrations |
| **Structured logging** | Warnings on validation failures; errors on unhandled exceptions |

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

---

## Getting Started

```powershell
# Clone the repository
git clone <repo-url>
cd pitsea-rc-results-processing

# Run the web app
dotnet run --project .\RaceResults.Web\RaceResults.Web.csproj
```

Open the URL printed to the console (typically `http://localhost:5200` when using the default launch profile).

The SQLite database (`raceresults.db`) is created automatically on first run in the working directory.

### Logo assets

The shared layout expects the following logo files:

- `RaceResults.Web/wwwroot/images/pitsea-logo-white.png`
- `RaceResults.Web/wwwroot/images/pitsea-logo-black.png`

If these files are missing, the navbar logo will not render.

---

## Domain Conventions

Club-specific conventions that the code relies on. These are deliberate, not bugs:

| Convention | Detail |
|---|---|
| **Ages are only recorded for under-18s** | The entry form does not capture ages for adults. A blank `Age` means "adult" by convention, so `IsU18` requires a recorded age under 18. Age-based statistics for adults (histograms, veteran categories, age-grading) are not possible from current data. |
| **Categories** | Four categories throughout: Male (18+), Female (18+), Male U18, Female U18. A runner is U18 only when their recorded age is under 18. |
| **Unaffiliated** | A runner with no club value. Unaffiliated counts in race stats exclude U18 runners. |
| **Gender normalisation** | Upload values starting with `M` → `Male`, `F` → `Female` (case-insensitive); anything else is kept as typed. Category logic matches on the first letter. |
| **Bib numbers are per-event** | The same person gets a different bib at each race. Nothing cross-event may key on bib number. |
| **Runner identity (cross-event)** | Until a runner registry exists (US15), the Champions leaderboard identifies the same person across events by **normalised name + club** (case- and punctuation-insensitive). A mid-season club change therefore splits a runner's points; same-name runners in the same club merge. |
| **C2C series schedule** | The Crown to Crown series runs the full year: Good Friday (11:00), then the second Wednesday of each month May–August (19:30), the first *or* second Wednesday of September (19:00, decided per year), and finally Boxing Day (11:00). Events store only a date — start times are not modelled. |
| **Champions season window** | Champions of Champions scores only C2C races dated May–September inclusive, keyed to the event's calendar year. This is a **deliberate subset of the series**: the Good Friday and Boxing Day races are real C2C events that earn no Champions points. Out-of-window or non-C2C events are never scored. |
| **Event types** | `Crown to Crown` and `Bluebell 5` (annual, around April/May, date varies). Course records on the results PDF render only for Crown to Crown. |

---

## Project Structure

```
pitsea-rc-results-processing/
├── pitsea-rc-results-processing.slnx   # Solution file
│
├── RaceResults.Web/                    # Main web application
│   ├── Controllers/
│   │   ├── HomeController.cs           # Dashboard (/)
│   │   ├── RaceController.cs           # All race operations (/Race/*)
   │   ├── EventsController.cs         # Event management (/Events/*)
   │   └── ChampionsController.cs      # Champions leaderboard (/Champions/*)
│   ├── Data/
│   │   └── RaceResultsDbContext.cs     # EF Core DbContext (SQLite)
│   ├── Migrations/                     # EF Core migration files
│   ├── Models/                         # Domain and view models
│   │   ├── RaceEvent.cs
│   │   ├── EventType.cs
│   │   ├── CreateEventInput.cs
│   │   ├── EditEventInput.cs
│   │   ├── EventsPageViewModel.cs
│   │   ├── Entrant.cs
│   │   ├── FinishBibRecord.cs
│   │   ├── TimingRow.cs
│   │   ├── ResultRecord.cs
│   │   ├── RaceStats.cs
│   │   ├── TopTenCategory.cs
│   │   ├── EditResultInput.cs
│   │   ├── OperationResult.cs
│   │   ├── RaceStatusCounts.cs
│   │   ├── UploadsViewModel.cs
│   │   ├── ResultsPageViewModel.cs
│   │   ├── HomeDashboardViewModel.cs
   │   ├── RaceStatsDashboardViewModel.cs
   │   ├── ChampionOfChampionsScore.cs # Cumulative yearly scoring data
   │   ├── PointsAuditLog.cs          # Audit trail for scoring changes
   │   ├── ChampionsLeaderboardEntry.cs # DTO for leaderboard display
   │   └── ChampionsLeaderboardViewModel.cs # View model for leaderboard UI
│   ├── Services/
│   │   ├── IRaceResultsService.cs      # Service interface
   │   ├── RaceResultsService.cs       # Implementation (file parsing, business logic)
   │   ├── IChampionsOfChampionsService.cs # Champions scoring interface
   │   └── ChampionsOfChampionsService.cs  # Champions scoring & leaderboard logic
│   ├── Views/
│   │   ├── Home/Index.cshtml           # Dashboard
│   │   ├── Events/
│   │   │   ├── Index.cshtml            # Event list and actions
│   │   │   ├── Create.cshtml           # Event creation form
│   │   │   └── Edit.cshtml             # Event edit form
   │   ├── Race/
   │   │   ├── Uploads.cshtml          # Upload forms with status counts
   │   │   ├── Results.cshtml          # Collated results table + DNF list
   │   │   ├── EditResult.cshtml       # Edit a single result row
   │   │   ├── Stats.cshtml            # Race statistics
   │   │   └── Top10.cshtml            # Top 10 by category
   │   └── Champions/
   │       └── Leaderboard.cshtml      # Champions leaderboard with year selector
│   └── Program.cs                      # App bootstrap, DI, middleware
│
├── RaceResults.UnitTests/              # xUnit unit tests (73 tests)
   ├── Helpers/
   │   ├── DbContextHelpers.cs         # In-memory SQLite factory
   │   └── FormFileHelpers.cs          # IFormFile test doubles (XLSX + CSV)
   ├── RaceResultsServiceTestBase.cs   # Base class with isolated DB per test
   ├── EventManagementTests.cs
   ├── UploadEntrantsTests.cs
   ├── UploadFinishBibTests.cs
   ├── UploadTimingsTests.cs
   ├── CollatedResultsTests.cs
   ├── StatsAndTopTenTests.cs
   ├── EditResultTests.cs
   ├── PdfGenerationTests.cs
   └── ChampionsOfChampionsServiceTests.cs # 7 tests for Champions scoring logic
│
├── RaceResults.IntegrationTests/       # xUnit integration tests (21+ tests)
│   ├── RaceResultsWebFactory.cs        # WebApplicationFactory with in-memory SQLite
│   ├── MultipartHelpers.cs             # Multipart form builders for file uploads
│   ├── HomeControllerTests.cs
│   ├── EventsControllerTests.cs
│   ├── UploadControllerTests.cs
│   └── ResultsControllerTests.cs
│
└── user-stories/
    ├── US01-US31 *.md                  # One file per user story, each with a Status line
    └── example-files/                  # Real-format sample upload files (canonical copies)
        ├── online-registration.xlsx    # Pre-registration entrants
        ├── on-the-day-1.xlsx           # On-the-day entrants (file 1)
        ├── on-the-day-2.xlsx           # On-the-day entrants (file 2)
        ├── bib-position.xlsx           # Finish position + bib
        ├── timings.csv                 # Timing device CSV
        └── example-output.pdf          # Reference PDF layout
```

> **Example files are mirrored** into `RaceResults.Web/wwwroot/example-files/` so the Uploads page can offer in-app "Download example file" links that work in published/installed builds (US27). The canonical copies live in `user-stories/example-files/`; **if you change a sample file, update both locations.**

---

## Architecture Notes

- **Service layer owns all business logic.** Controllers are thin: they call `IRaceResultsService` / `IChampionsOfChampionsService`, store feedback in `TempData`, and redirect. File parsing, validation, collation, scoring, and PDF generation all live in services.
- **DbContext factory pattern.** Services receive `IDbContextFactory<RaceResultsDbContext>` and create a short-lived context per operation, which is why `RaceResultsService` can be registered as a singleton.
- **DI registrations** (`Program.cs`): `IRaceResultsService` → singleton; `IChampionsOfChampionsService` → scoped; `IDatabaseBackupService` → scoped.
- **Migrations apply automatically at startup** (`db.Database.Migrate()`), skipped when the environment is `Testing` so integration tests can use `EnsureCreated` against in-memory SQLite.
- **Current event fallback.** Exactly one event is "current" at a time. If none is current, the most recent event by date is promoted; if no events exist at all, a default `Crown to Crown` event dated 1 May 2026 is created (in-season for Champions scoring).
- **Operation results, not exceptions.** Upload and edit flows return an `OperationResult` carrying `Messages`, `Warnings`, and `Errors`; controllers render all three. Warnings (e.g. unmatched bibs, duplicate names) do not block the operation.
- **Destructive upload semantics** are intentional and ordered: see [Data Persistence](#data-persistence).
- **Champions scoring is event-triggered, derived data.** The points audit log is the source of truth; the scores table is a rebuildable cache (see [Champions of Champions](#champions-of-champions)).

---

## Data Persistence

Data is stored in a **SQLite database** (`raceresults.db`) in the application working directory. The schema is created and migrated automatically at startup.

To use a custom database path, add a connection string to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=C:\\Data\\myrace.db"
  }
}
```

**Data reset rules** (all scoped to the current event only):
- Uploading new entrants clears that event's finish bib and timing data to maintain consistency
- Uploading a new finish bib file clears that event's timing data
- Deleting an event removes its entrants, finish rows, and timing rows (Champions audit rows for the event cascade-delete with it)

**Storage location caution:** SQLite holds write locks on its database file. Running the live database inside a cloud-synced folder (OneDrive, Google Drive, Dropbox) risks sync conflicts and file corruption. Prefer a local, non-synced path for the live database and sync *backup copies* instead.

**Backup & restore (US19):** the Settings page offers **Download backup** (a consistent snapshot taken via the SQLite backup API — safe even while the app is running) and **Restore from backup**. Restore validates the uploaded file's schema before replacing anything, saves the current database aside as `raceresults-prerestore-{timestamp}.db` so a bad restore can be undone, then applies any pending EF migrations so older backups upgrade cleanly. Backup filenames are timestamped (e.g. `raceresults-backup-2026-06-12-1430.db`). Destructive actions (deleting an event, re-uploading entrants over existing data) prompt a reminder to back up first. Scheduled/automatic backups are out of scope.

---

## Upload File Formats

### Entrant files (`.xlsx`)

| Column | Required | Accepted header names |
|---|---|---|
| Bib number | ✅ | `Bib`, `BibNumber`, `BibNo`, `Bib Num`, `Number`, `Race Number`, `Race No`, `Runner Number` |
| Name | ✅ | `Name`, `FullName`, `RunnerName` |
| Gender | ✅ | `Gender`, `Sex`, `M/F` |
| Age | ❌ | `Age` |
| Club | ❌ | `Club`, `Team`, `Club Name` |

- Multiple files can be uploaded at once (e.g. online pre-registration + on-the-day sign-ups)
- Duplicate bib numbers across files are rejected
- Gender values are normalised: anything starting with `M` → `Male`, `F` → `Female`

### Finish bib file (`.xlsx`)

| Column | Required | Accepted header names |
|---|---|---|
| Position | ✅ | `Position`, `FinishPosition`, `Place` |
| Bib | ✅ | `Bib`, `BibNumber`, `BibNo`, `Bib Num`, `Number`, `Race Number`, `Race No`, `Runner Number` |

### Timing file (`.csv` or `.xlsx`)

**CSV format** (e.g. from a virtual volunteer timing device):
```
STARTOFEVENT,03/04/2026 11:18:54,device-info
1,03/04/2026 11:19:20,00:17:51
2,03/04/2026 11:19:33,00:18:04
...
ENDOFEVENT,...
```
- The `STARTOFEVENT` / `ENDOFEVENT` rows are ignored automatically
- Zero-based position numbering is detected and remapped to 1-based

**XLSX format:**

| Column | Required | Accepted header names |
|---|---|---|
| Position | ✅ | `Position`, `FinishPosition`, `Place` |
| Time | ✅ | `Time`, `Timing`, `FinishTime` |

**Time validation (US17):** every finish time is validated at upload and stored as a typed, sortable duration (the original text is kept for audit). Accepted formats are `mm:ss`, `h:mm:ss`, and `hh:mm:ss.f` (fractional seconds tolerated); minutes/seconds out of range (e.g. `12:75`) and non-numeric values (e.g. `00:2X:99`) are rejected with the row number and offending value. Times are shown in a canonical format (`mm:ss` under an hour, `h:mm:ss` over). If a finisher's time is earlier than someone who placed ahead, the upload still succeeds but warns and lists the affected positions. Pre-existing string times are converted to typed durations at startup; any that cannot be parsed are reported in the log for manual correction via Edit rather than silently dropped.

---

## Workflow

The typical sequence for processing results after a race:

```
1. Uploads page    →  Upload entrant files (.xlsx)
2. Uploads page    →  Upload finish bib file (.xlsx)
3. Uploads page    →  Upload timing file (.csv or .xlsx)
4. Results page    →  Review collated results and DNF list
5. Results page    →  Edit any incorrect rows if needed
6. Stats page      →  View numeric stats and chart breakdowns (gender, category, club, finishers/minute)
7. Top 10 page     →  View category leaders
8. Results page    →  Export PDF or CSV
```

All pages show a live count of loaded entrants, finish rows, and timing rows at the top so you can see the current data state at a glance.

All operational race data is scoped to the current selected event.

### Champions of Champions (Yearly Cumulative)

For Crown to Crown races (May-September season), points are automatically calculated and accumulated:

```
1. After each event's results are complete (uploads + timings)
   → Points awarded to top 10 per category (10→1 scale)
2. Points audit trail automatically tracks all scoring actions
3. Champions leaderboard accessible anytime at /Champions/Leaderboard
   → View current year or past years via year selector
   → Tied runners marked with † and ranked by event participation
4. Edit any past result
   → Entire season points automatically recalculated
   → Audit log updated with "Recalculated" entry
5. PDF export available
   → Current year or any past year
   → Includes tie-breaking indicators and top 3 highlighting
```

**Points Allocation:**
- 1st place: 10 points
- 2nd place: 9 points
- ...
- 10th place: 1 point
- 11th+ place: 0 points

**Tie-Breaking:**
When runners have equal cumulative points, they are ranked by number of events completed (more events = higher rank). Tied runners are visually indicated with † on the leaderboard.

---

## Champions of Champions

The Champions of Champions is a **yearly cumulative leaderboard** that ranks runners across all Crown to Crown races from May through September. It recognizes consistent top performers throughout the season.

### How It Works

**Automatic Scoring:**
- After each Crown to Crown race is fully uploaded (entrants + finish positions + timings), the top 10 finishers in each category automatically receive points
- Points are allocated on a 10→1 scale (1st place = 10 points, 2nd = 9, ..., 10th = 1; 11th+ = 0 points)
- Categories: Male (18+), Female (18+), Male U18, Female U18
- **Season window is enforced:** only Crown to Crown events dated May–September inclusive are scored; out-of-season or non-C2C events are rejected by the scoring service
- All scores are tracked with a complete, append-only audit trail

**Runner Identity:**
- Bib numbers change between races, and entrant rows are recreated per event, so cumulative scores cannot key on either
- The same person is recognised across events by **normalised name + club** (alphanumerics only, case-insensitive)
- Consequences: a runner who changes club mid-season appears as two entries; two same-name runners in the same club would merge. A persistent runner registry (US15) is the planned long-term fix
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

### Database Schema

**PointsAuditLog Table (source of truth):**
- Append-only audit trail of every scoring action
- Tracks: SeasonYear, EventId, EntrantId, Category, PointsAwarded, Action (`Initial` / `Recalculated` / `Voided`), AuditTimestamp, Reason
- Each scoring pass for an event writes one timestamped **batch**; aggregation counts only the latest batch per event, so the full award/recalculation history is retained without double counting
- `Voided` entries are always excluded from aggregation (reserved for disqualifications — see planned US16)

**ChampionOfChampionsScore Table (derived cache):**
- Rebuilt from the audit log after every scoring or recalculation pass
- Stores cumulative seasonal totals per runner per category: TotalPoints, RaceCount, LastUpdated
- Indexed by (SeasonYear, EntrantId, Category)
- Serves the default leaderboard view; "as of event" views aggregate directly from the audit log instead

### Service Layer

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

### URL Routes

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

### UI Components

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

### Test Coverage

- **7 comprehensive unit tests** covering:
  - Point calculation accuracy (top 10 allocation)
  - Cumulative scoring across events
  - Category differentiation
  - Audit log tracking
  - Tie-breaking logic
  - Visual highlighting for top 3
  - Tie detection and marking

---

## Configuration

Key settings in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=raceresults.db"
  }
}
```

Logging writes to the console by default. Validation failures are logged as `Warning`; unhandled exceptions are logged as `Error` with request path and method.

---

## PDF Layout

The generated PDF follows the race-day format used by Pitsea Running Club:

- Header on all pages: left and right `pitsea-logo-white.png` logos, `PITSEA RUNNING CLUB`, and current event title in the format `[Event Name] RESULTS [Event Date]`
- Event date in PDF title uses ordinal day + uppercase month/year (for example: `1ST MAY 2026`)
- Page 1: winners summary (`1st Male`, `1st Female`, `1st Male Youth`, `1st Female Youth`); the course records line is shown for **Crown to Crown events only** (records are course-specific and currently hard-coded — making them data-driven is planned as US22)
- All pages: results table with columns `Position`, `Time`, `Race No`, `Name`, `Gender`, `Club Name`
- Table styling: black header row with white text and white borders; plain white body rows
- Column alignment in PDF table: `Position`, `Time`, `Race No`, and `Gender` are centered
- Continuation pages: same branded header and table styling as page 1, without repeating the winners block

---

## Running the Tests

```powershell
# Run all tests
dotnet test .\pitsea-rc-results-processing.slnx

# Run unit tests only
dotnet test .\RaceResults.UnitTests\RaceResults.UnitTests.csproj

# Run integration tests only
dotnet test .\RaceResults.IntegrationTests\RaceResults.IntegrationTests.csproj

# Run with coverage
dotnet test .\pitsea-rc-results-processing.slnx --collect:"XPlat Code Coverage"
```

### Test summary

| Project | Tests | Approach |
|---|---|---|
| `RaceResults.UnitTests` | 104 | Tests `RaceResultsService`, `ChampionsOfChampionsService`, `DatabaseBackupService`, and `RaceTime` directly against isolated SQLite DBs per test |
| `RaceResults.IntegrationTests` | 21 | Full HTTP stack via `WebApplicationFactory<Program>` with in-memory SQLite |
| **Total** | **125** | |

---

## Technology Stack

| Component | Package |
|---|---|
| Web framework | ASP.NET Core MVC (.NET 10) |
| Database | SQLite via `Microsoft.EntityFrameworkCore.Sqlite` 10.0.7 |
| Excel parsing | `ClosedXML` 0.105.0 |
| CSV parsing | `CsvHelper` 33.1.0 |
| PDF generation | `QuestPDF` 2026.2.4 (Community licence) |
| Unit testing | xUnit 2.9.3 |
| Integration testing | `Microsoft.AspNetCore.Mvc.Testing` 10.0.7 |

---

## User Stories

US01–US14, US17, US18, US19 and US27 are implemented; the remaining US15–US31 stories are planned. Each story file carries a **Status** line (✅ Complete / 📋 Planned) for tracking. Individual story files are in [`user-stories/`](user-stories/):

### Implemented

| Story | Title |
|---|---|
| [US01](user-stories/US01-entrant-upload.md) | Entrant Upload |
| [US02](user-stories/US02-entrant-file-validation.md) | Entrant File Validation |
| [US03](user-stories/US03-finish-position-bib-upload.md) | Finish Position & Bib Upload |
| [US04](user-stories/US04-unmatched-bib-flagging.md) | Unmatched Bib Flagging |
| [US05](user-stories/US05-finish-position-timing-upload.md) | Timing Upload |
| [US06](user-stories/US06-finish-position-consistency-validation.md) | Timing Consistency Validation |
| [US07](user-stories/US07-collated-results-view.md) | Collated Results View |
| [US08](user-stories/US08-dnf-indication.md) | DNF Indication |
| [US09](user-stories/US09-export-results-pdf.md) | Export Results to PDF |
| [US10](user-stories/US10-edit-results-without-reupload.md) | Edit Results Without Re-upload |
| [US11](user-stories/US11-display-race-stats.md) | Display Race Statistics |
| [US12](user-stories/US12-top-10-by-category.md) | Top 10 by Category |
| [US13](user-stories/US13-event-management.md) | Event Management and Event-Scoped Results |
| [US14](user-stories/US14-champions-of-champions-leaderboard.md) | Champions of Champions Leaderboard |
| [US18](user-stories/US18-export-results-csv.md) | Export Results to CSV |
| [US27](user-stories/US27-example-file-links.md) | Example Upload File Links |
| [US19](user-stories/US19-database-backup-restore.md) | Database Backup and Restore |
| [US17](user-stories/US17-time-validation-and-analytics.md) | Time Validation and Race Analytics |

### Planned

| Story | Title |
|---|---|
| [US15](user-stories/US15-runner-registry.md) | Runner Registry |
| [US16](user-stories/US16-finish-status-dns-dnf-dsq.md) | Finish Status (DNS / DNF / DSQ) |
| [US20](user-stories/US20-archive-completed-events.md) | Archive Completed Events |
| [US21](user-stories/US21-public-results-page.md) | Public Results Page |
| [US22](user-stories/US22-course-records-management.md) | Course Records Management |
| [US23](user-stories/US23-enhanced-race-statistics.md) | Enhanced Race Statistics |
| [US24](user-stories/US24-season-statistics.md) | Season Statistics and Runner Season Profiles |
| [US25](user-stories/US25-app-installer.md) | Application Installer |
| [US26](user-stories/US26-cloud-hosting.md) | Cloud Hosting |
| [US28](user-stories/US28-volunteer-roster.md) | Volunteer Roster Builder |
| [US29](user-stories/US29-volunteer-stats.md) | Volunteer Statistics |
| [US30](user-stories/US30-end-of-season-review.md) | End of Season Review |
| [US31](user-stories/US31-season-calendar-generator.md) | Season Calendar Generator |

### Roadmap dependencies

Most planned stories are independent, with these exceptions:

- **US22 (Course Records)** and the time-based halves of **US23/US24** depend on **US17 (typed times)**
- **US24 (Season Statistics)** depends on **US15 (Runner Registry)** for all cross-event aggregation; its attendance-based stats need only US15, its time-based stats also need US17
- **US16 (DSQ)** consumes the existing-but-unused `Voided` audit action in Champions scoring
- **US21 (Public Results)** pairs naturally with **US20 (Archiving)** so published pages never change underneath readers
- **US26 (Cloud Hosting)** requires authentication to be added first, and makes US21 (public links) genuinely useful; US25 (installer) is the alternative local deployment path
- **US29 (Volunteer Stats)** depends on **US28 (Volunteer Roster)**; the combined run+volunteer recognition stat also benefits from US15
- **US30 (End of Season Review)** is the capstone: it depends on **US24** and **US29**, and degrades gracefully where US16/US17/US22 are absent
- **US31 (Season Calendar Generator)** is independent (it encodes the C2C date rules in the Domain Conventions section) and pairs with US20 for season turnover

Suggested order for the independent quick wins: US18 (CSV export) → US19 (backup/restore) → US27 (example file links) → US23 (enhanced stats), then the US17 → US16 → US15 chain.

