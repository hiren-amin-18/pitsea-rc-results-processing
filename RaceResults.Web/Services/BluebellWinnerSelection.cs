using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>
/// Bluebell 5 prize selection (US33 AC6). Top 3 male and female by finish time, plus the first
/// vet of each gender whose position is outside the overall top 3.
/// </summary>
public static class BluebellWinnerSelection
{
    public static BluebellWinners Select(IReadOnlyList<ResultRecord> collated)
    {
        var maleTop3 = collated.Where(r => r.Entrant is not null && IsMale(r.Entrant.Gender)).Take(3).ToList();
        var femaleTop3 = collated.Where(r => r.Entrant is not null && IsFemale(r.Entrant.Gender)).Take(3).ToList();

        var maleTop3Positions = new HashSet<int>(maleTop3.Select(r => r.DisplayPosition));
        var femaleTop3Positions = new HashSet<int>(femaleTop3.Select(r => r.DisplayPosition));

        var vetMale = collated.FirstOrDefault(r =>
            r.Entrant is not null
            && IsMale(r.Entrant.Gender)
            && r.Entrant.IsVet
            && !maleTop3Positions.Contains(r.DisplayPosition));

        var vetFemale = collated.FirstOrDefault(r =>
            r.Entrant is not null
            && IsFemale(r.Entrant.Gender)
            && r.Entrant.IsVet
            && !femaleTop3Positions.Contains(r.DisplayPosition));

        return new BluebellWinners(maleTop3, femaleTop3, vetMale, vetFemale);
    }

    private static bool IsMale(string gender) => gender.Trim().StartsWith("m", StringComparison.OrdinalIgnoreCase);
    private static bool IsFemale(string gender) => gender.Trim().StartsWith("f", StringComparison.OrdinalIgnoreCase);
}

public record BluebellWinners(
    IReadOnlyList<ResultRecord> MaleTop3,
    IReadOnlyList<ResultRecord> FemaleTop3,
    ResultRecord? VetMale,
    ResultRecord? VetFemale);
