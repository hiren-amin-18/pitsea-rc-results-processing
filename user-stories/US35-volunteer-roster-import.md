# US35 - Volunteer Roster Import from Previous List

**Status:** ✅ Complete

## As a
Race organiser

## I want
To upload a previous volunteer list (spreadsheet) to an event and have it pre-populate that event's roster — creating any volunteers that don't yet exist and skipping (not overriding) any that already do

## So that
I can carry forward the manual rosters I already keep in Excel without retyping names — turning years of historical volunteer rotas into roster + volunteer-register data the app can use for stats, ballot counts, and future allocations.

---

## Background

[[US28-volunteer-roster]] gave each event a roster the organiser builds by hand, and the **copy-from-previous-event** shortcut covers future events once one C2C roster lives in the app. The gap is the **first time** — and **historical events**: for every past race the roster only exists in Excel. Typing each volunteer in one at a time is the bottleneck stopping us from backfilling history, which is what unlocks meaningful [[US29-volunteer-stats]] numbers and London Marathon ballot counts.

This story adds a one-shot **import**: pick an event, pick the spreadsheet, and the app fills the roster from it — creating volunteers it has never seen, leaving existing volunteers untouched.

The reference file is `Good Friday C2C 2026 Volunteers.xlsx` (the format the organiser has used historically): two columns — **Role** and **Volunteer(s)** — with one row per role, multiple names in a single cell separated by line breaks, and inline annotations like `(to run)`, `(finish)`, `(course)`.

## Scope

- **Both C2C and Bluebell 5** roster import. Same parser, same matcher; only the role alias table differs by event type (see [[US34-bluebell-volunteer-roster]] for the Bluebell role list).
- Targets a **single event** chosen by the organiser; the file does not carry the event date.
- One-shot operation: not a sync, not a live link. The import refuses if the target event already has any roster entries (see *Import Rules §5*), so re-running means clearing the existing roster first.

## File Format (reference)

| Column | Content |
|---|---|
| A — Role | Role name, one per row (e.g. `Lead`, `Timekeeping`, `On The Day Registration`, `Marshal (1)`, `Marshal (5a)`) |
| B — Volunteer(s) | One or more names separated by newlines within the cell |

Inline annotations after a name, in parentheses:
- `(to run)` — volunteer is running after their duty → set the assignment's **running-after** flag.
- `(finish)` / `(course)` — sub-location note for split roles (e.g. First Aid and Prizes) → stored as the assignment's optional note.

Empty `Volunteer(s)` cells mean the role was unfilled for that event — skip, don't create empty assignments.

## Import Rules

