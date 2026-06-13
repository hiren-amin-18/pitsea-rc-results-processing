using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContextFactory<RaceResultsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=raceresults.db"));

builder.Services.AddSingleton<IRaceResultsService, RaceResultsService>();
builder.Services.AddScoped<IChampionsOfChampionsService, ChampionsOfChampionsService>();
builder.Services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
builder.Services.AddScoped<IRunnerRegistryService, RunnerRegistryService>();
builder.Services.AddScoped<ICourseRecordService, CourseRecordService>();

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Apply EF migrations automatically on startup (skipped during integration tests).
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RaceResultsDbContext>();
    db.Database.Migrate();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    BackfillTimingDurations(db, startupLogger);
    BackfillRunners(db, startupLogger);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exceptionFeature?.Error is not null)
        {
            logger.LogError(exceptionFeature.Error, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
    });
});

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// US17: convert legacy string finish times to typed, sortable durations once. Times that cannot be
// parsed are left untyped and surfaced in a warning report (not silently kept) for manual correction.
static void BackfillTimingDurations(RaceResultsDbContext db, ILogger logger)
{
    var pending = db.TimingRows.Where(t => t.DurationTicks == null).ToList();
    if (pending.Count == 0)
    {
        return;
    }

    var unparseable = new List<TimingRow>();
    var converted = 0;
    foreach (var row in pending)
    {
        if (RaceTime.TryParse(row.Time, out var duration))
        {
            row.DurationTicks = duration.Ticks;
            converted++;
        }
        else
        {
            unparseable.Add(row);
        }
    }

    if (converted > 0)
    {
        db.SaveChanges();
        logger.LogInformation("US17 time migration: converted {Count} stored time(s) to typed durations.", converted);
    }

    if (unparseable.Count > 0)
    {
        var details = string.Join("; ", unparseable.Select(r => $"event {r.EventId} position {r.Position} = '{r.Time}'"));
        logger.LogWarning(
            "US17 time migration: {Count} stored time(s) could not be parsed and need manual correction via Edit: {Details}",
            unparseable.Count, details);
    }
}

// US15: create one persistent runner per distinct normalised name+club across all entrants, and link
// existing entrant rows to them. Runs once for legacy data (new uploads link runners themselves).
static void BackfillRunners(RaceResultsDbContext db, ILogger logger)
{
    var unlinked = db.Entrants.Where(e => e.RunnerId == null).ToList();
    if (unlinked.Count == 0)
    {
        return;
    }

    var byKey = db.Runners.ToList()
        .ToDictionary(r => RunnerIdentity.NormalizeKey(r.Name, r.Club));

    var created = 0;
    foreach (var entrant in unlinked)
    {
        var key = RunnerIdentity.NormalizeKey(entrant.Name, entrant.Club);
        if (!byKey.TryGetValue(key, out var runner))
        {
            runner = new Runner
            {
                Name = entrant.Name,
                Club = entrant.Club,
                Gender = entrant.Gender,
                Age = entrant.Age,
                IsActive = true
            };
            db.Runners.Add(runner);
            byKey[key] = runner;
            created++;
        }

        entrant.Runner = runner;
    }

    db.SaveChanges();
    logger.LogInformation(
        "US15 runner backfill: linked {Linked} entrant(s) to {Created} new runner(s).", unlinked.Count, created);
}

public partial class Program { }
