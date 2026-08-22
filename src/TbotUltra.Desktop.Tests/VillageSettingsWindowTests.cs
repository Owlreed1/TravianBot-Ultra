using Xunit;
using System.Windows;
using System.Windows.Controls;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Views;

namespace TbotUltra.Desktop.Tests;

// Runs on the shared WPF smoke thread: once any test creates Application.Current, constructing a
// Panel tests run on the shared WPF smoke thread so compiled XAML and templates use one dispatcher.
[Collection(WpfSmokeCollection.Name)]
public sealed class VillageSettingsPanelTests
{
    private readonly WpfSmokeFixture _wpf;

    public VillageSettingsPanelTests(WpfSmokeFixture wpf)
    {
        _wpf = wpf;
    }

    [Fact]
    public void Constructor_LoadsCompiledXamlWithNoVillages()
    {
        _wpf.Run(() =>
        {
            var panel = new VillageSettingsPanel([]);
            Assert.NotNull(panel);
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
            var panel = new VillageSettingsPanel(rows);
            var grid = Assert.IsType<DataGrid>(panel.FindName("VillageSettingsDataGrid"));
                var checkAllRow = Assert.IsType<VillageSettingsRow>(grid.Items[0]);
                Assert.True(checkAllRow.IsCheckAllRow);

                ClickCheckAll(grid.Columns[0], checkAllRow);
                Assert.All(rows, row => Assert.False(row.IsEnabledForAutomation));

                ClickCheckAll(grid.Columns[0], checkAllRow);
                Assert.All(rows, row => Assert.True(row.IsEnabledForAutomation));

                var npcColumn = grid.Columns
                    .OfType<DataGridTemplateColumn>()
                    .Single(column => HeaderTitle(column) == "NPC");
                ClickCheckAll(npcColumn, checkAllRow);
                Assert.All(rows, row => Assert.True(row.NpcTrade));

                ClickCheckAll(npcColumn, checkAllRow);
                Assert.All(rows, row => Assert.False(row.NpcTrade));
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

    private static void ClickCheckAll(DataGridColumn column, VillageSettingsRow checkAllRow)
    {
        var templateColumn = Assert.IsType<DataGridTemplateColumn>(column);
        var cell = Assert.IsType<Grid>(templateColumn.CellTemplate.LoadContent());
        cell.DataContext = checkAllRow;
        var button = Assert.Single(cell.Children.OfType<Button>());
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    [Fact]
    public void Constructor_DoesNotBuildOverviewBeforeThePanelIsLoaded()
    {
        _wpf.Run(() =>
        {
            var calls = 0;
            var panel = new VillageSettingsPanel(
                [],
                section: "Overview",
                overviewProjectionProvider: _ =>
                {
                    calls++;
                    return Task.FromResult<VillageOverviewProjection>(null!);
                },
                overviewSourceVersionProvider: () => 1);

            Assert.Equal(0, calls);
            Assert.Equal("Loading overview...", Assert.IsType<TextBlock>(panel.FindName("OverviewUpdatedTextBlock")).Text);
        });
    }

    private static string HeaderTitle(DataGridColumn column)
    {
        return (column.Header as TextBlock)?.Text ?? string.Empty;
    }
}
