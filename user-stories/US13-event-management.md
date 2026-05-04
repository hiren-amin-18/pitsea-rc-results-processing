# US13 - Event Management and Event-Scoped Results

## As a
Race organiser

## I want
To create, edit, select, and delete events so uploaded race data is stored per event and I can restart an event when needed

## So that
I can manage multiple races (Crown to Crown and Bluebell 5) without mixing result sets

---

## Acceptance Criteria

1. Event data model exists with:
- Event Name
- Date
- Event Type (`Crown to Crown` or `Bluebell 5`)

2. Event management UI allows:
- Creating a new event
- Editing an existing event
- Setting an event as current
- Deleting an event and all associated result data

3. Result data (entrants, finish rows, timings, collated views, stats, top 10, edits, PDF) is scoped to the current event.

4. Current event is visible in the application UI.

5. PDF title line uses current event name and date.

6. Event deletion removes only data associated with that event.

7. If the current event is deleted, another event becomes current; if none remain, a default current event is created.

8. Existing pre-event data is reset as part of migration to event-based storage.
