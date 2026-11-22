using BloodFlow.Application.Settings;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Tests.Settings;

public class SettingValueValidatorTests
{
    [Theory]
    [InlineData("56", "56")]
    [InlineData(" 18 ", "18")]
    [InlineData("007", "7")]
    public void Int_ValidValue_IsNormalized(string input, string expected)
    {
        var ok = SettingValueValidator.TryNormalize(SettingValueType.Int, input, out var normalized, out var error);

        Assert.True(ok);
        Assert.Equal(expected, normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Int_InvalidValue_IsRejected(string? input)
    {
        var ok = SettingValueValidator.TryNormalize(SettingValueType.Int, input, out _, out var error);

        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("true", "true")]
    [InlineData("TRUE", "true")]
    [InlineData(" False ", "false")]
    public void Bool_ValidValue_IsNormalized(string input, string expected)
    {
        Assert.True(SettingValueValidator.TryNormalize(SettingValueType.Bool, input, out var normalized, out _));
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Bool_InvalidValue_IsRejected()
    {
        Assert.False(SettingValueValidator.TryNormalize(SettingValueType.Bool, "yes", out _, out _));
    }

    [Theory]
    [InlineData("2.5", "2.5")]
    [InlineData("10", "10")]
    public void Decimal_UsesInvariantCulture(string input, string expected)
    {
        Assert.True(SettingValueValidator.TryNormalize(SettingValueType.Decimal, input, out var normalized, out _));
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void String_IsTrimmed()
    {
        Assert.True(SettingValueValidator.TryNormalize(SettingValueType.String, "  hello  ", out var normalized, out _));
        Assert.Equal("hello", normalized);
    }
}
