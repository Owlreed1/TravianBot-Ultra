using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class SmithyVillageSettingsSourceTests
{
    [Fact]
    public void BuildSmithyTroopOptions_ResolvesTribeFromTargetVillageKey()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.TroopTraining.cs"));
        var methodStart = source.IndexOf(
            "private List<SmithyTroopOption> BuildSmithyTroopOptions",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "    /// <summary>",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var methodBody = source[methodStart..methodEnd];
        Assert.Contains("ResolveVillageTribeByKey(villageKey", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveStoredTroopTrainingTribe()", methodBody, StringComparison.Ordinal);
    }
}
