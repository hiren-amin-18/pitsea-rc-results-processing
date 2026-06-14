namespace RaceResults.Web.Services;

/// <summary>Crown to Crown fixture date rules (US31). Pure: same inputs → same outputs.</summary>
public static class SeasonCalendar
{
    /// <summary>Easter Sunday for a Gregorian year via the Anonymous Gregorian (Meeus) algorithm.</summary>
    public static DateTime ComputeEasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var day = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateTime(year, month, day);
    }

    /// <summary>Good Friday for the year (Easter Sunday minus two days).</summary>
    public static DateTime GoodFriday(int year) => ComputeEasterSunday(year).AddDays(-2);

    /// <summary>First date in the month falling on the given day-of-week.</summary>
    public static DateTime FirstWeekdayOfMonth(int year, int month, DayOfWeek day)
    {
        var firstOfMonth = new DateTime(year, month, 1);
        var offset = ((int)day - (int)firstOfMonth.DayOfWeek + 7) % 7;
        return firstOfMonth.AddDays(offset);
    }

    public static DateTime SecondWednesday(int year, int month) =>
        FirstWeekdayOfMonth(year, month, DayOfWeek.Wednesday).AddDays(7);

    public static DateTime FirstWednesday(int year, int month) =>
        FirstWeekdayOfMonth(year, month, DayOfWeek.Wednesday);

    public static DateTime BoxingDay(int year) => new(year, 12, 26);

    /// <summary>Which Wednesday September's race lands on for a given year (US31 AC1).</summary>
    public enum SeptemberOption
    {
        FirstWednesday,
        SecondWednesday
    }

    /// <summary>The eight Crown to Crown fixtures for a year, in calendar order (US31).</summary>
    public static IReadOnlyList<SeasonFixture> BuildFixtures(int year, SeptemberOption septemberOption)
    {
        var fixtures = new List<SeasonFixture>
        {
            new($"Crown to Crown – Good Friday {year}", GoodFriday(year), new TimeSpan(11, 0, 0)),
            new($"Crown to Crown – May {year}", SecondWednesday(year, 5), new TimeSpan(19, 30, 0)),
            new($"Crown to Crown – June {year}", SecondWednesday(year, 6), new TimeSpan(19, 30, 0)),
            new($"Crown to Crown – July {year}", SecondWednesday(year, 7), new TimeSpan(19, 30, 0)),
            new($"Crown to Crown – August {year}", SecondWednesday(year, 8), new TimeSpan(19, 30, 0)),
            new($"Crown to Crown – September {year}",
                septemberOption == SeptemberOption.FirstWednesday ? FirstWednesday(year, 9) : SecondWednesday(year, 9),
                new TimeSpan(19, 0, 0)),
            new($"Crown to Crown – Boxing Day {year}", BoxingDay(year), new TimeSpan(11, 0, 0))
        };
        return fixtures;
    }
}

/// <summary>One generated fixture row (US31).</summary>
public record SeasonFixture(string EventName, DateTime EventDate, TimeSpan StartTime);
