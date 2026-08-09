using TbotUltra.Desktop.ViewModels;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class PostLoginSettingsViewModelTests
{
    [Fact]
    public void Defaults_PreserveNewAccountAnalysisSelections()
    {
        var vm = new PostLoginSettingsViewModel();

        Assert.True(vm.AnalyzeNewVillages);
        Assert.True(vm.AnalyzeNewAccount);
        Assert.False(vm.AnalyzeHero);
    }
}
