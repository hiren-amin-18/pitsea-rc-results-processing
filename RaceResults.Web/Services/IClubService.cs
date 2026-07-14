using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Canonical clubs registry (US45). Backs the fuzzy matcher on the Online Registration
/// generator and the Clubs admin page.</summary>
public interface IClubService
{
    IReadOnlyList<Club> GetClubs(bool includeInactive = true);
    Club? Find(int id);
    OperationResult Add(string name);
    OperationResult Rename(int id, string newName);
    OperationResult SetActive(int id, bool isActive);
}
