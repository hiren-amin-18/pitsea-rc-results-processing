using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class CourseRecordService : ICourseRecordService
{
    /// <summary>The four record categories, matching the existing top-ten category scheme.</summary>
    public static readonly string[] Categories = ["Male", "Female", "Male U18", "Female U18"];

    private static readonly EventType[] EventTypes = [EventType.CrownToCrown, EventType.Bluebell5];

    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly IRaceResultsService _raceResultsService;
    private readonly ILogger<CourseRecordService> _logger;

    public CourseRecordService(
        IDbContextFactory<RaceResultsDbContext> dbContextFactory,
        IRaceResultsService raceResultsService,
        ILogger<CourseRecordService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _raceResultsService = raceResultsService;
        _logger = logger;
    }

    public IReadOnlyList<CourseRecordSlot> GetCurrentRecordSlots()
    {
        using var db = _dbContextFactory.CreateDbContext();
        var current = db.CourseRecords.Where(r => r.IsCurrent).ToList();

        var slots = new List<CourseRecordSlot>();
        foreach (var eventType in EventTypes)
        {
            foreach (var category in Categories)
            {
                slots.Add(new CourseRecordSlot
                {
                    EventType = eventType,
                    Category = category,
                    Current = current.FirstOrDefault(r => r.EventType == eventType && r.Category == category)
                });
            }
        }

        return slots;
    }

    public EditCourseRecordInput GetRecordForEdit(EventType eventType, string category)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var record = db.CourseRecords.FirstOrDefault(r => r.IsCurrent && r.EventType == eventType && r.Category == category);

        return new EditCourseRecordInput
        {
            EventType = eventType,
            Category = category,
            Time = record is null ? string.Empty : RaceTime.Format(record.Duration),
            RunnerName = record?.RunnerName ?? string.Empty,
            Club = record?.Club,
            EventName = record?.EventName ?? string.Empty,
            EventDate = record?.EventDate ?? DateTime.Today
        };
    }

    public OperationResult UpsertRecord(EditCourseRecordInput input)
    {
        if (!Categories.Contains(input.Category))
        {
            return OperationResult.Fail(new[] { "Unknown category." });
        }
        if (string.IsNullOrWhiteSpace(input.RunnerName))
        {
            return OperationResult.Fail(new[] { "Runner name is required." });
        }
        if (!RaceTime.TryParse(input.Time, out var duration))
        {
            return OperationResult.Fail(new[] { $"Time '{input.Time}' is not valid. Use mm:ss or h:mm:ss." });
        }

        using var db = _dbContextFactory.CreateDbContext();
        var record = db.CourseRecords.FirstOrDefault(r => r.IsCurrent && r.EventType == input.EventType && r.Category == input.Category);
        if (record is null)
        {
            record = new CourseRecord
            {
                EventType = input.EventType,
                Category = input.Category,
                IsCurrent = true,
                CreatedAt = DateTime.UtcNow
            };
            db.CourseRecords.Add(record);
        }

        // Manual correction updates in place (no history entry).
        record.DurationTicks = duration.Ticks;
        record.RunnerName = input.RunnerName.Trim();
        record.Club = input.Club?.Trim() ?? string.Empty;
        record.EventName = input.EventName.Trim();
        record.EventDate = input.EventDate;
        db.SaveChanges();

        _logger.LogInformation("Course record set for {Type}/{Category}: {Time} {Runner}", input.EventType, input.Category, RaceTime.Format(duration), record.RunnerName);
        return OperationResult.Ok($"Course record saved for {input.EventType} {input.Category}.");
    }

    public IReadOnlyList<PendingCourseRecord> GetPendingRecords(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var raceEvent = db.Events.FirstOrDefault(e => e.Id == eventId);
        if (raceEvent is null)
        {
            return Array.Empty<PendingCourseRecord>();
        }

        var current = db.CourseRecords
            .Where(r => r.IsCurrent && r.EventType == raceEvent.EventType)
            .ToList();

        var pending = new List<PendingCourseRecord>();
        foreach (var category in _raceResultsService.GetTopTenByCategory(eventId))
        {
            var winner = category.Results.FirstOrDefault(r => r.Duration.HasValue);
            if (winner is null)
            {
                continue;
            }

            var record = current.FirstOrDefault(r => r.Category == category.Name);
            if (record is null || winner.Duration!.Value < record.Duration)
            {
                pending.Add(new PendingCourseRecord
                {
                    Category = category.Name,
                    Time = RaceTime.Format(winner.Duration!.Value),
                    RunnerName = winner.Name,
                    Club = winner.Club,
                    PreviousTime = record is null ? null : RaceTime.Format(record.Duration)
                });
            }
        }

        return pending;
    }

    public OperationResult ConfirmRecord(int eventId, string category)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var raceEvent = db.Events.FirstOrDefault(e => e.Id == eventId);
        if (raceEvent is null)
        {
            return OperationResult.Fail(new[] { "Event not found." });
        }

        var topTen = _raceResultsService.GetTopTenByCategory(eventId);
        var winner = topTen.FirstOrDefault(c => c.Name == category)?.Results.FirstOrDefault(r => r.Duration.HasValue);
        if (winner is null)
        {
            return OperationResult.Fail(new[] { $"No timed winner found for {category}." });
        }

        var record = db.CourseRecords.FirstOrDefault(r => r.IsCurrent && r.EventType == raceEvent.EventType && r.Category == category);
        if (record is not null && winner.Duration!.Value >= record.Duration)
        {
            return OperationResult.Fail(new[] { "That time no longer beats the current record." });
        }

        // Supersede the standing record, keeping it as history (US22 AC6).
        if (record is not null)
        {
            record.IsCurrent = false;
        }

        db.CourseRecords.Add(new CourseRecord
        {
            EventType = raceEvent.EventType,
            Category = category,
            DurationTicks = winner.Duration!.Value.Ticks,
            RunnerName = winner.Name,
            Club = winner.Club,
            EventName = raceEvent.EventName,
            EventDate = raceEvent.EventDate,
            SourceEventId = raceEvent.Id,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        _logger.LogInformation("New course record confirmed for {Type}/{Category}: {Time} {Runner}", raceEvent.EventType, category, RaceTime.Format(winner.Duration!.Value), winner.Name);
        return OperationResult.Ok($"New course record recorded for {category}: {RaceTime.Format(winner.Duration!.Value)} by {winner.Name}.");
    }
}
