# Architecture & Project Structure

- [Project Structure](#project-structure)
- [Architecture Notes](#architecture-notes)
- [Data Persistence](#data-persistence)

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
│   │   ├── RunnersController.cs        # Runner registry: list, edit, merge, batch-merge, dismiss similar pairs (/Runners/*)
│   │   ├── CourseRecordsController.cs  # Course record management (/CourseRecords/*)
│   │   ├── SeasonController.cs         # Season dashboard + runner profiles + end-of-season review (/Season/*)
│   │   ├── PublicController.cs         # Read-only published results pages (/public/*)
│   │   ├── VolunteersController.cs     # Volunteer register (/Volunteers/*)
│   │   ├── VolunteerRolesController.cs # Volunteer role catalogue (/VolunteerRoles/*)
│   │   ├── VolunteerRosterController.cs # Per-event roster + allocator (/Events/{id}/Roster/*)
│   │   └── VolunteerStatsController.cs # Season volunteer statistics (/VolunteerStats/*)
│   ├── Data/
│   │   └── RaceResultsDbContext.cs     # EF Core DbContext (SQLite); seeds C2C role catalogue
│   ├── Migrations/                     # EF Core migration files (most recent: AddVolunteerDefaultPreferencesAndGridMemory)
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
│   │   ├── AllocationModels.cs                       # US32 allocator inputs / draft / report; US40 grid row
│   │   └── AllocationCandidateRecord.cs              # US40 per-event allocate-grid memory
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
│   │   ├── IVolunteerRosterImportService.cs + VolunteerRosterImportService.cs # US35 xlsx import → preview → commit
│   │   ├── AllocationGridService.cs    # US40 allocate-grid load/save (interface + impl in one file)
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
│   │   ├── Volunteers/                 # Index, Create, Edit, _Form (US28), Merge (US39)
│   │   ├── VolunteerRoles/             # Index, Create, Edit, _Form (US28)
│   │   ├── VolunteerRoster/            # Index, Allocate, Draft (US28, US32), Import + ImportPreview (US35),
│   │   │                               #   Edit (US36), QuickAssign (US41)
│   │   └── VolunteerStats/             # Index (US29)
│   └── Program.cs                      # App bootstrap, DI, middleware, runtime backfills
│
├── RaceResults.UnitTests/              # xUnit unit tests (256 tests)
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
    ├── US01-US43 *.md                  # One file per user story, each with a Status line
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
- **DI registrations** (`Program.cs`): `IRaceResultsService` → singleton (uses `IDbContextFactory`); everything else scoped — `IChampionsOfChampionsService`, `IDatabaseBackupService`, `IRunnerRegistryService`, `ICourseRecordService`, `ISeasonStatisticsService`, `ISeasonCalendarService`, `ISeasonReviewService`, `IVolunteerRegistryService`, `IVolunteerRoleService`, `IVolunteerStatsService`, `IVolunteerRosterService`, `IVolunteerRosterExportService`, `IRosterAllocator`, `IAllocationGridService`, `IRosterDraftApplier`, `IVolunteerRosterImportService`.
- **Migrations apply automatically at startup** (`db.Database.Migrate()`), skipped when the environment is `Testing` so integration tests can use `EnsureCreated` against in-memory SQLite.
- **Current event promotion (no auto-seeding).** At most one event is "current" at a time. If none is current, the most recent non-archived event by date is promoted. If no events exist at all, `GetCurrentEvent()` returns `null` and the app renders an empty state ("No events yet — create the first event") rather than silently seeding a placeholder. Read-only views return empty data; mutating actions (uploads, edits, DSQ/reinstate) return a clear "Create an event first" error.
- **Operation results, not exceptions.** Upload and edit flows return an `OperationResult` carrying `Messages`, `Warnings`, and `Errors`; controllers render all three. Warnings (e.g. unmatched bibs, duplicate names) do not block the operation.
- **Destructive upload semantics** are intentional and ordered: see [Data Persistence](#data-persistence).
- **Champions scoring is event-triggered, derived data.** The points audit log is the source of truth; the scores table is a rebuildable cache (see [Champions of Champions](champions.md)).

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
- Deleting an event removes its entrants, finish rows, timing rows, **volunteer assignments**, and its saved allocate-grid state (US40) (Champions audit rows for the event cascade-delete with it). Volunteers themselves and the role catalogue are never deleted — the persistent register and role allow-lists survive any event deletion (US28 AC10). Deleting the last remaining event leaves the database with no events; the app shows an empty state on each page until a new event is created.

**Storage location caution:** SQLite holds write locks on its database file. Running the live database inside a cloud-synced folder (OneDrive, Google Drive, Dropbox) risks sync conflicts and file corruption. Prefer a local, non-synced path for the live database and sync *backup copies* instead.

**Installed builds (US25):** when no `ConnectionStrings:DefaultConnection` is configured (i.e. on installed builds rather than `dotnet run` from the repo), the app defaults to `%LOCALAPPDATA%\PitseaRaceResults\raceresults.db` — a per-user folder outside cloud-synced paths. An explicit connection string in `appsettings.json` or the `ConnectionStrings__DefaultConnection` environment variable still wins.

**Backup & restore (US19):** the Settings page offers **Download backup** (a consistent snapshot taken via the SQLite backup API — safe even while the app is running) and **Restore from backup**. Restore validates the uploaded file's schema before replacing anything, saves the current database aside as `raceresults-prerestore-{timestamp}.db` so a bad restore can be undone, then applies any pending EF migrations so older backups upgrade cleanly. Backup filenames are timestamped (e.g. `raceresults-backup-2026-06-12-1430.db`). Destructive actions (deleting an event, re-uploading entrants over existing data) prompt a reminder to back up first. Scheduled/automatic backups are out of scope.
