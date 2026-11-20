namespace BloodFlow.Application.Common
{
    public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
    {
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    /// <summary>Base for list queries. Out-of-range values are corrected, never trusted.</summary>
    public abstract record PageQuery
    {
        public const int MaxPageSize = 100;

        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 20;

        public int SafePage => Page < 1 ? 1 : Page;
        public int SafePageSize => PageSize < 1 ? 20 : Math.Min(PageSize, MaxPageSize);
        public int Skip => (SafePage - 1) * SafePageSize;
    }
}
