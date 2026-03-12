using System.Globalization;

namespace LbxRender.Parsing;

/// <summary>
/// Parses numeric values from .lbx XML attributes that may have a "pt" suffix.
/// </summary>
internal static class PtValueParser
{
    public static float Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0f;

        var s = value.AsSpan().Trim();
        if (s.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
            s = s[..^2];

        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0f;
    }

    public static bool TryParse(string? value, out float result)
    {
        result = 0f;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var s = value.AsSpan().Trim();
        if (s.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
            s = s[..^2];

        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
