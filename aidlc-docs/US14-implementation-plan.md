# US14 Implementation Plan - Champions of Champions Leaderboard

**Status**: AI-DLC INCEPTION PHASE - Workflow Planning  
**Date**: June 3, 2026  
**User Story**: US14 - Champions of Champions Leaderboard

---

## DELIVERABLES SUMMARY

### Phase 1: Data Layer ✅ COMPLETE
- 2 new models with full audit trail support
- 2 database migrations (Up/Down)
- DbContext configuration with proper relationships and indexes
- **Files**: ChampionOfChampionsScore.cs, PointsAuditLog.cs, migrations (2)

### Phase 2: Business Logic ✅ COMPLETE
- Full scoring engine with points allocation (10→1 for top 10)
- Cumulative leaderboard aggregation
- Tie-breaking by race participation count
- Edit recalculation support with audit trail
- **Files**: IChampionsOfChampionsService.cs, ChampionsOfChampionsService.cs
- **Tests**: ChampionsOfChampionsServiceTests.cs (8 test cases)

### Phase 3: Presentation ✅ COMPLETE
- Controller with leaderboard and PDF export routes
- View model with all required data
- Responsive Razor view with color highlighting
- **Files**: ChampionsController.cs, ChampionsLeaderboardViewModel.cs, Leaderboard.cshtml

### Phase 4: Integration (Partial) ⏳ IN PROGRESS
- ✅ Dependency injection configured
- ⏳ Event completion hook (needs trigger after upload complete)
- ⏳ Edit result hook (needs trigger in UpdateResult flow)

---

## 1. WORKSPACE DETECTION

### Existing Architecture
- **Framework**: ASP.NET Core MVC
- **Database**: Entity Framework Core with SQL migrations
- **Frontend**: Razor views with Bootstrap & Chart.js
- **Language**: C#
- **Test Framework**: xUnit with integration and unit tests
- **Build**: .NET solution (.slnx)

### Existing Features Related to US14
1. **Race Events** - Support for multiple events (EventId scoped)
2. **Top 10 By Category** (US12) - Gets top 10 finishers per category (Male, Female, Male U18, Female U18)
3. **PDF Export** (US09) - Already generates PDFs for race results
4. **Result Editing** (US10) - Can edit historical results with audit tracking needed
5. **Collated Results** - Finish position and timing data available
6. **Entrant Model** - Has `IsU18` property, Gender, Age, Club

---

## 2. REQUIREMENTS FROM US14

### Functional Requirements
- Display yearly Champions of Champions leaderboard
- Scope: Crown to Crown races from May-September
- Categories: Male, Female, Male U18, Female U18
- Points: Top 10 get 10→1 points (1st=10, 2nd=9, etc.), outside top 10 = 0
- Cumulative scoring across all races
- Tie-breaking: By number of races completed (descending)
- Visual highlighting: Top 3 (Gold/Silver/Bronze backgrounds)

### Non-Functional Requirements
- Automatic recalculation when past results are edited
- Audit trail: Track when points awarded vs recalculated
- PDF export with color highlights
- Viewable for each event in season
- No minimum race requirement to appear

### Data Integrity
- Edit to any past race event triggers full leaderboard recalculation
- Maintains audit history of scoring changes

---

## 3. DESIGN UNITS

### Unit 1: Data Model & Database
**Components**:
- `ChampionOfChampionsScore` - Runner's seasonal cumulative score
- `PointsAuditLog` - Track scoring changes over time
- `Event` enhancement - Mark as Crown to Crown + season year
- Migration for new tables

**Dependencies**: RaceResultsDbContext

### Unit 2: Service Layer - Scoring Engine
**Components**:
- `IChampionsOfChampionsService` interface
- Calculate top 10 points per category per event
- Aggregate cumulative scores
- Apply tie-breaking logic
- Recalculate on edit

**Dependencies**: RaceResultsService, Entrant, Top10 logic (US12)

### Unit 3: Service Layer - Leaderboard Query
**Components**:
- Get cumulative leaderboard as of event date
- Filter by season year and Crown to Crown races
- Format for display/export
- Tie-breaking and ranking

**Dependencies**: ChampionsOfChampionsService, database

### Unit 4: Controller & Views
**Components**:
- `ChampionsController` - HTTP endpoints
- `ChampionsLeaderboardViewModel` - Display model
- `Champions/Leaderboard.cshtml` view
- PDF export styling

**Dependencies**: LeaderboardService, PDF generation

### Unit 5: Integration & Event Hooks
**Components**:
- Hook into edit result flow to trigger recalculation
- Audit trail recording
- Result upload completion triggers scoring

**Dependencies**: RaceResultsService, edit result logic

---

## 4. TECHNICAL DECISIONS

| Decision | Rationale |
|----------|-----------|
| **Store cumulative scores** in DB | Faster leaderboard queries, audit trail |
| **Recalculate on edit** not batch | Ensures accuracy, simpler UX |
| **Audit log separate table** | Track when and why scores changed |
| **Event.IsCrownToCrown flag** | Easy to query by season |
| **Tie-breaking by race count** | Simple, fair, no complex tie rules |
| **PDF colors same as web** | Consistent UX across exports |

---

## 5. IMPLEMENTATION SEQUENCE

✅ **COMPLETED**:
1. ✅ **Unit 1**: Created models + migration 
   - ChampionOfChampionsScore model
   - PointsAuditLog model + AuditAction enum
   - EF Core migration (20260603000000)
   - DbContext updated with new tables

2. ✅ **Unit 2**: Built scoring engine
   - IChampionsOfChampionsService interface
   - ChampionsOfChampionsService implementation
   - CalculateAndSaveEventPointsAsync - awards top 10 points per category
   - RecalculateSeasonPointsAsync - recalculates on edit
   - GetLeaderboardAsync - retrieves cumulative leaderboard
   - Comprehensive tie-breaking by race count

3. ✅ **Unit 3**: Leaderboard queries
   - GetCurrentSeasonLeaderboardAsync
   - Proper ranking and tie-breaking
   - ChampionsLeaderboardEntry DTO with highlight class

4. ✅ **Unit 4**: Controller & Views
   - ChampionsController with Leaderboard and ExportPdf actions
   - ChampionsLeaderboardViewModel
   - Champions/Leaderboard.cshtml view with:
     - Category grouping
     - Gold/Silver/Bronze highlighting (CSS)
     - Responsive table display
     - PDF export button

5. ✅ **Unit 5 (Partial)**: Integration & Event Hooks
   - Service registered in DI (Program.cs)
   - Controller configured
   - ⏳ PENDING: Hook into result edit flow
   - ⏳ PENDING: Auto-trigger scoring after events

🔄 **IN PROGRESS**:
- Unit Tests for scoring engine (8 comprehensive test cases)
- Integration tests for edit → recalculation flow

⏳ **TODO**:
1. PDF generation for leaderboard (QuestPDF)
2. Integration point: Trigger scoring after event completion
3. Integration point: Recalculate on result edit
4. Navigation menu link
5. End-to-end integration tests
6. Manual testing of entire flow

---

## 6. TEST COVERAGE PLAN

- **Unit Tests**: Scoring logic, tie-breaking, aggregation
- **Integration Tests**: Full flow - event scoring → leaderboard → edit → recalc
- **UI Tests**: Leaderboard display, colors, PDF generation
- **Edge Cases**: Tied scores, single race, all races same runner, edits changing categories

---

## Next Steps
→ Proceed to Unit 1: Data Model & Database
