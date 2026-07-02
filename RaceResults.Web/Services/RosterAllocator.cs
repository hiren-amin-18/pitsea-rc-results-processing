using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class RosterAllocator : IRosterAllocator
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;

    public RosterAllocator(IDbContextFactory<RaceResultsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    private static string Normalise(string roleName) => (roleName ?? string.Empty).Trim().ToLowerInvariant();

    public AllocationDraft Propose(int eventId, IReadOnlyList<AllocationCandidate> candidates)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var raceEvent = db.Events.FirstOrDefault(e => e.Id == eventId)
            ?? throw new InvalidOperationException($"Event {eventId} not found.");

        var draft = new AllocationDraft
        {
            EventId = eventId,
            EventName = raceEvent.EventName,
            EventDate = raceEvent.EventDate
        };

        if (candidates.Count == 0)
        {
            draft.Report.Notes.Add("No candidates provided.");
            return draft;
        }

        candidates = candidates.DistinctBy(c => c.VolunteerId).ToList();

        var volunteers = db.Volunteers
            .Where(v => candidates.Select(c => c.VolunteerId).Contains(v.Id))
            .ToDictionary(v => v.Id);

        var roles = db.VolunteerRoles
            .Where(r => r.EventType == raceEvent.EventType && r.IsActive)
            .OrderBy(r => r.Category).ThenBy(r => r.SortOrder)
            .ToList();

        var allowLists = db.VolunteerRoleEligibilities
            .Where(e => roles.Select(r => r.Id).Contains(e.VolunteerRoleId))
            .GroupBy(e => e.VolunteerRoleId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.VolunteerId).ToHashSet());

        // Season history pools across event types (US34 AC6 — Bluebell + C2C in the same year are
        // treated as one fairness pool). No-shows (US42) never happened for fairness purposes.
        var seasonHistory = db.VolunteerAssignments
            .Where(a => candidates.Select(c => c.VolunteerId).Contains(a.VolunteerId) && !a.IsNoShow)
            .Join(db.Events,
                a => a.EventId,
                e => e.Id,
                (a, e) => new { a.VolunteerId, a.VolunteerRoleId, a.WillRunAfter, EventDate = e.EventDate, EventType = e.EventType })
            .Where(x => x.EventDate.Year == raceEvent.EventDate.Year
                && x.EventDate < raceEvent.EventDate)
            .ToList();

        var runAfterCount = candidates.ToDictionary(
            c => c.VolunteerId,
            c => seasonHistory.Count(h => h.VolunteerId == c.VolunteerId && h.WillRunAfter));

        // Role-name lookup so "Timekeeping at C2C" and "Timekeeping at Bluebell" collapse for mix-up
        // purposes (US34 AC7). Built once from all roles referenced in season history.
        var historyRoleIds = seasonHistory.Select(h => h.VolunteerRoleId).Distinct().ToList();
        var roleNameById = db.VolunteerRoles
            .Where(r => historyRoleIds.Contains(r.Id))
            .ToDictionary(r => r.Id, r => Normalise(r.Name));

        var recentRoleNameByVolunteer = seasonHistory
            .GroupBy(h => h.VolunteerId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => roleNameById.TryGetValue(x.VolunteerRoleId, out var n) ? n : string.Empty)
                      .ToDictionary(x => x.Key, x => x.Max(y => y.EventDate)));

        DateTime MostRecentInRole(int volunteerId, VolunteerRole role) =>
            recentRoleNameByVolunteer.TryGetValue(volunteerId, out var m)
                && m.TryGetValue(Normalise(role.Name), out var d) ? d : DateTime.MinValue;

        // ---------- Role partitions ----------
        // Generic-preference sentinels (e.g. "Marshal (any point)") are never physical slots.
        var genericPrefRoleIds = roles.Where(r => r.IsGenericPreference).Select(r => r.Id).ToHashSet();

        // Optional roles are deferred until after all mandatory minimums are met.
        var optionalRoles = roles.Where(r => r.IsOptional && !r.IsGenericPreference).ToList();

        // C2C only: non-runners at OTD Registration / Number Collection dual-cover the finish line.
        // Those slots are reserved and excluded from normal passes so dual assignment fills them.
        var finishLineRoles = raceEvent.EventType == EventType.CrownToCrown
            ? roles.Where(r => r.Name is "Finish Line Funnel" or "Finish Line Results").ToList()
            : new List<VolunteerRole>();
        var finishLineRoleIds = finishLineRoles.Select(r => r.Id).ToHashSet();
        var otdNcRoleIds = raceEvent.EventType == EventType.CrownToCrown
            ? roles.Where(r => r.Name is "On The Day Registration" or "Number Collection")
                   .Select(r => r.Id).ToHashSet()
            : new HashSet<int>();

        // Core: mandatory roles that receive volunteers in passes 1–7.
        // Excludes optional, generic-preference sentinels, and finish-line reserved slots.
        var coreRoles = roles
            .Where(r => !r.IsOptional && !r.IsGenericPreference && !finishLineRoleIds.Contains(r.Id))
            .ToList();

        // ---------- Slots ----------
        // Initialise from all roles so finish-line and optional slot counts are tracked even if not
        // filled via normal passes.
        var slots = roles.ToDictionary(r => r.Id, r => r.DefaultCount);
        var runAfterSlots = roles.ToDictionary(r => r.Id, r => r.RunAfterCapacity);
        var placed = new HashSet<int>();

        var candidatesById = candidates.ToDictionary(c => c.VolunteerId);

        void Place(AllocationCandidate cand, VolunteerRole role, AllocationReason reason, bool willRunAfter = false)
        {
            if (!volunteers.TryGetValue(cand.VolunteerId, out var v)) return;
            draft.Proposals.Add(new ProposedAssignment
            {
                VolunteerId = v.Id,
                VolunteerName = v.Name,
                VolunteerRoleId = role.Id,
                RoleName = role.Name,
                Category = role.Category,
                WillRunAfter = willRunAfter,
                Reason = reason,
                PreferredRoleId = cand.PreferredRoleId,
                WantsToRunAfter = cand.WantsToRunAfter,
                WantsNearFinish = cand.WantsNearFinish,
                CantWalkFar = cand.CantWalkFar,
                WantsSeated = cand.WantsSeated,
                WantsRaceHq = cand.WantsRaceHq,
                AnyRole = cand.AnyRole
            });
            slots[role.Id]--;
            if (willRunAfter) runAfterSlots[role.Id]--;
            placed.Add(v.Id);
        }

        bool CanAssign(AllocationCandidate cand, VolunteerRole role, bool wantsRunAfter)
        {
            if (slots[role.Id] <= 0) return false;
            return EligibleIgnoringSlots(cand, role)
                && (!wantsRunAfter || runAfterSlots[role.Id] > 0);
        }

        bool EligibleIgnoringSlots(AllocationCandidate cand, VolunteerRole role)
        {
            if (!volunteers.TryGetValue(cand.VolunteerId, out var v) || !v.IsActive) return false;
            if (role.RequiresFirstAid && !v.IsFirstAidTrained) return false;
            if (role.HasEligibilityRestriction)
            {
                if (!allowLists.TryGetValue(role.Id, out var list) || !list.Contains(v.Id)) return false;
            }
            return true;
        }

        // ---------- 1. Pre-placements ----------
        foreach (var role in roles.Where(r => r.PrePlacedVolunteerId.HasValue))
        {
            var preId = role.PrePlacedVolunteerId!.Value;
            if (!candidatesById.TryGetValue(preId, out var cand)) continue;
            if (placed.Contains(preId)) continue;
            if (CanAssign(cand, role, wantsRunAfter: false))
            {
                Place(cand, role, AllocationReason.PrePlaced);
            }
        }

        // ---------- 2. Restricted roles ----------
        foreach (var role in coreRoles.Where(r => r.HasEligibilityRestriction))
        {
            while (slots[role.Id] > 0)
            {
                var pick = candidates
                    .Where(c => !placed.Contains(c.VolunteerId))
                    .Where(c => CanAssign(c, role, c.WantsToRunAfter && runAfterSlots[role.Id] > 0))
                    .OrderByDescending(c => c.PreferredRoleId == role.Id)
                    .ThenBy(c => MostRecentInRole(c.VolunteerId, role))
                    .FirstOrDefault();
                if (pick == null) break;
                var willRunAfter = pick.WantsToRunAfter && runAfterSlots[role.Id] > 0;
                Place(pick, role, AllocationReason.Eligibility, willRunAfter);
            }
        }

        // ---------- 3. Run-after rotation ----------
        foreach (var role in coreRoles.Where(r => r.RunAfterCapacity > 0).OrderBy(r => r.Category).ThenBy(r => r.SortOrder))
        {
            while (slots[role.Id] > 0 && runAfterSlots[role.Id] > 0)
            {
                var pick = candidates
                    .Where(c => !placed.Contains(c.VolunteerId))
                    .Where(c => c.WantsToRunAfter)
                    .Where(c => CanAssign(c, role, wantsRunAfter: true))
                    .OrderBy(c => runAfterCount.TryGetValue(c.VolunteerId, out var s) ? s : 0)
                    .ThenBy(c => volunteers[c.VolunteerId].Name)
                    .FirstOrDefault();
                if (pick == null) break;
                Place(pick, role, AllocationReason.RunAfterRotation, willRunAfter: true);
            }
        }

        // ---------- 4. Preferences ----------

        // 4a. Specific role preference.
        // Generic-preference sentinels (e.g. "Marshal any point") are skipped here — handled in 4a₂.
        foreach (var cand in candidates.Where(c => !placed.Contains(c.VolunteerId) && c.PreferredRoleId.HasValue))
        {
            var role = coreRoles.FirstOrDefault(r => r.Id == cand.PreferredRoleId);
            if (role != null && CanAssign(cand, role, cand.WantsToRunAfter))
            {
                var willRunAfter = cand.WantsToRunAfter && runAfterSlots[role.Id] > 0;
                Place(cand, role, AllocationReason.Preference, willRunAfter);
            }
        }

        // 4a₂. Generic marshal preference: fill any open marshal point, least-recently-done first.
        // Runs after all specific marshal-point preferences so named points are honoured first.
        foreach (var cand in candidates.Where(c => !placed.Contains(c.VolunteerId)
            && c.PreferredRoleId.HasValue && genericPrefRoleIds.Contains(c.PreferredRoleId!.Value)))
        {
            var role = coreRoles
                .Where(r => r.Name.StartsWith("Marshal Point") && CanAssign(cand, r, cand.WantsToRunAfter))
                .OrderBy(r => MostRecentInRole(cand.VolunteerId, r))
                .FirstOrDefault();
            if (role != null)
            {
                var willRunAfter = cand.WantsToRunAfter && runAfterSlots[role.Id] > 0;
                Place(cand, role, AllocationReason.Preference, willRunAfter);
            }
        }

        // 4b. Can't walk far → Marshal 1 / 2 (fallback 6).
        foreach (var cand in candidates.Where(c => !placed.Contains(c.VolunteerId) && c.CantWalkFar))
        {
            var primary = new[] { "Marshal Point 1", "Marshal Point 2" };
            var fallback = new[] { "Marshal Point 6" };
            var role = coreRoles.FirstOrDefault(r => primary.Contains(r.Name) && CanAssign(cand, r, cand.WantsToRunAfter))
                    ?? coreRoles.FirstOrDefault(r => fallback.Contains(r.Name) && CanAssign(cand, r, cand.WantsToRunAfter));
            if (role != null)
            {
                var willRunAfter = cand.WantsToRunAfter && runAfterSlots[role.Id] > 0;
                Place(cand, role, AllocationReason.Preference, willRunAfter);
            }
        }

        // 4c. Seated → Number Collection / On The Day Registration.
        foreach (var cand in candidates.Where(c => !placed.Contains(c.VolunteerId) && c.WantsSeated))
        {
            var seatedRoles = new[] { "Number Collection", "On The Day Registration" };
            var role = coreRoles.FirstOrDefault(r => seatedRoles.Contains(r.Name) && CanAssign(cand, r, cand.WantsToRunAfter));
            if (role != null)
            {
                var willRunAfter = cand.WantsToRunAfter && runAfterSlots[role.Id] > 0;
                Place(cand, role, AllocationReason.Preference, willRunAfter);
            }
        }

        // 4d. Near finish → any Finish Area core role (finish-line roles are reserved for dual assignment).
        foreach (var cand in candidates.Where(c => !placed.Contains(c.VolunteerId) && c.WantsNearFinish))
        {
            var role = coreRoles.Where(r => r.Category == RoleCategory.FinishArea)
                .FirstOrDefault(r => CanAssign(cand, r, cand.WantsToRunAfter));
            if (role != null)
            {
                var willRunAfter = cand.WantsToRunAfter && runAfterSlots[role.Id] > 0;
                Place(cand, role, AllocationReason.Preference, willRunAfter);
            }
        }

        // 4e. Race HQ (Bluebell) → any Race HQ role.
        foreach (var cand in candidates.Where(c => !placed.Contains(c.VolunteerId) && c.WantsRaceHq))
        {
            var role = coreRoles.Where(r => r.Category == RoleCategory.RaceHq)
                .FirstOrDefault(r => CanAssign(cand, r, cand.WantsToRunAfter));
            if (role != null)
            {
                var willRunAfter = cand.WantsToRunAfter && runAfterSlots[role.Id] > 0;
                Place(cand, role, AllocationReason.Preference, willRunAfter);
            }
        }

        // ---------- 5. Mix-up across the season ----------
        // For each unplaced candidate (excluding AnyRole, those go last), pick the role they've done least recently.
        var seasonRotation = candidates
            .Where(c => !placed.Contains(c.VolunteerId) && !c.AnyRole)
            .ToList();

        foreach (var cand in seasonRotation)
        {
            var openRoles = coreRoles.Where(r => slots[r.Id] > 0 && CanAssign(cand, r, cand.WantsToRunAfter)).ToList();
            if (openRoles.Count == 0) continue;
            var ordered = openRoles
                .OrderBy(r => MostRecentInRole(cand.VolunteerId, r))
                .ThenBy(r => r.Category).ThenBy(r => r.SortOrder)
                .ToList();
            var role = ordered.First();
            var willRunAfter = cand.WantsToRunAfter && runAfterSlots[role.Id] > 0;
            Place(cand, role, AllocationReason.Mix, willRunAfter);
        }

        // ---------- 6. Marshal gender mix ----------
        // Best-effort post-pass: for each marshal point, if both proposed people are same gender AND there is
        // an unplaced candidate of the opposite gender who could fit and another marshal point with a same-gender
        // unplaced candidate, swap.
        foreach (var marshalRole in coreRoles.Where(r => r.Name.StartsWith("Marshal Point")))
        {
            var assigned = draft.Proposals.Where(p => p.VolunteerRoleId == marshalRole.Id).ToList();
            if (assigned.Count < 2) continue;
            var genders = assigned.Select(p => volunteers[p.VolunteerId].Gender).ToList();
            if (genders.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1) continue; // already mixed
            var theGender = genders.First();
            var swap = candidates
                .Where(c => !placed.Contains(c.VolunteerId))
                .Where(c => volunteers.TryGetValue(c.VolunteerId, out var v)
                    && !string.IsNullOrWhiteSpace(v.Gender)
                    && !v.Gender.Equals(theGender, StringComparison.OrdinalIgnoreCase))
                .Where(c => EligibleIgnoringSlots(c, marshalRole))
                .FirstOrDefault();
            if (swap == null) continue;
            // True swap: drop one of the same-gender assignees, place the opposite-gender candidate at this
            // marshal point, and re-home the dropped person in their best remaining core role. Skip the swap
            // entirely if there is nowhere left for the dropped person.
            var dropped = assigned.Last();
            var droppedCand = candidatesById[dropped.VolunteerId];
            var droppedAltRole = coreRoles
                .Where(r => r.Id != marshalRole.Id
                            && slots[r.Id] > 0
                            && CanAssign(droppedCand, r, droppedCand.WantsToRunAfter))
                .OrderBy(r => MostRecentInRole(droppedCand.VolunteerId, r))
                .ThenBy(r => r.Category).ThenBy(r => r.SortOrder)
                .FirstOrDefault();
            if (droppedAltRole == null) continue;

            draft.Proposals.Remove(dropped);
            slots[marshalRole.Id]++;
            if (dropped.WillRunAfter) runAfterSlots[marshalRole.Id]++;
            placed.Remove(dropped.VolunteerId);

            var droppedWillRunAfter = droppedCand.WantsToRunAfter && runAfterSlots[droppedAltRole.Id] > 0;
            Place(droppedCand, droppedAltRole, AllocationReason.Mix, droppedWillRunAfter);

            var swapCand = candidatesById[swap.VolunteerId];
            var willRunAfter = swapCand.WantsToRunAfter && runAfterSlots[marshalRole.Id] > 0;
            Place(swapCand, marshalRole, AllocationReason.Mix, willRunAfter);
        }

        // ---------- 7. Fill core remainder (AnyRole + anyone left) ----------
        foreach (var cand in candidates.Where(c => !placed.Contains(c.VolunteerId)))
        {
            var openRoles = coreRoles.Where(r => slots[r.Id] > 0 && CanAssign(cand, r, cand.WantsToRunAfter)).ToList();
            if (openRoles.Count == 0) continue;
            var role = openRoles
                .OrderBy(r => MostRecentInRole(cand.VolunteerId, r))
                .First();
            var willRunAfter = cand.WantsToRunAfter && runAfterSlots[role.Id] > 0;
            Place(cand, role, AllocationReason.Fill, willRunAfter);
        }

        // ---------- 7b. Finish line dual assignment (C2C only) ----------
        // Non-runners already placed at OTD Registration or Number Collection are additionally assigned
        // to cover Finish Line Funnel / Finish Line Results (minimum 3 across both roles).
        // These volunteers keep their primary OTD/NC assignment; this only adds a second entry.
        if (finishLineRoles.Count > 0)
        {
            var otdNcNonRunners = draft.Proposals
                .Where(p => otdNcRoleIds.Contains(p.VolunteerRoleId) && !p.WillRunAfter)
                .ToList();

            int dualAssigned = 0;
            foreach (var flRole in finishLineRoles)
            {
                var picks = otdNcNonRunners.Take(slots[flRole.Id]).ToList();
                foreach (var pick in picks)
                {
                    if (!candidatesById.TryGetValue(pick.VolunteerId, out var cand)) continue;
                    draft.Proposals.Add(new ProposedAssignment
                    {
                        VolunteerId = pick.VolunteerId,
                        VolunteerName = pick.VolunteerName,
                        VolunteerRoleId = flRole.Id,
                        RoleName = flRole.Name,
                        Category = flRole.Category,
                        WillRunAfter = false,
                        Reason = AllocationReason.FinishLine,
                        PreferredRoleId = cand.PreferredRoleId,
                        WantsToRunAfter = cand.WantsToRunAfter,
                        WantsNearFinish = cand.WantsNearFinish,
                        CantWalkFar = cand.CantWalkFar,
                        WantsSeated = cand.WantsSeated,
                        WantsRaceHq = cand.WantsRaceHq,
                        AnyRole = cand.AnyRole
                    });
                    slots[flRole.Id]--;
                    dualAssigned++;
                }
                otdNcNonRunners = otdNcNonRunners.Skip(picks.Count).ToList();
            }

            int flMinTotal = finishLineRoles.Sum(r => r.MinCount);
            if (dualAssigned < flMinTotal)
            {
                draft.Report.Notes.Add(
                    $"Finish line dual assignment: only {dualAssigned}/{flMinTotal} minimum finish-line slots covered " +
                    "from OTD Registration/Number Collection non-runners. Consider moving a non-runner into OTD/NC.");
            }
        }

        // ---------- 8. Optional roles (deferred until all mandatory minimums are met) ----------
        bool mandatoryMet = coreRoles.All(r => (r.DefaultCount - slots[r.Id]) >= r.MinCount);
        if (mandatoryMet && optionalRoles.Count > 0)
        {
            foreach (var cand in candidates.Where(c => !placed.Contains(c.VolunteerId)))
            {
                // Honour an explicit preference for an optional role if available.
                var prefOpt = optionalRoles.FirstOrDefault(r => r.Id == cand.PreferredRoleId
                    && CanAssign(cand, r, cand.WantsToRunAfter));
                var role = prefOpt
                    ?? optionalRoles.FirstOrDefault(r => slots[r.Id] > 0 && CanAssign(cand, r, cand.WantsToRunAfter));
                if (role == null) continue;
                var willRunAfter = cand.WantsToRunAfter && runAfterSlots[role.Id] > 0;
                Place(cand, role, AllocationReason.Fill, willRunAfter);
            }
        }
        else if (!mandatoryMet && optionalRoles.Count > 0)
        {
            var optionalNames = string.Join(", ", optionalRoles.Select(r => r.Name));
            draft.Report.Notes.Add(
                $"Optional roles not filled ({optionalNames}): one or more mandatory roles are below minimum count.");
        }

        // ---------- Report unplaced candidates ----------
        foreach (var cand in candidates.Where(c => !placed.Contains(c.VolunteerId)))
        {
            var reason = (!mandatoryMet && optionalRoles.Count > 0)
                ? "Mandatory roles not fully staffed; optional slots not opened."
                : "No compatible open role left.";
            draft.Report.UnplacedCandidates.Add(new UnplacedCandidate
            {
                VolunteerId = cand.VolunteerId,
                Name = volunteers.TryGetValue(cand.VolunteerId, out var v) ? v.Name : "(unknown)",
                Reason = reason
            });
        }

        // ---------- Report unfilled + unhonoured prefs ----------
        foreach (var role in roles.Where(r => !r.IsOptional && !r.IsGenericPreference))
        {
            var assigned = draft.Proposals.Count(p => p.VolunteerRoleId == role.Id);
            if (assigned < role.DefaultCount)
            {
                draft.Report.UnfilledRoles.Add(new UnfilledRole
                {
                    RoleName = role.Name,
                    Assigned = assigned,
                    Default = role.DefaultCount,
                    Min = role.MinCount
                });
            }
        }

        foreach (var cand in candidates)
        {
            // Use the first proposal (primary assignment) for preference-honoured checks.
            var prop = draft.Proposals.FirstOrDefault(p => p.VolunteerId == cand.VolunteerId);
            if (prop == null) continue;
            var name = volunteers.TryGetValue(cand.VolunteerId, out var v) ? v.Name : cand.VolunteerId.ToString();

            if (cand.PreferredRoleId.HasValue && prop.VolunteerRoleId != cand.PreferredRoleId)
            {
                var prefRole = roles.FirstOrDefault(r => r.Id == cand.PreferredRoleId);
                // Generic preference ("Marshal any point") is honoured if placed at any marshal point.
                bool honoured = prefRole?.IsGenericPreference == true
                    && prop.RoleName.StartsWith("Marshal Point");
                if (!honoured)
                {
                    var preferredName = prefRole?.Name ?? "(unknown)";
                    draft.Report.PreferencesNotHonoured.Add($"{name}: wanted '{preferredName}', got '{prop.RoleName}'.");
                }
            }
            if (cand.WantsToRunAfter && !prop.WillRunAfter)
            {
                draft.Report.PreferencesNotHonoured.Add($"{name}: wanted to run after, not allocated to a run-after slot.");
            }
        }

        return draft;
    }
}
