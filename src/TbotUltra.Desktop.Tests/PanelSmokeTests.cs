using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Views;
using Xunit;

namespace TbotUltra.Desktop.Tests;

/// <summary>
/// Constructs every tab panel with the App theme dictionaries loaded. These catch the class of bug a
/// unit test cannot: a StaticResource key that does not exist, a style key renamed on one side only,
/// or a XAML parse error. All of those build cleanly and only blow up when the panel is first shown.
/// </summary>
[Collection(WpfSmokeCollection.Name)]
public sealed class PanelSmokeTests
{
    private readonly WpfSmokeFixture _wpf;

    public PanelSmokeTests(WpfSmokeFixture wpf)
    {
        _wpf = wpf;
    }

    public static TheoryData<string, Func<UserControl>> Panels() => new()
    {
        { nameof(DashboardPanel), () => new DashboardPanel() },
        { nameof(DashboardHubPanel), () => new DashboardHubPanel() },
        { nameof(BuildingsPanel), () => new BuildingsPanel() },
        { nameof(ResourcesPanel), () => new ResourcesPanel() },
        { nameof(ResourcesHubPanel), () => new ResourcesHubPanel() },
        { nameof(HeroPanel), () => new HeroPanel() },
        { nameof(HeroHubPanel), () => new HeroHubPanel() },
        { nameof(TroopsPanel), () => new TroopsPanel() },
        { nameof(TroopsHubPanel), () => new TroopsHubPanel() },
        { nameof(FarmingPanel), () => new FarmingPanel() },
        { nameof(QueuePanel), () => new QueuePanel() },
        { nameof(LogsPanel), () => new LogsPanel() },
        { nameof(InboxPanel), () => new InboxPanel() },
        { nameof(NpcTradePanel), () => new NpcTradePanel() },
        { nameof(NpcTradeHubPanel), () => new NpcTradeHubPanel() },
        { nameof(ReinforcementsPanel), () => new ReinforcementsPanel() },
        { nameof(BusyOverlayControl), () => new BusyOverlayControl() },
        { nameof(StoragePreflightPlanView), () => new StoragePreflightPlanView("Preflight", []) },
        { nameof(UpdateConfirmationView), () => new UpdateConfirmationView("1.2.3") },
        { nameof(LobbyWorldSelectionView), () => new LobbyWorldSelectionView(new(
            "Configured world",
            "https://wrong.x1.europe.travian.com",
            [new("12345678-1234-1234-1234-123456789012", "Europe 3", "player · Europe 3 · PLAY NOW")])) },
    };

    [Theory]
    [MemberData(nameof(Panels))]
    public void Panel_LoadsWithoutXamlOrResourceErrors(string name, Func<UserControl> create)
    {
        _wpf.Run(() =>
        {
            var panel = create();
            Assert.NotNull(panel);

            // Measure/arrange so templates expand and lazily-applied styles actually run, instead of
            // only proving the constructor parsed the XAML.
            panel.Measure(new Size(1280, 900));
            panel.Arrange(new Rect(0, 0, 1280, 900));
            panel.UpdateLayout();
        });

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Fact]
    public void FarmingPanel_ContainsLossMoveControls()
    {
        _wpf.Run(() =>
        {
            var panel = new FarmingPanel();

            Assert.Equal("Move red/yellow farms to list", panel.MoveLossesOption.Content);
            Assert.Equal("DisplayText", panel.LossDestinationOption.DisplayMemberPath);
        });
    }

    [Fact]
    public void DashboardHubPanel_ContainsRequestedTabsInOrder()
    {
        _wpf.Run(() =>
        {
            var panel = new DashboardHubPanel();
            var tabs = Assert.IsType<TabControl>(panel.FindName("DashboardTabControl"));

            Assert.Equal(
                new[] { "Dashboard", "Village settings", "Village overview" },
                tabs.Items.Cast<TabItem>().Select(item => item.Header?.ToString() ?? string.Empty).ToArray());
        });
    }

    [Fact]
    public void FarmingPanel_ContainsFarmingAndInactiveOasisScanTabsInOrder()
    {
        _wpf.Run(() =>
        {
            var panel = new FarmingPanel();
            var tabs = Assert.IsType<TabControl>(panel.FindName("FarmingTabControl"));
            var inactiveButton = Assert.IsType<Button>(panel.FindName("TravcoInactiveSearchButton"));

            Assert.Equal(
                new[] { "Farming", "Inactive / oasis scan" },
                tabs.Items.Cast<TabItem>().Select(item => item.Header?.ToString() ?? string.Empty).ToArray());
            Assert.Equal("Inactive / oasis analysis", inactiveButton.Content);
        });
    }

