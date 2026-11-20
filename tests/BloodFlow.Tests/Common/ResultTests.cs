using BloodFlow.Application.Common;

namespace BloodFlow.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Value_Converts_To_Success()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Error_Converts_To_Failure()
    {
        Result<int> result = Error.NotFound("X.NotFound", "Not found");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public void Reading_Value_Of_Failure_Throws()
    {
        Result<int> result = Error.Conflict("X.Conflict", "Conflict");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
