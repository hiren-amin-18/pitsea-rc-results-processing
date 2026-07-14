# US45 - Online Registration Spreadsheet Generator

**Status:** ✅ Complete

## As a
Race organiser

## I want
To upload the two entrant CSVs exported from the online registration platform (adults 18+ and U18) for a C2C event and have the app produce the `<Event Name> Online Registration.xlsx` spreadsheet — with names properly cased, club names snapped to our canonical club list via fuzzy matching, blank race numbers, and Age filled in only for U18s

## So that
The manual step of hand-cleaning each season's registration export into the master spreadsheet disappears — the file drops out in the format we already use downstream (assigning bibs, then feeding [[US01-entrant-upload]]) with typos in club names and inconsistent name casing already resolved.

---

## Background

Online race registration is done on an external platform. When the organiser closes registration they export **two CSV files** — one for entrants 18 or over on race day, one for those under 18 — and today spend a chunk of time hand-tidying them into the `Online Registration.xlsx` spreadsheet the club has always worked from. The tidy-up is mechanical but tedious:

- Names arrive as separate `Forename` and `Surname` columns, often in inconsistent case (`JOHN`, `smith`, `mcDonald`).
- Clubs are free-typed by the runner on the registration form, so `Pitsea RC`, `pitsea running club`, `PRC`, and `Pitsea Running Club` all mean the same club.
- The U18 CSV has extra columns (identified by the presence of `U18IsDependantOfUser`) and its own `Age_on_Race_Day` value.
- The output spreadsheet has one row per entrant, no race number yet (bibs are assigned later), Age blank for adults (by our [[ages-only-recorded-for-u18]] convention).

The reference file that shows the target shape is [online-registration.xlsx](example-files/online-registration.xlsx) — columns `Race #`, `Name`, `Club Name`, `M/F`, `Age`, `Comments`.

The output of this story becomes the input to [[US01-entrant-upload]] once the organiser has assigned race numbers by hand in Excel. That flow, and the entrant upload itself, are unchanged.

## Scope

- **C2C only.** Bluebell 5 already uses a single online-registration spreadsheet in a different shape ([[US33-bluebell-results-processing]]) and its own entry route; not touched here.
- Targets **a single event** chosen by the organiser. The output file is named after that event.
- **One-shot generation.** Not a sync, not a live link. Re-running with new CSVs regenerates the file from scratch.
- **Does not upload the entrants** — it produces the spreadsheet the organiser will later edit (to assign race numbers) and then feed to the existing entrant upload.

## File Formats

### Input — Adults CSV (18+)
Distinguished by the **absence** of the `U18IsDependantOfUser` column. Columns used:

| Column | Use |
|---|---|
| `Forename` | Part of `Name` |
| `Surname` | Part of `Name` |
| `Club` | Fuzzy-matched to canonical club list → `Club Name` |
| `Gender` | Passed through as `M/F` |

`Age_on_Race_Day`, if present, is **ignored** for adults (per [[ages-only-recorded-for-u18]]).

### Input — U18 CSV
Distinguished by the **presence** of the `U18IsDependantOfUser` column. Same four columns as adults are used, plus:

| Column | Use |
|---|---|
| `Age_on_Race_Day` | Written to `Age` |

### Output — `<Event Name> Online Registration.xlsx`
One sheet, one row per entrant, following the layout of the existing [online-registration.xlsx](example-files/online-registration.xlsx) reference:

| Column | Value |
|---|---|
| A — `Race #` | **Blank** (organiser assigns manually before entrant upload) |
| B — `Name` | `PROPER(Forename) + " " + PROPER(Surname)` — see *Generation Rules §1* |
| C — `Club Name` | Canonical club name from the fuzzy match — see *Generation Rules §2* |
| D — `M/F` | `Male` / `Female` from `Gender` |
| E — `Age` | `Age_on_Race_Day` for U18 rows; **blank** for adults. Cell is **shaded by gender** — see *Generation Rules §9* |
| F — `Comments` | Blank (retained for parity with the existing template) |

Header row styled and frozen the same way the reference file is, so downstream use (assigning bibs, then re-uploading) is visually familiar.

A reference file demonstrating the shape and the Age colour coding is [July C2C 2026 Online Registration.xlsx](example-files/july-c2c-2026-online-registration.xlsx) — sample rows include Female U18 (age 12, pink) and Male U18 (ages 14 and 16, blue), with adult rows leaving Age blank and unshaded.

## Generation Rules

