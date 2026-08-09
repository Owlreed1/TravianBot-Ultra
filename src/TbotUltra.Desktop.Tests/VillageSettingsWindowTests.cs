using Xunit;
using System.Windows;
using System.Windows.Controls;
using TbotUltra.Desktop.Models;

namespace TbotUltra.Desktop.Tests;

// Runs on the shared WPF smoke thread: once any test creates Application.Current, constructing a
// Window on a second STA thread deadlocks against that Application's dispatcher.
[Collection(WpfSmokeCollection.Name)]
public sealed class VillageSettingsWindowTests
{
    private readonly WpfSmokeFixture _wpf;

    public VillageSettingsWindowTests(WpfSmokeFixture wpf)
    {
        _wpf = wpf;
    }

    [Fact]
    public void Constructor_LoadsCompiledXamlWithNoVillages()
    {
        _wpf.Run(() =>
        {
            var window = new VillageSettingsWindow([]);
            window.Close();
        });
    }

    [Fact]
    public void CheckAllHeaderButtons_ToggleStaticAndEligibleGroupColumns()
    {
        _wpf.Run(() =>
        {
            var rows = new[]
            {
                BuildRow("First", isAutomationEnabled: true, isNpcTradeEnabled: false, isFarmingEnabled: false, canToggleFarming: true),
                BuildRow("Second", isAutomationEnabled: true, isNpcTradeEnabled: true, isFarmingEnabled: true, canToggleFarming: false),
            };
            var window = new VillageSettingsWindow(rows);
            try
            {
                var grid = Assert.IsType<DataGrid>(window.FindName("VillageSettingsDataGrid"));

                ClickCheckAll(grid.Columns[0]);
                Assert.All(rows, row => Assert.False(row.IsEnabledForAutomation));

                ClickCheckAll(grid.Columns[0]);
                Assert.All(rows, row => Assert.True(row.IsEnabledForAutomation));

                var farmingColumn = grid.Columns
                    .OfType<DataGridTemplateColumn>()
                    .Single(column => HeaderTitle(column) == "Farming");
                ClickCheckAll(farmingColumn);
                Assert.True(rows[0].GroupToggles[0].IsEnabled);
                Assert.True(rows[1].GroupToggles[0].IsEnabled);

                ClickCheckAll(farmingColumn);
                Assert.False(rows[0].GroupToggles[0].IsEnabled);
                Assert.True(rows[1].GroupToggles[0].IsEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static VillageSettingsRow BuildRow(
        string name,
        bool isAutomationEnabled,
        bool isNpcTradeEnabled,
        bool isFarmingEnabled,
        bool canToggleFarming) => new()
    {
        Name = name,
        PopText = "100",
        IsEnabledForAutomation = isAutomationEnabled,
        NpcTrade = isNpcTradeEnabled,
        GroupToggles =
        [
            new VillageGroupToggle
            {
                GroupKey = "farming",
                Title = "Farming",
                IsEnabled = isFarmingEnabled,
                CanToggle = canToggleFarming,
            },
        ],
    };

    private static void ClickCheckAll(DataGridColumn column)
    {
        var header = Assert.IsType<StackPanel>(column.Header);
        var button = Assert.Single(header.Children.OfType<Button>());
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static string HeaderTitle(DataGridColumn column)
    {
        var header = Assert.IsType<StackPanel>(column.Header);
        return header.Children
            .OfType<TextBlock>()
            .Concat(header.Children.OfType<StackPanel>().SelectMany(panel => panel.Children.OfType<TextBlock>()))
            .FirstOrDefault()?
            .Text
            ?? string.Empty;
    }
}