1. **Volunteer matching is by name**: case-insensitive, with whitespace collapsed (leading/trailing trimmed, internal runs reduced to single spaces); no fuzzy or initialism handling. If a volunteer with that normalised name already exists in the register, **reuse it as-is** — never override any field on an existing record (member flag, gender, first-aid flag, contact, runner link, active flag all stay exactly as they were). Ambiguous spellings like `Barbara GB` vs a fuller form stay as separate records unless the organiser renames one before re-running.
2. **New volunteers** created by the import default to:
   - **Club member flag: true** (Pitsea RC member) — the historical files are Pitsea-organised, so this is the safe default. No per-volunteer override in the preview; the organiser flips the handful of exceptions (Ian, members' family) on the volunteer page after the import.
   - **Gender: prompted in the import preview** — every newly-created volunteer appears in the preview with a Male / Female / Unknown selector the organiser fills in before committing. Defaults to Unknown; existing volunteers don't appear in this list (their gender is never touched).
   - **First aid trained: false** — even if assigned to a first-aid role in the file, the import doesn't infer the flag; the organiser sets it on the volunteer record.
   - **Active: true.**
   - **Runner link: none.**
3. **Role matching is by name**, with a small set of known aliases per event type so the historical file shape works without editing it:
   - **C2C**: `Marshal (1)` … `Marshal (7)`, `Marshal (5a)` → seed roles `Marshal Point 1` … `Marshal Point 7`, `Marshal Point 5a`.
   - **Bluebell 5**: aliases as needed for the Bluebell role list ([[US34-bluebell-volunteer-roster]]).
   - Exact-match (case-insensitive) for all other roles.
   - Unmatched roles → reported in the import summary; no assignment created.
4. **Assignments are created** for the chosen event, one per (volunteer, role) pair in the file:
   - `(to run)` → running-after flag set on that assignment.
   - `(finish)` / `(course)` (or any other parenthesised note that isn't `to run`) → stored verbatim as the assignment note.
5. **Existing roster on the target event** — the import **refuses** if the event already has any assignments. The preview tells the organiser the event isn't empty and points them at the roster page to clear it first. This keeps the operation unambiguous: import means "fill an empty roster from this file", nothing more.
6. **Eligibility / pre-placed / first-aid-required constraints** on roles are **not enforced during import** — the file is the source of truth for what actually happened. If a first-aid role names a volunteer who isn't flagged as first-aid-trained, the assignment is created anyway and the roster page's existing warning surfaces the mismatch. The organiser can fix the volunteer record (or the roster) afterwards.

## Acceptance Criteria

1. On any event's roster page, an **Import from file** action accepts an `.xlsx` matching the format above. The action is available for both C2C and Bluebell 5 events.
2. The import shows a **dry-run preview** before committing, listing:
   - new volunteers to be created, each with a **Male / Female / Unknown** selector for the organiser to set inline (defaults to Unknown);
   - existing volunteers that will be reused (read-only, no fields editable here);
   - assignments to be created, including any `(to run)` and note annotations parsed from the file;
   - unmatched roles and any rows that couldn't be parsed.
3. If the target event already has any assignments, the import **refuses** and prompts the organiser to clear the roster first — nothing is created.
4. New volunteers are created with the defaults in *Import Rules §2*, using the gender the organiser picked in the preview. Existing volunteers (case-insensitive name match) are reused with **no field changed** (gender, member flag, first-aid flag, contact, runner link, active flag all preserved).
5. Assignments are created on the target event, respecting `(to run)` as the running-after flag and other parenthesised text as the assignment note.
6. Role aliases are recognised per event type (`Marshal (n)` → `Marshal Point n` for C2C; Bluebell aliases as needed); other unmatched role names are surfaced in the summary, not silently dropped.
7. The import **never deletes** volunteers from the register and **never modifies** existing volunteer records.
8. Re-importing the same file into the same event after clearing the roster produces the same result: no duplicate volunteer records (matched by name), assignments recreated from scratch.

## Resolved Decisions

- **Gender for new volunteers** → prompted inline in the import preview (Male / Female / Unknown, default Unknown). Existing volunteers' gender is never touched.
- **Existing roster on the target event** → refuse the import; require the organiser to clear the roster first.
- **Bluebell 5** → in scope. Same parser and matcher, separate role alias table per event type.
- **Name matching tolerance** → case-insensitive + whitespace-collapse, exact otherwise. No fuzzy or initialism matching; the organiser renames in the file or the register if two spellings should be the same person.
- **First-aid role assignments to non-first-aid volunteers** → create the assignment anyway; the roster page's existing warning surfaces the mismatch.
- **Member-flag exceptions** (Ian, members' family) → accept the `member = true` default for all new volunteers; organiser flips the handful of exceptions on the volunteer page after the import.

## Notes

- Foundational dependency: [[US28-volunteer-roster]] (volunteer register, role catalogue, Assignment model, roster page).
- Feeds [[US29-volunteer-stats]] indirectly — every historical event imported adds real volunteering instances (and London Marathon ballot entries) to each person's record.
- Out of scope: CSV/other formats, multi-event imports, syncing back **to** Excel (the existing PDF/Excel export from US28 covers the outbound direction), inferring role complement changes from the file.
