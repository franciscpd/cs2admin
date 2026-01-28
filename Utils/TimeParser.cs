using System.Text.RegularExpressions;

namespace CS2Admin.Utils;

public static partial class TimeParser
{
    [GeneratedRegex(@"^(\d+)([smhdwMy]?)$", RegexOptions.IgnoreCase)]
    private static partial Regex DurationRegex();

    /// <summary>
    /// Parses a duration string into a TimeSpan.
    /// Supports: s (seconds), m (minutes), h (hours), d (days), w (weeks), M (months), y (years)
    /// Returns null for permanent duration (0 or "0")
    /// </summary>
    public static TimeSpan? Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return TimeSpan.Zero;

        input = input.Trim();

        if (input == "0")
            return null; // Permanent

        var match = DurationRegex().Match(input);
        if (!match.Success)
            return TimeSpan.Zero;

        var value = int.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value.ToLowerInvariant();

        if (value == 0)
            return null; // Permanent

        return unit switch
        {
            "s" => TimeSpan.FromSeconds(value),
            "m" or "" => TimeSpan.FromMinutes(value), // Default to minutes
            "h" => TimeSpan.FromHours(value),
            "d" => TimeSpan.FromDays(value),
            "w" => TimeSpan.FromDays(value * 7),
            "M" => TimeSpan.FromDays(value * 30),
            "y" => TimeSpan.FromDays(value * 365),
            _ => TimeSpan.FromMinutes(value)
        };
    }

    /// <summary>
    /// Formats a TimeSpan into a human-readable string
    /// </summary>
    public static string Format(TimeSpan? duration)
    {
        if (duration == null)
            return "permanent";

        var ts = duration.Value;

        if (ts.TotalDays >= 365)
            return $"{(int)(ts.TotalDays / 365)} year(s)";
        if (ts.TotalDays >= 30)
            return $"{(int)(ts.TotalDays / 30)} month(s)";
        if (ts.TotalDays >= 7)
            return $"{(int)(ts.TotalDays / 7)} week(s)";
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays} day(s)";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours} hour(s)";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes} minute(s)";
        return $"{(int)ts.TotalSeconds} second(s)";
    }
}
