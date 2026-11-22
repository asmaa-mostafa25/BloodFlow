namespace BloodFlow.Infrastructure.Common
{
    /// <summary>RowVersion travels to the client as Base64 text and comes back on update.</summary>
    internal static class RowVersionConverter
    {
        public static string ToBase64(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

        public static bool TryParse(string? value, out byte[] rowVersion)
        {
            rowVersion = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                rowVersion = Convert.FromBase64String(value);
                return rowVersion.Length == 8; // SQL Server rowversion is always 8 bytes
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
