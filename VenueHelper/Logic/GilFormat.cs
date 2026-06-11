using System.Globalization;

namespace VenueHelper.Logic;

// Parsing/formatting helpers for gil amounts with k/M/B shorthand, so hosts can
// type "3.4M" instead of "3400000".
public static class GilFormat
{
    // Parses a gil string. Accepts plain numbers ("3400000"), shorthand
    // ("3.4M", "500k", "2b"), commas, a leading minus, and surrounding spaces.
    // Returns false if it can't be parsed.
    public static bool TryParse(string input, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var s = input.Trim().Replace(",", "").Replace(" ", "");
        var negative = s.StartsWith("-");
        if (negative) s = s[1..];
        if (s.Length == 0) return false;

        double mult = 1;
        var last = char.ToLowerInvariant(s[^1]);
        if (last == 'k') { mult = 1_000; s = s[..^1]; }
        else if (last == 'm') { mult = 1_000_000; s = s[..^1]; }
        else if (last == 'b') { mult = 1_000_000_000; s = s[..^1]; }

        if (s.Length == 0) return false;
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            return false;

        var result = num * mult;
        if (negative) result = -result;
        value = (long)Math.Round(result);
        return true;
    }

    // Compact display: 3,400,000 -> "3.4M", 500000 -> "500k", small stays plain.
    public static string Short(long gil)
    {
        var sign = gil < 0 ? "-" : "";
        var a = Math.Abs(gil);
        if (a >= 1_000_000_000) return $"{sign}{a / 1_000_000_000.0:0.##}B";
        if (a >= 1_000_000) return $"{sign}{a / 1_000_000.0:0.##}M";
        if (a >= 1_000) return $"{sign}{a / 1_000.0:0.##}k";
        return $"{sign}{a}";
    }
}
