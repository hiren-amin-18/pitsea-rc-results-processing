# US33 - Bluebell 5 Results Processing

**Status:** ✅ Complete

## As a
Race organiser

## I want
The system to process Bluebell 5 entries and results using Bluebell-specific category rules — non-vet (Senior) vs vet — and produce a Bluebell-style PDF with a winners block on the first page

## So that
I can run Bluebell 5 events end-to-end (entry upload → collated results → PDF) without manual post-processing, and award the correct prizes for Senior 1st/2nd/3rd Male & Female plus 1st Vet Male & Vet Female

---

## Context

For Bluebell events:
- Entries come from a single online registration spreadsheet (see [Bluebell 5 2026 Online Registration.xlsx](example-files/bluebell-5-online-registration.xlsx)).
- The spreadsheet has an **Age** column whose value is either:
  - `Male U40` — male runner under 40 on race day (Senior, non-vet)
  - `Female U35` — female runner under 35 on race day (Senior, non-vet)
  - *(blank)* — runner is **at or over** the vet threshold for their gender (M 40+, F 35+ → Vet)
- There are **no under-18 entrants** in Bluebell.
- Prizes are awarded for: 1st / 2nd / 3rd Male, 1st / 2nd / 3rd Female, 1st Vet Male (40+), 1st Vet Female (35+).
- The vet prize is **mutually exclusive with the overall top 3**: if a vet finishes in the top 3 of their gender, the vet prize goes to the next eligible vet in the same gender who is not in the top 3.
- The reference layout for the final PDF output is the `Bluebell 5 Results 2026 - FINAL.pdf` supplied alongside the story (not committed to the repo).

---

## Acceptance Criteria

### 1. Entrant upload (Bluebell event type)
1. When the current event type is `Bluebell 5`, the entry upload accepts the Bluebell registration spreadsheet format and reads the `Age` column.
2. Each entrant is classified as **Senior** (value `Male U40` or `Female U35`) or **Vet** (blank, with gender determining threshold M40+/F35+).
3. Vet status is stored on the entrant record so it survives across results regeneration and PDF export.
4. If a row's `Age` value indicates an under-18 (e.g. `Male U18`, `Female U18`, or any value outside the accepted set), the upload **fails validation** with a clear error identifying the offending row(s). U18 entries are not accepted for Bluebell.
5. The expected column headers on the Bluebell registration sheet are `Race No`, `Name`, `Club Name`, `M/F`, `Age`. The `M/F` column must be present and populated; rows with missing `M/F` fail validation as today.

### 2. Winners calculation
6. After results are collated, the system computes Bluebell winners:
   - **1st / 2nd / 3rd Male** — first three male finishers by chip/finish time, regardless of vet status.
   - **1st / 2nd / 3rd Female** — first three female finishers by finish time, regardless of vet status.
   - **1st Vet Male** — first male finisher flagged Vet who is **not** in the male top 3.
   - **1st Vet Female** — first female finisher flagged Vet who is **not** in the female top 3.
7. DNF / DNS / DSQ finishers (see US16) are excluded from winners selection.
8. If no eligible vet exists in a gender, the corresponding vet slot shows as empty rather than blocking the export.

### 3. Top 10 view (extension of US12)
9. For Bluebell events, the Top 10 view shows **Male, Female, Vet Male, Vet Female** (Vet replaces the U18 categories used by Crown to Crown).
10. Vet Male / Vet Female lists are filtered by vet status only — they are **not** subject to the "skip top 3" rule (that rule applies only to the single prize-winner selection in AC 6).

### 4. PDF export (Bluebell template)
11. The PDF uses the same overall template as the Crown to Crown PDF (US09) — same header style, same results table columns (Position, Time, Race [bib], Name, Gender, Club Name), same pagination footer.
12. The first page contains a **winners block** placed between the title and the results table, laid out in two columns matching the reference PDF:
    - Left column: `1st Male = <name>`, `2nd Male = <name>`, `3rd Male = <name>`, `1st Vet Male = <name>`
    - Right column: `1st Female = <name>`, `2nd Female = <name>`, `3rd Female = <name>`, `1st Vet Female = <name>`
13. Empty winner slots (e.g. no eligible vet) render as `1st Vet Male = -` (or equivalent placeholder) rather than being omitted.
14. The results table itself does **not** include a vet/age column — it matches the C2C column set.
15. Title line uses the event name and date from the current event (per US13), e.g. `BLUEBELL 5 RESULTS 17th MAY 2026`.

### 5. Event-type scoping
16. All Bluebell-specific behaviour (upload format, vet derivation, winners block, Top 10 categories) activates only when the current event's `EventType = Bluebell 5`. Crown to Crown events continue to behave exactly as today.

---

## Out of scope
- Course records on the Bluebell PDF (the reference PDF does not include them).
- Age-grading or vet sub-bands (e.g. V40/V45/V50). Only a single Vet category per gender.
- Changing the C2C PDF or Top 10 view.

## Reference files
- Entry spreadsheet: [bluebell-5-online-registration.xlsx](example-files/bluebell-5-online-registration.xlsx)
