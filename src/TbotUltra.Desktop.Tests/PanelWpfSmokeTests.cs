using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TbotUltra.Desktop.ViewModels;
using TbotUltra.Desktop.Views;
using Xunit;

namespace TbotUltra.Desktop.Tests;

[Collection(WpfSmokeCollection.Name)]
public sealed class PanelWpfSmokeTests
{
    private readonly WpfSmokeFixture _wpf;

    public PanelWpfSmokeTests(WpfSmokeFixture wpf)
    {
        _wpf = wpf;
    }

    [Fact]
    public void MigratedPanels_LoadWithTheirViewModelAndCommandBindings()
    {
        _wpf.Run(() =>
        {
            AssertPanelCommands(new FarmingPanel(), new FarmListsViewModel(),
                "AnalyzeFarmListsButton", "FarmListSendAllNowButton", "CreateFarmListButton", "AddFarmsToListButton");
            AssertPanelCommands(new HeroPanel(), new HeroViewModel(),
                "RefreshAdventuresButton", "RefreshHeroHpButton", "RefreshHeroStatsButton", "RefreshHeroInventoryButton");
            AssertPanelCommands(new QueuePanel(), new TravianQueueViewModel(),
                "QueueRemoveButton", "QueueRedoButton", "QueueMoveToTopButton", "QueueMoveUpButton",
                "QueueMoveDownButton", "QueueMoveToBottomButton", "QueueRefreshButton",
                "ClearVillageQueueButton", "QueueClearButton", "QueuePopoutButton");
            AssertPanelCommands(new ResourcesPanel(), new ResourcesViewModel(), "LoadResourcesButton");
            AssertPanelCommands(new TroopsPanel(), new TroopTrainingViewModel(), "CheckCelebrationButton", "RefreshTroopQueuesButton");

            var hero = new HeroViewModel();
            var heroPanel = new HeroPanel { DataContext = hero };
            Layout(heroPanel);
            var refreshHero = Assert.IsType<Button>(heroPanel.FindName("RefreshHeroStatsButton"));
            Assert.True(refreshHero.IsEnabled);
            hero.SetManualOperationRunning(true);
            heroPanel.UpdateLayout();
            Assert.False(refreshHero.IsEnabled);
            hero.SetManualOperationRunning(false);
            heroPanel.UpdateLayout();
            Assert.True(refreshHero.IsEnabled);

            var buildings = new BuildingsViewModel();
            var buildingsPanel = new BuildingsPanel { DataContext = buildings };
            Layout(buildingsPanel);
            Assert.Same(buildings, buildingsPanel.DataContext);
            Assert.Contains(FindButtons(buildingsPanel), button => ReferenceEquals(button.Command, buildings.LoadCommand));
            Assert.Contains(FindButtons(buildingsPanel), button => ReferenceEquals(button.Command, buildings.UpgradeAllToMaxCommand));
        });
    }

    private static void AssertPanelCommands(UserControl panel, object viewModel, params string[] buttonNames)
    {
        panel.DataContext = viewModel;
        Layout(panel);
        Assert.Same(viewModel, panel.DataContext);

        foreach (var name in buttonNames)
        {
            var button = Assert.IsType<Button>(panel.FindName(name));
            Assert.NotNull(button.Command);
            Assert.Equal(button.Command.CanExecute(button.CommandParameter), button.IsEnabled);
        }
    }

    private static void Layout(UserControl panel)
    {
        panel.Measure(new Size(1280, 900));
        panel.Arrange(new Rect(0, 0, 1280, 900));
        panel.UpdateLayout();
    }

    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Button button)
            {
                yield return button;
            }

            foreach (var nested in FindButtons(child))
            {
                yield return nested;
            }
        }
    }
}
