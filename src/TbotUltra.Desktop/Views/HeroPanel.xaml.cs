using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.ViewModels;

namespace TbotUltra.Desktop.Views;

/// <summary>
/// Hero / Adventures panel. Owns the drag-and-drop scratch state for the
/// attribute priority list and routes button clicks back to the host
/// MainWindow's internal "Core" methods, which still hold the
/// service-bound logic (refresh stats / refresh adventures / queue
/// adventure). The panel reads its DataContext as a
/// <see cref="HeroViewModel"/> inherited from the host TabItem.
/// </summary>
public partial class HeroPanel : UserControl
{
    public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
        nameof(Section),
        typeof(string),
        typeof(HeroPanel),
        new PropertyMetadata("All"));

    public string Section
    {
        get => (string)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    private Point _dragStart;
    private HeroAttributePriorityItem? _dragSource;
    private MainWindow? _hostCache;

    public ObservableCollection<HeroResourceOverviewRow> HeroResourceRows { get; } = [];

    public HeroPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplySection();
    }

    private void ApplySection()
    {
        if (string.Equals(Section, "All", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SectionTitle.Text = Section;
        SectionDescription.Visibility = Visibility.Collapsed;
        if (string.Equals(Section, "Adventures", StringComparison.OrdinalIgnoreCase))
        {
            SectionDescription.Visibility = Visibility.Visible;
            SectionDescription.Text = "Configure adventure behaviour and queue adventures.";
            AttributeInventoryGrid.Visibility = Visibility.Collapsed;
            return;
        }

        SettingsCard.Visibility = Visibility.Collapsed;
        if (string.Equals(Section, "Attributes", StringComparison.OrdinalIgnoreCase))
        {
            HeroInventoryCard.Visibility = Visibility.Collapsed;
            InventoryColumn.Width = new GridLength(0);
            AttributeColumn.Width = new GridLength(1, GridUnitType.Star);
            AttributePriorityCard.Margin = new Thickness(0);
            return;
        }

        AttributePriorityCard.Visibility = Visibility.Collapsed;
        AttributeColumn.Width = new GridLength(0);
        InventoryColumn.Width = new GridLength(1, GridUnitType.Star);
        HeroInventoryCard.Margin = new Thickness(0);
        RefreshHeroResourceSettings();
    }

    internal void RefreshHeroResourceSettings()
    {
        if (string.Equals(Section, "Hero inventory", StringComparison.OrdinalIgnoreCase))
        {
            Host?.LoadHeroResourceSettingsIntoPanel(this);
        }
    }

    internal void LoadHeroResourceSettings(IReadOnlyList<HeroResourceOverviewRow> rows)
    {
        HeroResourceRows.Clear();
        foreach (var row in rows)
        {
            HeroResourceRows.Add(row);
        }

        HeroResourceMaxLimitTextBox.Text = rows.Count > 0
            ? rows.Max(row => row.MaxUsePerResource).ToString()
            : "5000";
        HeroResourceSettingsStatusText.Text = string.Empty;
    }

    private void ToggleAllHeroResources_Click(object sender, RoutedEventArgs e)
        => SetAllHeroResourceRows(row => row.IsHeroResourcesEnabled, (row, value) => row.IsHeroResourcesEnabled = value);

    private void ToggleAllHeroConstruction_Click(object sender, RoutedEventArgs e)
        => SetAllHeroResourceRows(row => row.UseConstruction, (row, value) => row.UseConstruction = value);

    private void ToggleAllHeroSmithy_Click(object sender, RoutedEventArgs e)
        => SetAllHeroResourceRows(row => row.UseSmithy, (row, value) => row.UseSmithy = value);

    private void ToggleAllHeroBrewery_Click(object sender, RoutedEventArgs e)
        => SetAllHeroResourceRows(row => row.UseBrewery, (row, value) => row.UseBrewery = value);

    private void ToggleAllHeroTownHall_Click(object sender, RoutedEventArgs e)
        => SetAllHeroResourceRows(row => row.UseTownHall, (row, value) => row.UseTownHall = value);

    private void SetAllHeroResourceRows(
        Func<HeroResourceOverviewRow, bool> get,
        Action<HeroResourceOverviewRow, bool> set)
    {
        if (HeroResourceRows.Count == 0)
        {
            return;
        }

        var target = !HeroResourceRows.All(get);
        foreach (var row in HeroResourceRows)
        {
            set(row, target);
        }
    }

    private void SaveHeroResourceSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(HeroResourceMaxLimitTextBox.Text, out var maxUsePerResource) || maxUsePerResource < 0)
        {
            HeroResourceSettingsStatusText.Foreground = (Brush)FindResource("DangerTextBrush");
            HeroResourceSettingsStatusText.Text = "Enter a valid max limit.";
            return;
        }

        var results = HeroResourceRows
            .Select(row => new HeroResourceOverviewResult(
                row.VillageKey,
                row.VillageName,
                row.Settings with { MaxUsePerResource = maxUsePerResource }))
            .ToList();
        if (Host?.SaveHeroResourceSettingsFromPanel(results) != true)
        {
            return;
        }

        HeroResourceSettingsStatusText.Foreground = (Brush)FindResource("SuccessTextBrush");
        HeroResourceSettingsStatusText.Text = "Saved.";
    }

    /// <summary>
    /// Resolves the parent <see cref="MainWindow"/>. Returns <c>null</c> while
    /// the panel is detached from the visual tree (e.g. early in the load
    /// cycle); production calls happen from event handlers, after the panel
    /// is mounted under MainWindow.
    /// </summary>
    private MainWindow? Host
    {
        get
        {
            if (_hostCache is not null)
            {
                return _hostCache;
            }

            _hostCache = Window.GetWindow(this) as MainWindow;
            return _hostCache;
        }
    }

    private HeroViewModel? Vm => DataContext as HeroViewModel;

    private void HeroAttributePriorityItemsControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(HeroAttributePriorityItemsControl);
        _dragSource = FindHeroAttributePriorityItem(e.OriginalSource as DependencyObject);
    }

    private void HeroAttributePriorityItemsControl_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSource is null)
        {
            return;
        }

        var position = e.GetPosition(HeroAttributePriorityItemsControl);
        var delta = position - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(HeroAttributePriorityItemsControl, _dragSource, DragDropEffects.Move);
    }

    private void HeroAttributePriorityItemsControl_Drop(object sender, DragEventArgs e)
    {
        if (Vm is not { } vm)
        {
            return;
        }

        if (!e.Data.GetDataPresent(typeof(HeroAttributePriorityItem)))
        {
            return;
        }

        if (e.Data.GetData(typeof(HeroAttributePriorityItem)) is not HeroAttributePriorityItem sourceItem)
        {
            return;
        }

        var targetItem = FindHeroAttributePriorityItem(e.OriginalSource as DependencyObject);
        var fromIndex = vm.AttributePriorityItems.IndexOf(sourceItem);
        if (fromIndex < 0)
        {
            return;
        }

        var toIndex = targetItem is null
            ? vm.AttributePriorityItems.Count - 1
            : vm.AttributePriorityItems.IndexOf(targetItem);
        if (toIndex < 0 || fromIndex == toIndex)
        {
            return;
        }

        vm.AttributePriorityItems.Move(fromIndex, toIndex);
        vm.UpdateOrders();
        Host?.PersistHeroPriorityToConfig();
    }

    private static HeroAttributePriorityItem? FindHeroAttributePriorityItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: HeroAttributePriorityItem item })
            {
                return item;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

}
