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
