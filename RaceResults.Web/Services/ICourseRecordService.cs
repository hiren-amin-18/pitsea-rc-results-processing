using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Course-record storage, management, detection, and confirmation (US22).</summary>
public interface ICourseRecordService
{
    /// <summary>All event-type/category slots with their current record (if any), for the management list.</summary>
    IReadOnlyList<CourseRecordSlot> GetCurrentRecordSlots();

    /// <summary>Pre-filled edit input for a slot (existing record or blank).</summary>
    EditCourseRecordInput GetRecordForEdit(EventType eventType, string category);

    /// <summary>Set or correct the current record for a slot in place (no history entry).</summary>
    OperationResult UpsertRecord(EditCourseRecordInput input);

    /// <summary>Category winners in the event whose typed time beats (or first-sets) the stored record (AC4, AC7).</summary>
    IReadOnlyList<PendingCourseRecord> GetPendingRecords(int eventId);

    /// <summary>Confirm a new record for the category winner of the event: supersede the old record and keep history (AC4, AC6).</summary>
    OperationResult ConfirmRecord(int eventId, string category);
}
