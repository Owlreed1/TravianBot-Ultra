using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class SettingsNumericInputValidationTests
{
    [Fact]
    public void DecimalInput_RejectsCommaAndExplainsRequiredSeparator()
    {
        var valid = SettingsWindow.TryValidateNumericInputText(
            "5,6",
            wholeNumber: false,
            min: 0,
            max: 3600,
            out var error);

        Assert.False(valid);
        Assert.Contains("period", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("5.6", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("5.6", false, 0, 3600, true)]
    [InlineData("5.6", true, 0, 3600, false)]
    [InlineData("", false, 0, 3600, false)]
    [InlineData("3601", false, 0, 3600, false)]
    public void NumericInput_ValidatesFormatAndRange(
        string text,
        bool wholeNumber,
        double min,
        double max,
        bool expected)
    {
        var valid = SettingsWindow.TryValidateNumericInputText(
            text,
            wholeNumber,
            min,
            max,
            out _);

        Assert.Equal(expected, valid);
    }

    [Fact]
    public void WholeNumberInput_WithComma_RecommendsAValidWholeNumber()
    {
        var valid = SettingsWindow.TryValidateNumericInputText(
            "5,6",
            wholeNumber: true,
            min: 1,
            max: 10080,
            out var error);

        Assert.False(valid);
        Assert.Contains("whole number", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "5",
            SettingsWindow.GetNumericInputCorrectionExample("5,6", wholeNumber: true, min: 1, max: 10080));
    }

    [Theory]
    [InlineData("5,6", false, 0, 3600, "5.6")]
    [InlineData("5000", false, 0, 3600, "3600")]
    [InlineData("", false, 2, 5, "2")]
    public void CorrectionExample_IsValidForTheField(
        string text,
        bool wholeNumber,
        double min,
        double max,
        string expected)
    {
        Assert.Equal(
            expected,
            SettingsWindow.GetNumericInputCorrectionExample(text, wholeNumber, min, max));
    }
}
