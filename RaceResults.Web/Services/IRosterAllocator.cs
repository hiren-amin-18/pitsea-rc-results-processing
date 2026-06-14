using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Stateless rules engine that drafts a roster from candidate sign-ups + season history (US32).</summary>
public interface IRosterAllocator
{
    AllocationDraft Propose(int eventId, IReadOnlyList<AllocationCandidate> candidates);
}
