using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class FarmListOnlyLossesSourceTests
{
    [Fact]
    public void CreateFlow_SetsAndVerifiesOfficialOnlyLossesCheckboxWithoutBlockingSave()
    {
        var source = ReadSource("TravianClient.FarmListCreation.cs");
        var helperStart = source.IndexOf(
            "private async Task TryApplyOnlyLossesSettingAsync(",
            StringComparison.Ordinal);
        var nextMethod = source.IndexOf(
            "private async Task<string> ResolveOfficialCreateFarmListVillageValueAsync(",
            helperStart,
            StringComparison.Ordinal);
        var helper = source[helperStart..nextMethod];

        Assert.Contains("input[type='checkbox'][name='onlyLosses']", helper, StringComparison.Ordinal);
        Assert.Contains("label.checkbox:has(input[name='onlyLosses'])", helper, StringComparison.Ordinal);
        Assert.True(
            helper.Split("IsCheckedAsync", StringSplitOptions.None).Length >= 3,
            "The Official checkbox must be read before the click and verified after it.");
        Assert.Contains("continuing creation", helper, StringComparison.Ordinal);
        Assert.Contains("catch (Exception ex)", helper, StringComparison.Ordinal);

        var apply = source.IndexOf("await TryApplyOnlyLossesSettingAsync(", StringComparison.Ordinal);
        var submit = source.IndexOf(
            "await DelayBeforeClickAsync(cancellationToken, \"create farm list\")",
            apply,
            StringComparison.Ordinal);
        Assert.True(apply >= 0 && submit > apply, "onlyLosses must be applied before the Create click.");
    }

    private static string ReadSource(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "TbotUltra.Worker",
                "Services",
                "Automation",
                "Farming",
                fileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
