using BloodFlow.Application.Common;
using Microsoft.Extensions.Configuration;

namespace BloodFlow.Infrastructure.Common
{
    /// <summary>
    /// Real clock. "Today" is calculated in the business time zone (default Africa/Cairo),
    /// because a blood bag expires at midnight in Egypt, not at midnight UTC or on the
    /// hosting server's clock (which may be in another country).
    /// </summary>
    internal sealed class SystemClock : IClock
    {
        private readonly TimeZoneInfo _timeZone;

        public SystemClock(IConfiguration configuration)
        {
            var id = configuration["App:TimeZone"] ?? "Africa/Cairo";
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
        }

        public DateTime UtcNow => DateTime.UtcNow;

        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone));
    }
}
