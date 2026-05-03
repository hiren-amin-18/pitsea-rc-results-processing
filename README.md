# 5K Race Results Web App (C#)

This repository now contains an ASP.NET Core MVC web app that implements the user stories in the `user-stories` folder.

## Project

- Web app: `RaceResults.Web`
- Solution: `pitsea-rc-results-processing.slnx`

## Implemented Features

- Upload one or more entrant `.xlsx` files (online registration and on-the-day files)
- Validate entrant uploads for required fields and duplicate bib numbers
- Upload finish position + bib `.xlsx`
- Flag unmatched bib numbers from finish file
- Upload timings from `.csv` or `.xlsx`
- Validate timing positions match finish-bib positions
- Collate and display results by finish position
- Show DNF runners (entrants without a finish row)
- Export results to PDF using a structured, styled layout
- Edit result rows without re-uploading source files
- Display race stats:
  - total males
  - total females
  - total males U18
  - total females U18
  - unaffiliated males excluding U18
  - unaffiliated females excluding U18
- Show top 10 by category:
  - male
  - female
  - male U18
  - female U18

## Run Locally

From repository root:

```powershell
dotnet restore .\RaceResults.Web\RaceResults.Web.csproj
dotnet run --project .\RaceResults.Web\RaceResults.Web.csproj
```

Then open the URL shown in the console (for example `https://localhost:xxxx`).

## Typical Upload Files

These sample files are under `user-stories/example-files`:

- `online-registration.xlsx`
- `on-the-day-1.xlsx`
- `on-the-day-2.xlsx`
- `bib-position.xlsx`
- `timings.csv`

## Notes

- Data is stored in-memory while the app is running.
- Re-uploading entrants resets downstream finish and timing data to preserve consistency.
