# PDF Layout & Configuration

- [PDF Layout](#pdf-layout)
- [Configuration](#configuration)

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
- The PDF lists finishers only — DNF, DNS and DSQ entrants are excluded

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
