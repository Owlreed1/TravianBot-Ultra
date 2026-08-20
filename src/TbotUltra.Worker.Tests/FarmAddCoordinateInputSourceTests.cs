using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class FarmAddCoordinateInputSourceTests
{
    [Fact]
    public void ReusedAddTargetForm_ReplacesAndVerifiesBothCoordinatesBeforeValidation()
    {
        var source = File.ReadAllText(Path.Combine(
            ProjectRootLocator.FindProjectRoot(),
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Farming",
            "TravianClient.FarmAdd.cs"));

        var fillCall = source.IndexOf("FillAndVerifyFarmCoordinatesAsync", StringComparison.Ordinal);
        var validation = fillCall >= 0
            ? source.IndexOf("var validationTriggered", fillCall, StringComparison.Ordinal)
            : -1;

        Assert.True(fillCall >= 0 && validation > fillCall,
            "Coordinates must be replaced and verified before Travian target validation starts.");
        Assert.Contains("await TypeHumanlyAsync(xInput, expectedX", source, StringComparison.Ordinal);
        Assert.Contains("await TypeHumanlyAsync(yInput, expectedY", source, StringComparison.Ordinal);
        Assert.Contains("var actualX = await xInput.InputValueAsync", source, StringComparison.Ordinal);
        Assert.Contains("var actualY = await yInput.InputValueAsync", source, StringComparison.Ordinal);
        Assert.Contains("string.Equals(actualX, expectedX", source, StringComparison.Ordinal);
        Assert.Contains("string.Equals(actualY, expectedY", source, StringComparison.Ordinal);
        Assert.Contains("coordinate input mismatch", source, StringComparison.Ordinal);
    }
}
