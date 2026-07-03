# Workflow

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

## Volunteer roster

Sits alongside the race workflow and works for past, current, and future events:

```
1. Volunteers page          →  Add volunteers (gender, member, first-aid, optional runner link,
                                usual preferences); same-name creates warn; merge duplicates if needed
2. Volunteer Roles page     →  Populate the restricted-role allow-lists (Lead, Results),
                                set Marshal Point 7's pre-placed volunteer (Ian)
3. Events → Roster          →  Add assignments by hand (dropdowns show live fill state; picking a
                                volunteer pre-fills their usual preferences), use the "+" on a role
                                row to quick-assign several eligible volunteers at once, or click
                                "Allocate draft" — the grid starts from usual preferences and is
                                remembered per event
4. Roster page (Apply step) →  Review the draft, untick any proposals you disagree with, then Apply
5. Roster page              →  Edit assignments in place (role / run-after / note);
                                export to PDF or Excel for race-day briefing
6. After the event          →  Mark no-shows (kept on the roster but excluded from stats, ballot,
                                and fill counts) and add late arrivals to keep stats accurate
7. Volunteer Stats page     →  Season summary + per-volunteer profile + ballot count + CSV
```

## Champions of Champions (Yearly Cumulative)

For Crown to Crown races (May–September season), points are automatically calculated and accumulated. See [Champions of Champions](champions.md) for the full design.

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

Points allocation (10→1 for the top 10) and tie-breaking are documented in [Champions of Champions](champions.md).
