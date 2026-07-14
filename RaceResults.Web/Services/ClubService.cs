using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class ClubService : IClubService
{
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;

    public ClubService(IDbContextFactory<RaceResultsDbContext> factory) => _factory = factory;

    public IReadOnlyList<Club> GetClubs(bool includeInactive = true)
    {
        using var db = _factory.CreateDbContext();
        var q = db.Clubs.AsQueryable();
        if (!includeInactive) q = q.Where(c => c.IsActive);
        return q.OrderBy(c => c.Name).ToList();
    }

    public Club? Find(int id)
    {
        using var db = _factory.CreateDbContext();
        return db.Clubs.FirstOrDefault(c => c.Id == id);
    }

    public OperationResult Add(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return OperationResult.Fail(new[] { "Club name is required." });

        using var db = _factory.CreateDbContext();
        if (db.Clubs.Any(c => c.Name.ToLower() == trimmed.ToLower()))
            return OperationResult.Fail(new[] { $"Club '{trimmed}' already exists." });

        db.Clubs.Add(new Club { Name = trimmed, IsActive = true });
        db.SaveChanges();
        return OperationResult.Ok($"Added club '{trimmed}'.");
    }

    public OperationResult Rename(int id, string newName)
    {
        var trimmed = (newName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return OperationResult.Fail(new[] { "Club name is required." });

        using var db = _factory.CreateDbContext();
        var club = db.Clubs.FirstOrDefault(c => c.Id == id);
        if (club is null) return OperationResult.Fail(new[] { "Club not found." });

        if (db.Clubs.Any(c => c.Id != id && c.Name.ToLower() == trimmed.ToLower()))
            return OperationResult.Fail(new[] { $"Another club is already called '{trimmed}'." });

        club.Name = trimmed;
        db.SaveChanges();
        return OperationResult.Ok($"Renamed to '{trimmed}'.");
    }

    public OperationResult SetActive(int id, bool isActive)
    {
        using var db = _factory.CreateDbContext();
        var club = db.Clubs.FirstOrDefault(c => c.Id == id);
        if (club is null) return OperationResult.Fail(new[] { "Club not found." });

        club.IsActive = isActive;
        db.SaveChanges();
        return OperationResult.Ok(isActive ? $"Reactivated '{club.Name}'." : $"Deactivated '{club.Name}'.");
    }
}