    [Fact]
    public void ReinforcementsPanel_ContainsReinforcementsAndIncomingAttackTabs()
    {
        _wpf.Run(() =>
        {
            var panel = new ReinforcementsPanel();
            var tabs = Assert.IsType<TabControl>(panel.FindName("SendTroopsTabControl"));
            var attacks = Assert.IsType<TabItem>(panel.FindName("IncomingAttacksTabItem"));
            var grid = Assert.IsType<DataGrid>(panel.FindName("IncomingAttackDataGrid"));
            var infoIcon = Assert.IsType<TextBlock>(panel.FindName("IncomingAttacksInfoIcon"));

            Assert.Equal(3, tabs.Items.Count);
            Assert.Equal("Incoming attacks", attacks.Header);
            Assert.Equal(7, grid.Columns.Count);
            Assert.Equal(
                "Incoming attacks and raids detected on Dorf1 and verified in Rally Point. Times use the Travian server clock.",
                infoIcon.ToolTip);
        });
    }

    [Fact]
    public void TroopsHubPanel_ContainsRequestedTabsInOrder()
    {
        _wpf.Run(() =>
        {
            var panel = new TroopsHubPanel();
            var tabs = Assert.IsType<TabControl>(panel.FindName("TroopsTabControl"));

            Assert.Equal(
                new[] { "Build Troops", "Upgrade troops", "Reinforcements", "Incoming attacks", "Evasion", "Catapult waves", "Brewery celebration" },
                tabs.Items.Cast<TabItem>().Select(item => item.Header?.ToString() ?? string.Empty).ToArray());
        });
    }

    [Fact]
    public void TroopEvasionPanel_OffersLockedTimingChoicesAndExpandableVillageList()
    {
        _wpf.Run(() =>
        {
            var panel = new TroopEvasionPanel();
            var lead = Assert.IsType<ComboBox>(panel.FindName("LeadTimeComboBox"));
            var protection = Assert.IsType<ComboBox>(panel.FindName("ProtectionWindowComboBox"));
            var villages = Assert.IsType<ItemsControl>(panel.FindName("VillageItemsControl"));

            Assert.Equal(new[] { 1, 2, 5, 10 }, lead.Items.Cast<int>().ToArray());
            Assert.Equal(new[] { 1, 2, 5, 10 }, protection.Items.Cast<int>().ToArray());
            Assert.Same(panel.Villages, villages.ItemsSource);
        });
    }

    [Fact]
    public void ResourcesHubPanel_ContainsResourcesAndTradingTabsInOrder()
    {
        _wpf.Run(() =>
        {
            var panel = new ResourcesHubPanel();
            var tabs = Assert.IsType<TabControl>(panel.FindName("ResourcesTabControl"));

            Assert.Equal(
                new[] { "Resources", "Trading" },
                tabs.Items.Cast<TabItem>().Select(item => item.Header?.ToString() ?? string.Empty).ToArray());
        });
    }

    [Fact]
    public void NpcTradeHubPanel_ContainsNpcAndSilverTradingTabsInOrder()
    {
        _wpf.Run(() =>
        {
            var panel = new NpcTradeHubPanel();
            var tabs = Assert.IsType<TabControl>(panel.FindName("NpcTradeTabControl"));

            Assert.Equal(
                new[] { "NPC", "Silver trading" },
                tabs.Items.Cast<TabItem>().Select(item => item.Header?.ToString() ?? string.Empty).ToArray());
        });
    }

    [Fact]
    public void HeroHubPanel_ContainsRequestedTabsInOrder()
    {
        _wpf.Run(() =>
        {
            var panel = new HeroHubPanel();
            var tabs = Assert.IsType<TabControl>(panel.FindName("HeroTabControl"));

            Assert.Equal(
                new[] { "Adventures", "Attributes", "Hero inventory" },
                tabs.Items.Cast<TabItem>().Select(item => item.Header?.ToString() ?? string.Empty).ToArray());
        });
    }

    [Fact]
    public void FarmingPanel_NextSendDisplay_BindsToHeadersCreatedAfterTheTimerUpdate()
    {
        _wpf.Run(() =>
        {
            var panel = new FarmingPanel();

            panel.SetNextSendDisplay("Next send: 01:00");
            var lists = new ObservableCollection<FarmListStatusRow>
            {
                new() { Name = "Test", TotalFarmCount = 1, VillageOrdinal = 0, VillageHeaderText = "Village" },
            };
            panel.FarmLists.ItemsSource = lists;
            var view = CollectionViewSource.GetDefaultView(lists);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FarmListStatusRow.VillageOrdinal)));
            panel.Measure(new Size(1280, 900));
            panel.Arrange(new Rect(0, 0, 1280, 900));
            panel.UpdateLayout();

            var nextSend = FindVisualChildren<TextBlock>(panel)
                .Single(element => Equals(element.Tag, "FarmListNextSendText"));
            Assert.Equal("Next send: 01:00", nextSend.Text);
        });
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
