# Pitsea RC Race Result Processor

An ASP.NET Core MVC web application for processing race results. Built for race organisers to upload entrant, finish, and timing data, then view, edit, and export the collated results.

---

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Project Structure](#project-structure)
- [Data Persistence](#data-persistence)
- [Upload File Formats](#upload-file-formats)
- [Workflow](#workflow)
- [Configuration](#configuration)
- [Running the Tests](#running-the-tests)
- [Technology Stack](#technology-stack)
- [User Stories](#user-stories)

---

## Features

| Feature | Description |
|---|---|
| **Entrant upload** | Upload one or more `.xlsx` files (e.g. online registration + on-the-day) |
| **Entrant validation** | Required fields checked; duplicate bib numbers rejected |
| **Finish bib upload** | Upload a finish-position-to-bib `.xlsx` file |
| **Unmatched bib flagging** | Bibs in the finish file that don't match any entrant are warned |
| **Timing upload** | Upload a timing file as `.csv` or `.xlsx`; zero-based positions auto-remapped |
| **Timing consistency check** | Timing positions must exactly match finish bib positions |
| **Collated results view** | All results in finish order, with name, club, gender, age, and time |
| **DNF indication** | Entrants without a finish row are listed separately |
| **Edit results** | Correct any result row (position, bib, time) without re-uploading files |
| **Race stats + graphs** | Totals plus chart breakdowns for Male/Female, category, club, and finishers per minute |
| **Top 10 by category** | Top 10 finishers for Male, Female, Male U18, Female U18 |
| **PDF export** | Download a branded, race-ready PDF: first page includes winners + course records, subsequent pages continue with results table |
| **Event management** | Create, edit, select current, and delete events (`Crown to Crown` / `Bluebell 5`) with event-scoped results |
| **Settings + dark mode** | Theme toggle in Settings and navbar; preference persisted in browser local storage |
| **Theme-aware branding** | App logo switches by theme (light uses white logo, dark uses black logo) at a fixed size |
| **Persistent storage** | All data saved to a SQLite database and survives app restarts |
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

## Project Structure

```
pitsea-rc-results-processing/
├── pitsea-rc-results-processing.slnx   # Solution file
│
├── RaceResults.Web/                    # Main web application
│   ├── Controllers/
│   │   ├── HomeController.cs           # Dashboard (/)
│   │   ├── RaceController.cs           # All race operations (/Race/*)
│   │   └── EventsController.cs         # Event management (/Events/*)
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
│   │   └── RaceStatsDashboardViewModel.cs
│   ├── Services/
│   │   ├── IRaceResultsService.cs      # Service interface
│   │   └── RaceResultsService.cs       # Implementation (file parsing, business logic)
│   ├── Views/
│   │   ├── Home/Index.cshtml           # Dashboard
│   │   ├── Events/
│   │   │   ├── Index.cshtml            # Event list and actions
│   │   │   ├── Create.cshtml           # Event creation form
│   │   │   └── Edit.cshtml             # Event edit form
│   │   └── Race/
│   │       ├── Uploads.cshtml          # Upload forms with status counts
│   │       ├── Results.cshtml          # Collated results table + DNF list
│   │       ├── EditResult.cshtml       # Edit a single result row
│   │       ├── Stats.cshtml            # Race statistics
│   │       └── Top10.cshtml            # Top 10 by category
│   └── Program.cs                      # App bootstrap, DI, middleware
│
├── RaceResults.UnitTests/              # xUnit unit tests (66 tests)
│   ├── Helpers/
│   │   ├── DbContextHelpers.cs         # In-memory SQLite factory
│   │   └── FormFileHelpers.cs          # IFormFile test doubles (XLSX + CSV)
│   ├── RaceResultsServiceTestBase.cs   # Base class with isolated DB per test
│   ├── EventManagementTests.cs
│   ├── UploadEntrantsTests.cs
│   ├── UploadFinishBibTests.cs
│   ├── UploadTimingsTests.cs
│   ├── CollatedResultsTests.cs
│   ├── StatsAndTopTenTests.cs
│   ├── EditResultTests.cs
│   └── PdfGenerationTests.cs
│
├── RaceResults.IntegrationTests/       # xUnit integration tests (21 tests)
│   ├── RaceResultsWebFactory.cs        # WebApplicationFactory with in-memory SQLite
│   ├── MultipartHelpers.cs             # Multipart form builders for file uploads
│   ├── HomeControllerTests.cs
│   ├── EventsControllerTests.cs
│   ├── UploadControllerTests.cs
│   └── ResultsControllerTests.cs
│
└── user-stories/
    ├── US01-US13 *.md                  # Individual user story files
    └── example-files/
        └── timings.csv                 # Example timing CSV
```

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

**Data reset rules:**
- Uploading new entrants automatically clears all finish bib and timing data to maintain consistency
- Uploading a new finish bib file automatically clears timing data

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

---

## Workflow

The typical sequence for processing results after a race:

```
1. Uploads page  →  Upload entrant files (.xlsx)
2. Uploads page  →  Upload finish bib file (.xlsx)
3. Uploads page  →  Upload timing file (.csv or .xlsx)
4. Results page  →  Review collated results and DNF list
5. Results page  →  Edit any incorrect rows if needed
6. Stats page    →  View numeric stats and chart breakdowns (gender, category, club, finishers/minute)
7. Top 10 page   →  View category leaders
8. Results page  →  Export PDF
```

All pages show a live count of loaded entrants, finish rows, and timing rows at the top so you can see the current data state at a glance.

All operational race data is scoped to the current selected event.

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
- Event date in PDF title uses ordinal day + uppercase month/year (for example: `3RD APRIL 2026`)
- Page 1: winners summary (`1st Male`, `1st Female`, `1st Male Youth`, `1st Female Youth`) plus course records line
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
| `RaceResults.UnitTests` | 66 | Tests `RaceResultsService` directly against an isolated in-memory SQLite DB per test |
| `RaceResults.IntegrationTests` | 21 | Full HTTP stack via `WebApplicationFactory<Program>` with in-memory SQLite |
| **Total** | **87** | |

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

All 13 user stories are implemented. Individual story files are in [`user-stories/`](user-stories/):

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

