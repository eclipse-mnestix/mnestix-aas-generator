using System.Globalization;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Formats scalar mapping results using culture-independent XML Schema lexical forms.
/// </summary>
internal static class ScalarValueFormatter
{
    public static string ToInvariantString(JToken token)
    {
        if (token is not JValue { Value: { } value })
        {
            return string.Empty;
        }

        return value switch
        {
            double number => FormatFloatingPoint(number),
            float number => FormatFloatingPoint(number),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatFloatingPoint(double value)
    {
        if (double.IsPositiveInfinity(value)) return "INF";
        if (double.IsNegativeInfinity(value)) return "-INF";
        if (double.IsNaN(value)) return "NaN";

        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string FormatFloatingPoint(float value)
    {
        if (float.IsPositiveInfinity(value)) return "INF";
        if (float.IsNegativeInfinity(value)) return "-INF";
        if (float.IsNaN(value)) return "NaN";

        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}
