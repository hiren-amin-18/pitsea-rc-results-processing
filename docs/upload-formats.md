# Upload File Formats

## Entrant files (`.xlsx`)

The expected sheet layout depends on the current event's type (US33).

**Crown to Crown:**

| Column | Required | Accepted header names |
|---|---|---|
| Bib number | ✅ | `Bib`, `BibNumber`, `BibNo`, `Bib Num`, `Number`, `Race Number`, `Race No`, `Runner Number` |
| Name | ✅ | `Name`, `FullName`, `RunnerName` |
| Gender | ✅ | `Gender`, `Sex`, `M/F` |
| Age | ❌ | `Age` — integer; only recorded for U18 |
| Club | ❌ | `Club`, `Team`, `Club Name` |

**Bluebell 5:**

| Column | Required | Accepted header names |
|---|---|---|
| Bib number | ✅ | (same as above) |
| Name | ✅ | (same as above) |
| Gender | ✅ | (same as above) |
| Age | ❌ | `Age` — must be `Male U40`, `Female U35`, or blank (blank = Vet: M40+ / F35+). U18 values are rejected. |
| Club | ❌ | (same as above) |

- Multiple files can be uploaded at once (e.g. online pre-registration + on-the-day sign-ups)
- Duplicate bib numbers across files are rejected
- Gender values are normalised: anything starting with `M` → `Male`, `F` → `Female`

## Finish bib file (`.xlsx`)

| Column | Required | Accepted header names |
|---|---|---|
| Position | ✅ | `Position`, `FinishPosition`, `Place` |
| Bib | ✅ | `Bib`, `BibNumber`, `BibNo`, `Bib Num`, `Number`, `Race Number`, `Race No`, `Runner Number` |

## Timing file (`.csv` or `.xlsx`)

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

## Online-registration CSV inputs to the Online Registration generator (US45)

The **Generate Online Registration** action on a Crown to Crown event takes the two `.csv` files exported from the club's online registration platform:

| CSV | Identified by | Rows |
|---|---|---|
| Adults (18+) | `U18IsDependantOfUser` column **absent** | one row per entrant 18 or over on race day |
| U18 | `U18IsDependantOfUser` column **present** | one row per entrant under 18 on race day |

Required columns (both files): `Forename`, `Surname`, `Gender`, `Club`. The U18 file additionally requires `Age_on_Race_Day` (used to fill the `Age` cell in the output). All other columns are ignored. Header matching is case-insensitive and tolerant of punctuation.

`Gender` is expected to be `Male` or `Female`; anything else surfaces in the preview and blocks generation. `Club` is fuzzy-matched to the canonical [Clubs](../RaceResults.Web/Views/Clubs/Index.cshtml) list — confident matches snap silently, unresolved values wait in the preview for the organiser to pick a canonical club, add a new one, or leave the raw text as typed.

Output: `<event-slug>-online-registration.xlsx` with the club's standard six columns (`Race #`, `Name`, `Club Name`, `M/F`, `Age`, `Comments`). Race # is blank; `Age` cells for U18 rows are shaded pink (Female) or light blue (Male). The organiser then adds race numbers in Excel and feeds the file through the normal [Entrant upload](#entrant-files-xlsx) above.

Bluebell 5 keeps its single-file registration flow (see [US33](../user-stories/US33-bluebell-results-processing.md)); this generator is C2C-only.
