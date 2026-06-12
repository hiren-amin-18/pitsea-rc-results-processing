# US14 - Champions of Champions Leaderboard

**Status:** ✅ Complete

As a race organizer, I want to display a yearly Champions of Champions leaderboard across all Crown to Crown races (May to September), so that I can highlight consistent top performers and generate cumulative rankings.

## Overview
The Champions of Champions is an annual competition spanning all Crown to Crown races from May through September inclusive. Runners' performances across all races in this period are scored and ranked cumulatively.

## Scoring System
- Top 10 finishers in each category (Male, Female, Male U18, Female U18) earn points based on their finish position
- **Points awarded**: 1st = 10 points, 2nd = 9 points, 3rd = 8 points, ..., 10th = 1 point
- Runners finishing outside the top 10 earn 0 points
- Points are accumulated across all races in the season

## Categories
- Male (18 and over)
- Female (18 and over)
- Male (U18)
- Female (U18)

## Leaderboard Display
- Show cumulative scores for each runner across all events
- Display rank, runner name, club, category, and total points
- When cumulative scores are tied, rank by number of races completed (more races = better ranking)
- Display top 3 performers with visual highlighting:
  - **Gold**: 1st place (gold background)
  - **Silver**: 2nd place (silver background)
  - **Bronze**: 3rd place (bronze/copper background)
- Update automatically after each event is completed

## PDF Export
- Export the current cumulative leaderboard as of the selected event to PDF
- Include the same visual highlighting for top 3 (gold, silver, bronze)
- Show event date and race name in the report header

## Acceptance Criteria
1. After each Crown to Crown race, the leaderboard is automatically updated with new scores
2. All runners who scored points appear on the leaderboard (no minimum race requirement)
3. Scores are correctly calculated: top 10 finishers receive 10 down to 1 point
4. Ties are broken by number of races completed (descending order)
5. Top 3 ranked runners are visually distinguished with appropriate background colors
6. PDF export includes all cumulative data as of the selected event
7. Leaderboard is viewable for each individual race event in the season
8. When a past race result is edited, the leaderboard automatically recalculates affected runner scores
9. The system tracks (timestamps/audit log) when points were awarded vs when they were recalculated

## Edit Behavior & Recalculation
- If any past race result is edited (finish position, runner category, or any scoring factor), the Champions of Champions leaderboard **automatically recalculates** with the updated points
- The system **tracks when points were awarded vs when they were recalculated** for transparency and audit purposes
- This ensures the leaderboard always reflects the current, accurate data across all races

## Notes
- This feature is specific to Crown to Crown races during the May-September season
- The leaderboard resets annually
- Only races explicitly marked as "Crown to Crown" events are included in the scoring
