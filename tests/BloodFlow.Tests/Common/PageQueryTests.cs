using BloodFlow.Application.Staff;

namespace BloodFlow.Tests.Common;

public class PageQueryTests
{
    [Theory]
    [InlineData(1, 20, 0, 20)]
    [InlineData(3, 10, 20, 10)]
    [InlineData(0, 20, 0, 20)]      // page below 1 → page 1
    [InlineData(-5, 0, 0, 20)]      // invalid size → default 20
    [InlineData(2, 1000, 100, 100)] // size capped at 100
    public void Page_And_Size_Are_Made_Safe(int page, int pageSize, int expectedSkip, int expectedSize)
    {
        var query = new StaffQuery { Page = page, PageSize = pageSize };

        Assert.Equal(expectedSkip, query.Skip);
        Assert.Equal(expectedSize, query.SafePageSize);
    }
}