1. **Name casing.** Each of `Forename` and `Surname` is transformed with the Excel `PROPER` convention: first letter of each whitespace-separated word uppercased, rest lowercased. `JOHN SMITH` → `John Smith`; `mcdonald` → `Mcdonald`; `o'brien` → `O'Brien`. Known-limitation particles like `Mc…`, `Mac…`, and Scottish/Irish `O'` are treated exactly as Excel's `PROPER` would treat them — no bespoke exception list. The joined `Name` is `Forename + " " + Surname` after transformation.
2. **Club fuzzy match.** For each row the raw `Club` string is matched against the **canonical club list** (see *Club Master List* below). Match is case-insensitive with whitespace and punctuation normalisation, plus fuzzy similarity (e.g. `Pitsea RC` → `Pitsea Running Club`, `Rochford RC` → `Rochford Running Club`, `Basildon AC` → `Basildon Athletics Club`).
   - **High-confidence match** — snap the row's `Club Name` to the canonical name.
   - **Low-confidence / no match** — the row goes into the **preview** for manual resolution; nothing is written to the file yet.
   - **Blank `Club`** — output `Club Name` is blank (unattached runner); no preview entry.
3. **Row order.** All entrants (adults + U18) are output in a **single block**, sorted alphabetically by Surname then Forename (case-insensitive, using the PROPER-cased values so `de Cristofano` and `De Cristofano` sort together). U18s are not grouped separately; the `Age` column makes them identifiable.
4. **Gender values.** Only `Male` and `Female` are expected (the two values the registration platform produces). Anything else is surfaced in the preview as an unrecognised row and blocks generation until resolved (fix the CSV or map the value).
5. **Missing required columns** — if either CSV is missing `Forename`, `Surname`, `Gender`, or `Club`, the file is rejected with a clear error naming the missing column. `Age_on_Race_Day` is required on the U18 CSV only.
6. **Missing files.** Both CSVs are required to generate. If only one is uploaded the preview says which one is missing; the organiser can upload the second file without starting over.
7. **Duplicates.** Same (normalised Name, Gender) appearing in both CSVs is flagged in the preview so the organiser can decide which record to keep. Duplicates **within** one CSV are also flagged.
8. **Filename.** `<event-slug>-online-registration.xlsx` — consistent with the CSV export naming from [[US18-export-results-csv]] (e.g. `crown-to-crown-2026-05-01-online-registration.xlsx`).
9. **Age cell colour coding.** For U18 rows the `Age` cell is shaded so under-18 entrants are visually obvious at a glance and separable by gender when scanning:
   - `M/F = Female` → **light pink** (Office theme *Accent 2* at 80% tint, hex ≈ `#E6B8B7`).
   - `M/F = Male` → **light blue** (Office theme *Accent 1* at 80% tint, hex ≈ `#B8CCE4`).
   - Adult rows (Age blank) → **no fill**.
   These are the exact fills used in the reference file linked above, so a manually-produced spreadsheet and a generated one look identical.

## Club Master List

A canonical clubs table, seeded with the list agreed with the organiser, and manageable in-app (add / rename / deactivate — never hard-delete, so historic entrants keep matching).

**Seed (initial list):**

Aberystwyth AC · Bad Boy Running · Barking Road Runners · Barking Running Club · Basildon Athletics Club · Basildon CC · Benfleet Running Club · Billericay Striders · Braintree & District Athletic Club · Brentwood Beagles Athletics Club · Brentwood Running Club · Castle Point Joggers · Castle Point Young Runners · Chelmsford Athletics · City of Southend On Sea AC · Corringham Running Club · Dagenham 88 Runners · Daws Heath Harriers · Dengie 100 Runners · East Essex Triathlon Club · East London Runners · Fordy Runs Running Club · Harold Wood Running Club · Havering '90 Joggers · Havering AC · Havering Tri · Hockley Trail Runners · Hot Steppers · Ilford AC · JBR Run and Tri Club · Kingswood Running Club · Leigh-on-Sea Striders · London Heathside · Lonely Goat RC · Maldon Soul Runners · Mid Essex Casuals · Nuclear Races Striders · Pewsey Vale Running Club · Phoenix Striders · Pitsea Running Club · Rayleigh Rat Runners · RED Runners · Rochford Running Club · South Woodham Runners · Springfield Striders RC · SS Athletics · St Edmund Pacers · Thames Hare & Hounds · Thurrock Harriers · Thurrock Nomads · Trail Running Association · Vegan Runners UK · Ware Joggers · Witham Running Club · Woman of Wickford

Adding a club made available during preview resolution (§2 below) creates it in the clubs table immediately, so a follow-up CSV with the same spelling matches without further prompting.

## Acceptance Criteria

