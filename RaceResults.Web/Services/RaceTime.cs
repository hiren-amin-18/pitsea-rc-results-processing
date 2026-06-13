using System.Globalization;

namespace RaceResults.Web.Services;

/// <summary>
/// Parsing, validation, and canonical formatting of race finish times (US17).
/// Comparison and sorting should always use the parsed <see cref="TimeSpan"/>; raw text is for audit only.
/// </summary>
public static class RaceTime
{
    /// <summary>
    /// Parse a finish time, tolerant of common variants:
    /// <c>mm:ss</c>, <c>h:mm:ss</c>, <c>hh:mm:ss.f</c> (fractional seconds allowed).
    /// Rejects non-numeric parts, seconds ≥ 60, and (in h:mm:ss form) minutes ≥ 60.
    /// </summary>
    public static bool TryParse(string? raw, out TimeSpan value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var parts = raw.Trim().Split(':');
        if (parts.Length is < 2 or > 3)
        {
            return false;
        }

        if (parts.Length == 3)
        {
            // hours : minutes : seconds(.fraction)
            if (!TryParseWhole(parts[0], out var hours) ||
                !TryParseWhole(parts[1], out var minutes) ||
                !TryParseSeconds(parts[2], out var seconds))
            {
                return false;
            }

            if (minutes >= 60 || seconds >= 60)
            {
                return false;
            }

            value = TimeSpan.FromSeconds((hours * 3600) + (minutes * 60) + seconds);
            return true;
        }

        // minutes : seconds(.fraction) — minutes may exceed 59 (e.g. a 75:30 finish).
        if (!TryParseWhole(parts[0], out var mins) ||
            !TryParseSeconds(parts[1], out var secs))
        {
            return false;
        }

        if (secs >= 60)
        {
            return false;
        }

        value = TimeSpan.FromSeconds((mins * 60) + secs);
        return true;
    }

    /// <summary>Canonical display: <c>mm:ss</c> under an hour, <c>h:mm:ss</c> from an hour up.</summary>
    public static string Format(TimeSpan value)
    {
        var totalHours = (int)value.TotalHours;
        return totalHours >= 1
            ? $"{totalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}";
    }

    /// <summary>Gap relative to the winner, e.g. <c>+1:23</c> or <c>+1:02:03</c>.</summary>
    public static string FormatGap(TimeSpan gap)
    {
        if (gap <= TimeSpan.Zero)
        {
            return "—";
        }

        var totalHours = (int)gap.TotalHours;
        return totalHours >= 1
            ? $"+{totalHours}:{gap.Minutes:00}:{gap.Seconds:00}"
            : $"+{gap.Minutes}:{gap.Seconds:00}";
    }

    private static bool TryParseWhole(string text, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private static bool TryParseSeconds(string text, out double value)
    {
        // Allow an optional fractional component (e.g. "07.5"), but no sign or exponent.
        return double.TryParse(
            text.Trim(),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value) && value >= 0;
    }
}
