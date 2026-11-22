using System.Globalization;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Application.Settings
{
    /// <summary>
    /// Checks that a new setting value matches its type, and returns it in one
    /// standard text form (so "true", "True" and " TRUE " are all stored as "true").
    /// Pure logic with no database: easy to unit test.
    /// </summary>
    public static class SettingValueValidator
    {
        public static bool TryNormalize(SettingValueType type, string? rawValue, out string normalized, out string? error)
        {
            normalized = string.Empty;
            error = null;
            var value = rawValue?.Trim() ?? string.Empty;

            if (value.Length == 0)
            {
                error = "Value is required.";
                return false;
            }

            switch (type)
            {
                case SettingValueType.Int:
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    {
                        error = "Value must be a whole number.";
                        return false;
                    }
                    if (i <= 0)
                    {
                        error = "Value must be greater than zero.";
                        return false;
                    }
                    normalized = i.ToString(CultureInfo.InvariantCulture);
                    return true;

                case SettingValueType.Decimal:
                    if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                    {
                        error = "Value must be a number (use '.' as the decimal separator).";
                        return false;
                    }
                    if (d <= 0)
                    {
                        error = "Value must be greater than zero.";
                        return false;
                    }
                    normalized = d.ToString(CultureInfo.InvariantCulture);
                    return true;

                case SettingValueType.Bool:
                    if (!bool.TryParse(value, out var b))
                    {
                        error = "Value must be 'true' or 'false'.";
                        return false;
                    }
                    normalized = b ? "true" : "false";
                    return true;

                case SettingValueType.String:
                    normalized = value;
                    return true;

                default:
                    error = "Unknown setting type.";
                    return false;
            }
        }
    }
}
