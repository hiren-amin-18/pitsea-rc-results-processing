using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Builds the end-of-season review (US30). Capstone aggregator over US14/US15/US16/US22/US24.</summary>
public interface ISeasonReviewService
{
    SeasonReview Build(int year);
    byte[] GeneratePdf(int year);
}
