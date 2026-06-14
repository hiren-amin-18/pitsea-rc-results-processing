namespace RaceResults.Web.Services;

/// <summary>
/// Decides where the SQLite database file lives (US25). An explicit
/// <c>ConnectionStrings:DefaultConnection</c> always wins (so <c>dotnet run</c>
/// from the repo behaves as before); otherwise the default is a per-user
/// directory outside cloud-synced folders.
/// </summary>
public static class DatabasePathResolver
{
    private const string AppFolderName = "PitseaRaceResults";
    private const string DbFileName = "raceresults.db";

    /// <summary>Build the connection string to use; creates the per-user directory if needed.</summary>
    public static string Resolve(string? configuredConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        var dataDir = GetDefaultDataDirectory();
        Directory.CreateDirectory(dataDir);
        return $"Data Source={Path.Combine(dataDir, DbFileName)}";
    }

    /// <summary>The per-user data directory for installed builds (US25 AC3).</summary>
    public static string GetDefaultDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            return Path.Combine(localAppData, AppFolderName);
        }

        // Fallback for environments where LocalApplicationData is empty (e.g. some Linux setups).
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", AppFolderName);
    }
}
