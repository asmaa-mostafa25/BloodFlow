using System.Globalization;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Common
{
    /// <summary>Reads typed values from SystemSettings, with a safe fallback.</summary>
    internal sealed class SettingsReader
    {
        private readonly AppDbContext _db;

        public SettingsReader(AppDbContext db) => _db = db;

        public async Task<int> GetIntAsync(string key, int fallback, CancellationToken cancellationToken = default)
        {
            var value = await _db.SystemSettings.AsNoTracking()
                .Where(s => s.SettingKey == key)
                .Select(s => s.SettingValue)
                .FirstOrDefaultAsync(cancellationToken);

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0
                ? number
                : fallback;
        }
    }
}
