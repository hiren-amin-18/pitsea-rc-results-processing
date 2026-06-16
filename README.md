# Pitsea RC Race Result Processor

An ASP.NET Core MVC web application for processing race results, built for Pitsea Running Club. Race organisers upload entrant, finish, and timing data, then view, edit, and export the collated results. The app maintains the club's yearly **Champions of Champions** leaderboard across the Crown to Crown race series, and also manages the **volunteer roster** for each event — including a rules-based draft allocator, season-long volunteer recognition, and London Marathon ballot tracking.

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
| **Runner registry** | Persistent runners that per-event entrants link to; upload auto-matches by name+club with near-match warnings; a Runners page lists race counts and supports editing and merging duplicates |
| **Finish status (DNS/DNF/DSQ)** | Non-finishers default to DNF and can be set DNS (no-show); finishers can be disqualified with a recorded reason, which removes them from results (positions close up), voids their Champions points, and is reversible |
| **Edit results** | Correct any result row (position, bib, time, runner details) without re-uploading files; edits to Crown to Crown events trigger a Champions season recalculation |
| **Race stats + graphs** | Totals plus chart breakdowns for Male/Female, category, club, affiliation, and finishers per minute |
| **Enhanced statistics** | Completion rate, gender-split percentages, finish-time summary (winner/median/average, 25/50/75 percentiles), and the busiest finish window (US23) |
| **Season statistics** | A per-year dashboard (most attended + ever-present, top clubs, fastest per category per event type, most improved, participation trends, season DNF rate) and per-runner season profiles, keyed on the runner registry (US24) |
| **End of season review** | A consolidated season-end page and branded PDF covering headlines, year-on-year deltas, the Champions of Champions standings, runner recognition, new course records, DNF/DSQ totals, and an awards list ready for trophy engraving — degrades gracefully where dependent stories aren't implemented (US30) |
| **Windows installer** | A self-contained Windows release (Inno Setup installer or zip fallback) lets committee members install and run the app without the .NET SDK; database lives in a per-user folder so upgrades preserve data and uninstall keeps it by default (US25) |
| **Top 10 by category** | Top 10 finishers for Male, Female, Male U18, Female U18 |
| **Champions leaderboard** | Yearly cumulative scoring across Crown to Crown races in the May–September season window; top 10 per category earn points (10→1); runners identified across events by name + club; tie-breaking by event participation; multi-year navigation |
| **Champions audit trail** | Append-only points audit log distinguishing initial awards from recalculations; full scoring history retained |
| **PDF export** | Download a branded, race-ready PDF: first page includes winners + data-driven course records for the event type, subsequent pages continue with results table |
| **Course records** | Records stored per event type and category; a management page to view/correct them, automatic detection of a new record after timings (organiser confirms), retained record history, and a "NEW COURSE RECORD" flag on the PDF |
| **CSV export** | Download collated results (finishers + DNF, with a Status column) and the Champions leaderboard as Excel-friendly UTF-8 CSV with descriptive filenames |
| **Champions PDF export** | Export Champions leaderboard to PDF with tie-breaking indicators (†) and gold/silver/bronze highlighting for top 3 |
| **Event management** | Create, edit, select current, and delete events (`Crown to Crown` / `Bluebell 5`) with event-scoped results |
| **Season calendar generator** | One-click "Generate Season" creates the year's Crown to Crown fixtures from the club's fixed date rules (Good Friday, second Wednesdays May–Aug, first-or-second Wednesday Sep, Boxing Day) with start times; preview before generating; skips dates that already have a C2C event (US31) |
| **Event archiving** | Mark a finalised event as archived to make it read-only: uploads, edits, and detail changes are rejected; it can't be current or deleted until unarchived; results remain viewable and exportable, and it still counts toward Champions (US20) |
| **Public results page** | Publish an event from the Events page to expose a shareable read-only URL (`/public/results/{token}`) with the collated results, category winners, DNF list, and a public Champions of Champions leaderboard. Tokens are unguessable per event; unpublished events return 404 (US21) |
| **Volunteer register** | Persistent volunteers with gender, first-aid-trained flag, club-member flag, and an optional link to a runner. Deactivate to preserve history without losing past assignments (US28) |
| **Volunteer roster** | Per-event roster page grouped by Leadership / Finish Area / Course, with the 23 Crown to Crown roles seeded by default. Restricted roles (Lead, Results) honour an allow-list; Marshal Point 7 supports a standing pre-placement (Ian + dog Shane); first-aid roles require a trained volunteer; min/max overrides and double-booking warnings supported. Edit retrospectively for past events, copy from the previous event, export to PDF and Excel (US28) |
| **Volunteer statistics** | Per-event panel on the roster page; season page with total volunteering instances, unique volunteers, role coverage trend, most-active leaderboard, and per-volunteer profile including the "ran X, volunteered Y, involved in Z" combined recognition. CSV export. London Marathon ballot entries counted one per volunteering instance, members only (US29) |
| **Automated roster allocation** | Pick attendees + per-volunteer preferences (specific role, run-after, near-finish, can't-walk-far, seated, any-role) and have the app propose a draft. Greedy seven-step rules engine: pre-place fixtures → eligibility → run-after rotation across the season → preferences → role mix-up across the season → marshal gender mix → fill remainder. Review and apply; Apply re-validates through the roster service (US32) |
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

For non-technical users, a pre-built Windows release is produced by [`installer/build-installer.ps1`](installer/build-installer.ps1) — see [installer/README.md](installer/README.md). Installed builds default to a per-user database location and don't need the .NET SDK.

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
| **Finish status** | Each entrant has a status (US16): *Finished* (has a position), *DNF* (no finish row — the default for non-finishers), *DNS* (registered but never started — excluded from the DNF list, stats, and PDF), *DSQ* (finished but disqualified — removed from results with positions closing up, and no Champions points). DSQ requires a reason and is reversible; the stored finish rows are never rewritten (it is a presentation-level adjustment). |
| **Unaffiliated** | A runner with no club value. Unaffiliated counts in race stats exclude U18 runners. |
| **Gender normalisation** | Upload values starting with `M` → `Male`, `F` → `Female` (case-insensitive); anything else is kept as typed. Category logic matches on the first letter. |
| **Bib numbers are per-event** | The same person gets a different bib at each race. Nothing cross-event may key on bib number. |
| **Runner identity (cross-event)** | A persistent **runner registry** (US15) is the source of cross-event identity: per-event entrants link to a `Runner`, and Champions scoring keys on `RunnerId`. At entrant upload, runners are matched by **normalised name + club** (case- and punctuation-insensitive); an exact match links automatically, no match creates a runner, and a near match (same name/different club or a small typo) is flagged as a warning for the organiser to review and optionally merge under **Runners**. |
| **C2C series schedule** | The Crown to Crown series runs the full year: Good Friday (11:00), then the second Wednesday of each month May–August (19:30), the first *or* second Wednesday of September (19:00, decided per year), and finally Boxing Day (11:00). Events store only a date — start times are not modelled. |
| **Champions season window** | Champions of Champions scores only C2C races dated May–September inclusive, keyed to the event's calendar year. This is a **deliberate subset of the series**: the Good Friday and Boxing Day races are real C2C events that earn no Champions points. Out-of-window or non-C2C events are never scored. |
| **Event types** | `Crown to Crown` and `Bluebell 5` (annual, around April/May, date varies). Course records are held per event type (US22): Crown to Crown ships with seeded records; Bluebell 5 starts empty, and the PDF records line is omitted until records exist. |
| **Volunteer roles** | C2C runs with 23 seeded roles in three categories: Leadership (Lead, Shadow Lead, Results), Finish Area (Timekeeping, Course Setup, Number Collection, On The Day Registration, Finish Line Funnel + Results, First Aid and Prizes, Tail Runners, Photographer, Water Table), and Course (Marshal Points 1–7 + 5a, Metal Gate, First Aid On Course). Default counts, min/max overrides, and run-after capacity (how many in a role may run their race afterwards) are configurable per role. Bluebell 5 will get its own role seed in a later story. |
| **Restricted roles + pre-placement** | Some roles only accept specific volunteers (Lead = Hiren or Michael; Results = Hiren) via an allow-list, seeded empty so the organiser populates it once the people are added. Roles can also pre-place a specific volunteer (Marshal Point 7 is configured to pre-place Ian — and his dog Shane — who is not a Pitsea RC member). |
| **First-aid roles** | First Aid and Prizes (also presents the prizes) and First Aid On Course can only be filled by volunteers flagged as first-aid trained. |
| **London Marathon ballot** | One ballot entry per volunteering instance (each role at each event counts), counted **per calendar year, with no per-person cap**, and **for Pitsea RC members only**. Non-members count toward all other volunteer recognition but earn zero ballot entries. Membership is renewed yearly in April, so a single member flag on the volunteer record is sufficient — no per-event-date tracking. |

---

## Project Structure

```
pitsea-rc-results-processing/
├── pitsea-rc-results-processing.slnx   # Solution file
│
├── RaceResults.Web/                    # Main web application
│   ├── Controllers/                    # Thin controllers; all logic in services
│   │   ├── HomeController.cs           # Dashboard + Settings (/)
│   │   ├── RaceController.cs           # Uploads / Results / Stats / Top10 / exports (/Race/*)
│   │   ├── EventsController.cs         # Event management + season generator (/Events/*)
│   │   ├── ChampionsController.cs      # Champions leaderboard + exports (/Champions/*)
│   │   ├── RunnersController.cs        # Runner registry: list, edit, merge (/Runners/*)
│   │   ├── CourseRecordsController.cs  # Course record management (/CourseRecords/*)
│   │   ├── SeasonController.cs         # Season dashboard + runner profiles + end-of-season review (/Season/*)
│   │   ├── PublicController.cs         # Read-only published results pages (/public/*)
│   │   ├── VolunteersController.cs     # Volunteer register (/Volunteers/*)
│   │   ├── VolunteerRolesController.cs # Volunteer role catalogue (/VolunteerRoles/*)
│   │   ├── VolunteerRosterController.cs # Per-event roster + allocator (/Events/{id}/Roster/*)
│   │   └── VolunteerStatsController.cs # Season volunteer statistics (/VolunteerStats/*)
│   ├── Data/
│   │   └── RaceResultsDbContext.cs     # EF Core DbContext (SQLite); seeds C2C role catalogue
│   ├── Migrations/                     # EF Core migration files (most recent: AddVolunteerRoster)
│   ├── Models/                         # Domain entities, input DTOs, view models
│   │   ├── RaceEvent.cs, EventType.cs, Entrant.cs, FinishBibRecord.cs, TimingRow.cs, ResultRecord.cs
│   │   ├── Runner.cs                                 # Persistent runner (US15)
│   │   ├── ChampionOfChampionsScore.cs, PointsAuditLog.cs, ChampionsLeaderboardViewModel.cs
│   │   ├── CourseRecord.cs, CourseRecordModels.cs    # US22
│   │   ├── SeasonStatisticsModels.cs                 # US24
│   │   ├── SeasonReview.cs                           # US30
│   │   ├── RaceStatisticsSummary.cs, RaceStatsDashboardViewModel.cs # US23
│   │   ├── FinishStatus.cs                           # US16
│   │   ├── PublicViewModels.cs                       # US21
│   │   ├── Volunteer.cs, VolunteerRole.cs, VolunteerRoleEligibility.cs, VolunteerAssignment.cs
│   │   ├── RoleCategory.cs, VolunteerInputs.cs       # US28 DTOs / view models
│   │   ├── VolunteerStatsModels.cs                   # US29 stats DTOs
│   │   └── AllocationModels.cs                       # US32 allocator inputs / draft / report
│   ├── Services/                       # Business logic; receive IDbContextFactory<RaceResultsDbContext>
│   │   ├── IRaceResultsService.cs + RaceResultsService.cs           # File parsing, collation, PDF/CSV
│   │   ├── IChampionsOfChampionsService.cs + ChampionsOfChampionsService.cs
│   │   ├── IDatabaseBackupService.cs + DatabaseBackupService.cs     # US19
│   │   ├── IRunnerRegistryService.cs + RunnerRegistryService.cs     # US15
│   │   ├── ICourseRecordService.cs + CourseRecordService.cs        # US22
│   │   ├── ISeasonStatisticsService.cs + SeasonStatisticsService.cs # US24
│   │   ├── ISeasonCalendarService.cs + SeasonCalendarService.cs + SeasonCalendar.cs # US31
│   │   ├── ISeasonReviewService.cs + SeasonReviewService.cs        # US30
│   │   ├── IVolunteerRegistryService.cs + VolunteerRegistryService.cs # US28
│   │   ├── IVolunteerRoleService.cs + VolunteerRoleService.cs       # US28
│   │   ├── IVolunteerRosterService.cs + VolunteerRosterService.cs   # US28
│   │   ├── IVolunteerRosterExportService.cs + VolunteerRosterExportService.cs # US28 PDF + Excel
│   │   ├── IVolunteerStatsService.cs + VolunteerStatsService.cs    # US29
│   │   ├── IRosterAllocator.cs + RosterAllocator.cs                # US32 rules engine
│   │   ├── IRosterDraftApplier.cs + RosterDraftApplier.cs          # US32 persist via roster service
│   │   ├── RaceTime.cs                 # Time parsing/formatting (US17)
│   │   ├── RunnerIdentity.cs           # Normalised name/club key (US15)
│   │   └── DatabasePathResolver.cs     # Per-user DB location for installed builds (US25)
│   ├── Views/                          # Razor views (Bootstrap; dark-mode aware)
│   │   ├── Shared/_Layout.cshtml       # Navbar (Race / Standings / Manage dropdowns) + theme toggle
│   │   ├── Home/                       # Dashboard + Settings (backup/restore)
│   │   ├── Race/                       # Uploads, Results, EditResult, Stats, Top10
│   │   ├── Events/                     # Index, Create, Edit, GenerateSeason (US31)
│   │   ├── Champions/                  # Leaderboard
│   │   ├── Runners/                    # Index, Edit (US15)
│   │   ├── CourseRecords/              # Index, Edit (US22)
│   │   ├── Season/                     # Dashboard, runner profile, Review (US24, US30)
│   │   ├── Public/                     # Read-only published results (US21)
│   │   ├── Volunteers/                 # Index, Create, Edit, _Form (US28)
│   │   ├── VolunteerRoles/             # Index, Create, Edit, _Form (US28)
│   │   ├── VolunteerRoster/            # Index, Allocate, Draft (US28, US32)
│   │   └── VolunteerStats/             # Index (US29)
│   └── Program.cs                      # App bootstrap, DI, middleware, runtime backfills
│
├── RaceResults.UnitTests/              # xUnit unit tests (193 tests)
│   ├── Helpers/
│   │   ├── DbContextHelpers.cs         # In-memory SQLite factory
│   │   └── FormFileHelpers.cs          # IFormFile test doubles (XLSX + CSV)
│   ├── RaceResultsServiceTestBase.cs   # Base class with isolated DB per test
│   ├── Race + uploads: UploadEntrantsTests, UploadFinishBibTests, UploadTimingsTests,
│   │                   CollatedResultsTests, StatsAndTopTenTests, EditResultTests,
│   │                   FinishStatusTests, RaceTimeTests, RaceStatisticsSummaryTests
│   ├── Events / season: EventManagementTests, EventArchivingTests, SeasonCalendarTests,
│   │                    SeasonStatisticsTests, SeasonReviewTests
│   ├── Identity / records: RunnerRegistryTests, CourseRecordTests
│   ├── Exports + backup: PdfGenerationTests, CsvExportTests, DatabaseBackupServiceTests
│   ├── Installer path: DatabasePathResolverTests
│   ├── Champions: ChampionsOfChampionsServiceTests
│   └── Volunteers: VolunteerRosterTests, VolunteerStatsTests, RosterAllocatorTests
│
├── RaceResults.IntegrationTests/       # xUnit integration tests (26 tests)
│   ├── RaceResultsWebFactory.cs        # WebApplicationFactory with in-memory SQLite
│   ├── MultipartHelpers.cs             # Multipart form builders for file uploads
│   ├── HomeControllerTests.cs
│   ├── EventsControllerTests.cs
│   ├── UploadControllerTests.cs
│   └── ResultsControllerTests.cs
│
├── aidlc-docs/                         # AI-DLC plan + summary + audit artefacts per story
│   ├── audit.md                        # Append-only audit log (one entry per story)
│   ├── construction/plans/             # USxx-code-generation-plan.md (one per story)
│   └── construction/USxx/              # USxx-implementation-summary.md (one per story)
│
└── user-stories/
    ├── US01-US32 *.md                  # One file per user story, each with a Status line
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
- **DI registrations** (`Program.cs`): `IRaceResultsService` → singleton (uses `IDbContextFactory`); everything else scoped — `IChampionsOfChampionsService`, `IDatabaseBackupService`, `IRunnerRegistryService`, `ICourseRecordService`, `ISeasonStatisticsService`, `ISeasonCalendarService`, `ISeasonReviewService`, `IVolunteerRegistryService`, `IVolunteerRoleService`, `IVolunteerStatsService`, `IVolunteerRosterService`, `IVolunteerRosterExportService`, `IRosterAllocator`, `IRosterDraftApplier`.
- **Migrations apply automatically at startup** (`db.Database.Migrate()`), skipped when the environment is `Testing` so integration tests can use `EnsureCreated` against in-memory SQLite.
- **Current event promotion (no auto-seeding).** At most one event is "current" at a time. If none is current, the most recent non-archived event by date is promoted. If no events exist at all, `GetCurrentEvent()` returns `null` and the app renders an empty state ("No events yet — create the first event") rather than silently seeding a placeholder. Read-only views return empty data; mutating actions (uploads, edits, DSQ/reinstate) return a clear "Create an event first" error.
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
- Deleting an event removes its entrants, finish rows, timing rows, and **volunteer assignments** (Champions audit rows for the event cascade-delete with it). Volunteers themselves and the role catalogue are never deleted — the persistent register and role allow-lists survive any event deletion (US28 AC10). Deleting the last remaining event leaves the database with no events; the app shows an empty state on each page until a new event is created.

**Storage location caution:** SQLite holds write locks on its database file. Running the live database inside a cloud-synced folder (OneDrive, Google Drive, Dropbox) risks sync conflicts and file corruption. Prefer a local, non-synced path for the live database and sync *backup copies* instead.

**Installed builds (US25):** when no `ConnectionStrings:DefaultConnection` is configured (i.e. on installed builds rather than `dotnet run` from the repo), the app defaults to `%LOCALAPPDATA%\PitseaRaceResults\raceresults.db` — a per-user folder outside cloud-synced paths. An explicit connection string in `appsettings.json` or the `ConnectionStrings__DefaultConnection` environment variable still wins.

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

### Volunteer roster

Sits alongside the race workflow and works for past, current, and future events:

```
1. Volunteers page          →  Add volunteers (gender, member, first-aid, optional runner link)
2. Volunteer Roles page     →  Populate the restricted-role allow-lists (Lead, Results),
                                set Marshal Point 7's pre-placed volunteer (Ian)
3. Events → Roster          →  Add assignments by hand, or click "Allocate draft" to
                                pick attendees + preferences and have the rules engine propose one
4. Roster page (Apply step) →  Review the draft, then Apply to persist assignments
5. Roster page              →  Edit freely; export to PDF or Excel for race-day briefing
6. After the event          →  Adjust for no-shows / late additions to keep stats accurate
7. Volunteer Stats page     →  Season summary + per-volunteer profile + ballot count + CSV
```

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

### Database Schema

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
- Page 1: winners summary (`1st Male`, `1st Female`, `1st Male Youth`, `1st Female Youth`); the course records line is rendered from stored records for the event's type and omitted when none exist (US22). A record set at this event is flagged "NEW COURSE RECORD"
- All pages: results table with columns `Position`, `Time`, `Race No`, `Name`, `Gender`, `Club Name`
- Table styling: black header row with white text and white borders; plain white body rows
- Column alignment in PDF table: `Position`, `Time`, `Race No`, and `Gender` are centered
- Continuation pages: same branded header and table styling as page 1, without repeating the winners block
- After the results table, DNF and DSQ sections are listed (US16); DNS entrants are excluded

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
| `RaceResults.UnitTests` | 195 | Tests `RaceResultsService` (incl. statistics + archiving), `ChampionsOfChampionsService`, `DatabaseBackupService`, `RaceTime`, the runner registry, finish-status, course records, season statistics, the season calendar, the season review (including volunteer recognition wired up to US29), the installer DB-path resolver, the volunteer registry / roles / roster / export / stats services, and the US32 roster allocator + draft applier against isolated SQLite DBs per test |
| `RaceResults.IntegrationTests` | 26 | Full HTTP stack via `WebApplicationFactory<Program>` with in-memory SQLite |
| **Total** | **221** | |

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

All user stories are implemented — US01–US25, US27, US28, US29, US30, US31, and US32 (US26 cloud hosting was dropped as not required). Each story file carries a **Status** line (✅ Complete) for tracking. Individual story files are in [`user-stories/`](user-stories/):

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
| [US15](user-stories/US15-runner-registry.md) | Runner Registry |
| [US16](user-stories/US16-finish-status-dns-dnf-dsq.md) | Finish Status (DNS / DNF / DSQ) |
| [US22](user-stories/US22-course-records-management.md) | Course Records Management |
| [US23](user-stories/US23-enhanced-race-statistics.md) | Enhanced Race Statistics |
| [US24](user-stories/US24-season-statistics.md) | Season Statistics and Runner Season Profiles |
| [US20](user-stories/US20-archive-completed-events.md) | Archive Completed Events |
| [US21](user-stories/US21-public-results-page.md) | Public Results Page |
| [US31](user-stories/US31-season-calendar-generator.md) | Season Calendar Generator |
| [US30](user-stories/US30-end-of-season-review.md) | End of Season Review (volunteer-recognition section now wired up to US29) |
| [US25](user-stories/US25-app-installer.md) | Application Installer |
| [US28](user-stories/US28-volunteer-roster.md) | Volunteer Roster Builder |
| [US29](user-stories/US29-volunteer-stats.md) | Volunteer Statistics |
| [US32](user-stories/US32-roster-auto-allocation.md) | Automated Roster Allocation |

### Story dependencies (for context)

Most stories are independent; these are the non-obvious dependencies the implementation relied on:

- **US22 (Course Records)** and the time-based halves of **US23/US24** depend on **US17 (typed times)**
- **US24 (Season Statistics)** depends on **US15 (Runner Registry)** for all cross-event aggregation; its attendance-based stats need only US15, its time-based stats also need US17
- **US16 (DSQ)** consumes the existing-but-unused `Voided` audit action in Champions scoring
- **US21 (Public Results)** pairs naturally with **US20 (Archiving)** so published pages never change underneath readers
- **US29 (Volunteer Stats)** depends on **US28 (Volunteer Roster)**; the combined run+volunteer recognition stat also benefits from US15
- **US32 (Automated Roster Allocation)** depends on **US28** for the register, role complement, and editable roster, and on the season history **US29** reports on to drive rotation/mix-up; it hands a draft back to US28's roster
- **US30 (End of Season Review)** is the capstone: it depends on **US24** and **US29**, and degrades gracefully where US16/US17/US22 are absent. The volunteer recognition section is now wired up to US29 (Volunteer of the Season + ever-present volunteers + ran-and-volunteered double commitment); if no volunteer assignments exist for the year, the section is omitted gracefully.
- **US31 (Season Calendar Generator)** is independent (it encodes the C2C date rules in the Domain Conventions section) and pairs with US20 for season turnover

