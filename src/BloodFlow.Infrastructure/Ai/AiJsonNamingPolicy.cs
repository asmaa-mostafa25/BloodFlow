using System.Text.Json;
using System.Text.RegularExpressions;

namespace BloodFlow.Infrastructure.Ai
{
    /// <summary>
    /// snake_case like the AI service, with a "_" before numbers too:
    /// ExpiringIn3Days -> expiring_in_3_days, PredictedConsumption14d -> predicted_consumption_14d.
    /// (The built-in SnakeCaseLower gives expiring_in3_days.)
    /// </summary>
    public sealed partial class AiJsonNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
            => LetterThenDigit().Replace(SnakeCaseLower.ConvertName(name), "$1_$2");

        [GeneratedRegex("([a-z])([0-9])")]
        private static partial Regex LetterThenDigit();
    }
}