1. On a **Crown to Crown** event's page a **Generate Online Registration spreadsheet** action accepts two CSVs — the adults file and the U18 file. The action is **not shown for Bluebell 5 events**.
2. The generator identifies which CSV is which by the presence/absence of the `U18IsDependantOfUser` column and reports an error if both files look the same (both adult or both U18).
3. Before writing the .xlsx, a **dry-run preview** shows:
   - The row count that will be output, broken down as adults + U18.
   - **Unresolved club matches** — each raw `Club` value the fuzzy matcher wasn't confident about, with the top canonical suggestion pre-selected, an editable pick-from-list, `Add as new club` (writes back to the clubs table), and `Leave as typed`. Every unresolved club must be answered before generation.
   - **Duplicate names** across the two files or within one file, so the organiser can decide which to keep.
   - **Unrecognised Gender values** and any rows the parser couldn't read; these block generation.
   - Rows whose `Club` matches a canonical name confidently are **not shown** in the preview (nothing to decide).
4. The generated file matches the shape of [online-registration.xlsx](example-files/online-registration.xlsx): columns `Race #`, `Name`, `Club Name`, `M/F`, `Age`, `Comments`; styled header row; frozen first row; `Race #` blank on every row; `Age` blank for adults, filled from `Age_on_Race_Day` for U18s. U18 `Age` cells are colour-coded by gender exactly as in [july-c2c-2026-online-registration.xlsx](example-files/july-c2c-2026-online-registration.xlsx) — light pink for Female, light blue for Male; adult `Age` cells have no fill (see *Generation Rules §9*).
5. Names are output with each of `Forename` and `Surname` `PROPER`-cased and joined by a single space, per *Generation Rules §1*.
6. Clubs in the output are canonical names from the clubs table, with the fuzzy matcher snapping obvious variants (e.g. `Pitsea RC` → `Pitsea Running Club`); low-confidence matches only end up in the file after the organiser resolves them in the preview.
7. Rows are ordered alphabetically by Surname then Forename across the whole file (adults and U18 interleaved), case-insensitive on the PROPER-cased values.
8. The filename follows the pattern `<event-slug>-online-registration.xlsx` (consistent with [[US18-export-results-csv]] naming).
9. A **Clubs admin page** allows the organiser to add / rename / deactivate clubs. Renaming a club never rewrites existing entrant records (their historic free-text `Club` stays as it was); only future matches use the new name. Deactivated clubs are hidden from preview suggestions but never deleted.
10. Re-running the generator on the same event with the same CSVs (after any preview resolutions have been saved to the clubs table) produces the same file with no prompts — proving the preview resolutions actually persisted.
11. The action does **not** create entrants in the database — it only produces the spreadsheet. Entrants still land through [[US01-entrant-upload]] once the organiser has filled in `Race #` and re-uploaded.

## Resolved Decisions

- **Club master list** → seed + manageable table. The list provided seeds the clubs table on first run of the migration; the organiser adds/renames/deactivates in-app thereafter. (Rationale: new clubs like *Hot Steppers* and *St Edmund Pacers* appear regularly; a static list would need a code change each time.)
- **Low-confidence fuzzy match** → **preview and resolve inline**, US35-style. Nothing is written until the organiser confirms; picks made in the preview can also add the raw value as a new canonical club, which persists.
- **Entry point** → new tool on the event page, **C2C only** (hidden on Bluebell events).
- **Row order** → single alphabetical block by Surname then Forename; U18s not grouped separately (the `Age` column makes them identifiable).
- **Name casing** → Excel `PROPER` convention; no bespoke `Mc…` / `O'…` exception list (matches the tool the organiser has been using manually).
- **Race number** → left blank in output (bibs are assigned manually in Excel before the file is fed to [[US01-entrant-upload]]).
- **Age column** → filled only for U18 rows from `Age_on_Race_Day`, blank for adults (per [[ages-only-recorded-for-u18]]).
- **Comments column** → retained (blank) so the output matches the existing template shape without needing to edit it downstream.
- **Age cell colour coding** → U18 rows are shaded by gender (Female = light pink / Accent 2 @ 80% tint; Male = light blue / Accent 1 @ 80% tint); adult rows unshaded. Matches the shading in the July 2026 reference file so a generated file is indistinguishable from one the organiser produced by hand.

## Notes

- Feeds into [[US01-entrant-upload]]: once the organiser has added `Race #` values by hand in Excel, the file goes through the existing entrant upload unchanged.
- Independent of [[US15-runner-registry]] but consistent with it: the same canonical club names surface in both places, so a runner's `Club` on the register lines up with the `Club Name` on this file.
- Complementary to [[US35-volunteer-roster-import]] in shape (upload → dry-run preview with per-row resolutions → commit), so the two follow the same UX pattern.
- **Out of scope:**
  - Bluebell 5 (single-file registration in a different shape, handled by [[US33-bluebell-results-processing]]).
  - Uploading the generated file back into the app automatically — this story stops at producing the spreadsheet; entrant upload stays a separate, explicit step.
  - Retroactively normalising historic entrants' `Club` values to canonical names (renames in the clubs table don't touch existing entrants).
  - Merging duplicate clubs across historic entrants (grooming step, deferred).
