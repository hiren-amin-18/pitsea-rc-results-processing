# User Stories

All user stories are implemented — US01–US25 and US27–US43 (US26 cloud hosting was dropped as not required). Each story file carries a **Status** line (✅ Complete) for tracking. Individual story files are in [`user-stories/`](../user-stories/):

## Implemented

| Story | Title |
|---|---|
| [US01](../user-stories/US01-entrant-upload.md) | Entrant Upload |
| [US02](../user-stories/US02-entrant-file-validation.md) | Entrant File Validation |
| [US03](../user-stories/US03-finish-position-bib-upload.md) | Finish Position & Bib Upload |
| [US04](../user-stories/US04-unmatched-bib-flagging.md) | Unmatched Bib Flagging |
| [US05](../user-stories/US05-finish-position-timing-upload.md) | Timing Upload |
| [US06](../user-stories/US06-finish-position-consistency-validation.md) | Timing Consistency Validation |
| [US07](../user-stories/US07-collated-results-view.md) | Collated Results View |
| [US08](../user-stories/US08-dnf-indication.md) | DNF Indication |
| [US09](../user-stories/US09-export-results-pdf.md) | Export Results to PDF |
| [US10](../user-stories/US10-edit-results-without-reupload.md) | Edit Results Without Re-upload |
| [US11](../user-stories/US11-display-race-stats.md) | Display Race Statistics |
| [US12](../user-stories/US12-top-10-by-category.md) | Top 10 by Category |
| [US13](../user-stories/US13-event-management.md) | Event Management and Event-Scoped Results |
| [US14](../user-stories/US14-champions-of-champions-leaderboard.md) | Champions of Champions Leaderboard |
| [US18](../user-stories/US18-export-results-csv.md) | Export Results to CSV |
| [US27](../user-stories/US27-example-file-links.md) | Example Upload File Links |
| [US19](../user-stories/US19-database-backup-restore.md) | Database Backup and Restore |
| [US17](../user-stories/US17-time-validation-and-analytics.md) | Time Validation and Race Analytics |
| [US15](../user-stories/US15-runner-registry.md) | Runner Registry |
| [US16](../user-stories/US16-finish-status-dns-dnf-dsq.md) | Finish Status (DNS / DNF / DSQ) |
| [US22](../user-stories/US22-course-records-management.md) | Course Records Management |
| [US23](../user-stories/US23-enhanced-race-statistics.md) | Enhanced Race Statistics |
| [US24](../user-stories/US24-season-statistics.md) | Season Statistics and Runner Season Profiles |
| [US20](../user-stories/US20-archive-completed-events.md) | Archive Completed Events |
| [US21](../user-stories/US21-public-results-page.md) | Public Results Page |
| [US31](../user-stories/US31-season-calendar-generator.md) | Season Calendar Generator |
| [US30](../user-stories/US30-end-of-season-review.md) | End of Season Review (volunteer-recognition section now wired up to US29) |
| [US25](../user-stories/US25-app-installer.md) | Application Installer |
| [US28](../user-stories/US28-volunteer-roster.md) | Volunteer Roster Builder |
| [US29](../user-stories/US29-volunteer-stats.md) | Volunteer Statistics |
| [US32](../user-stories/US32-roster-auto-allocation.md) | Automated Roster Allocation |
| [US33](../user-stories/US33-bluebell-results-processing.md) | Bluebell 5 Results Processing |
| [US34](../user-stories/US34-bluebell-volunteer-roster.md) | Bluebell 5 Volunteer Roster & Allocation |
| [US35](../user-stories/US35-volunteer-roster-import.md) | Volunteer Roster Import from Spreadsheet |
| [US36](../user-stories/US36-edit-roster-assignment.md) | Edit Roster Assignments In Place |
| [US37](../user-stories/US37-roster-form-fill-awareness.md) | Roster Form Fill-State Awareness |
| [US38](../user-stories/US38-selective-draft-apply.md) | Selective Draft Apply |
| [US39](../user-stories/US39-volunteer-duplicate-guard-and-merge.md) | Volunteer Duplicate Guard and Merge |
| [US40](../user-stories/US40-persistent-volunteer-preferences.md) | Persistent Volunteer Preferences and Allocate Form Memory |
| [US41](../user-stories/US41-per-role-quick-assign.md) | Per-Role Quick Assign |
| [US42](../user-stories/US42-no-show-tracking.md) | No-Show Tracking |
| [US43](../user-stories/US43-volunteer-register-grooming.md) | Volunteer Register Grooming |

## Story dependencies (for context)

Most stories are independent; these are the non-obvious dependencies the implementation relied on:

- **US22 (Course Records)** and the time-based halves of **US23/US24** depend on **US17 (typed times)**
- **US24 (Season Statistics)** depends on **US15 (Runner Registry)** for all cross-event aggregation; its attendance-based stats need only US15, its time-based stats also need US17
- **US16 (DSQ)** consumes the existing-but-unused `Voided` audit action in Champions scoring
- **US21 (Public Results)** pairs naturally with **US20 (Archiving)** so published pages never change underneath readers
- **US29 (Volunteer Stats)** depends on **US28 (Volunteer Roster)**; the combined run+volunteer recognition stat also benefits from US15
- **US32 (Automated Roster Allocation)** depends on **US28** for the register, role complement, and editable roster, and on the season history **US29** reports on to drive rotation/mix-up; it hands a draft back to US28's roster
- **US34 (Bluebell 5 Volunteer Roster)** extends **US28** and **US32** with the Bluebell role catalogue and preference set; reuses the same volunteer register, allocator engine, applier, and exports; pools season history with C2C and matches roles by name for cross-event rotation
- **US30 (End of Season Review)** is the capstone: it depends on **US24** and **US29**, and degrades gracefully where US16/US17/US22 are absent. The volunteer recognition section is now wired up to US29 (Volunteer of the Season + ever-present volunteers + ran-and-volunteered double commitment); if no volunteer assignments exist for the year, the section is omitted gracefully.
- **US31 (Season Calendar Generator)** is independent (it encodes the C2C date rules in the [Domain Conventions](domain-conventions.md)) and pairs with US20 for season turnover
- **US36–US41 (roster management improvements)** all build on **US28**/**US32**: US36 wires up the update path US28 stubbed; US37/US41 are roster-page ergonomics; US38 filters the US32 draft before the applier; US40 layers volunteer defaults + per-event grid memory under the US32 allocate form without touching the allocator itself
- **US42 (No-Show Tracking)** corrects **US29** stats/ballot counts and feeds honest season history to **US32**; **US43 (Register Grooming)** consumes US42's flag for its recency and count columns
