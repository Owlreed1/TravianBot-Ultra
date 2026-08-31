using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
    private readonly DispatcherTimer _heroResourceMaxSaveTimer;
    private bool _isLoadingHeroResourceSettings;
    private bool _isApplyingHeroResourceBulkChange;

    public ObservableCollection<HeroResourceOverviewRow> HeroResourceRows { get; } = [];

    public HeroPanel()
    {
        InitializeComponent();
        _heroResourceMaxSaveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(350),
        };
        _heroResourceMaxSaveTimer.Tick += (_, _) =>
        {
            _heroResourceMaxSaveTimer.Stop();
            PersistHeroResourceSettings();
        };
        HeroResourceMaxLimitTextBox.TextChanged += (_, _) =>
        {
            if (_isLoadingHeroResourceSettings)
            {
                return;
            }

            _heroResourceMaxSaveTimer.Stop();
            _heroResourceMaxSaveTimer.Start();
        };
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
        _isLoadingHeroResourceSettings = true;
        try
        {
            foreach (var existingRow in HeroResourceRows)
            {
                existingRow.PropertyChanged -= HeroResourceRow_PropertyChanged;
            }

            HeroResourceRows.Clear();
            foreach (var row in rows)
            {
                row.PropertyChanged += HeroResourceRow_PropertyChanged;
                HeroResourceRows.Add(row);
            }

            HeroResourceMaxLimitTextBox.Text = rows.Count > 0
                ? rows.Max(row => row.MaxUsePerResource).ToString()
                : "5000";
            HeroResourceSettingsStatusText.Text = string.Empty;
        }
        finally
        {
            _isLoadingHeroResourceSettings = false;
        }
    }

    private void HeroResourceRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HeroResourceOverviewRow.Settings))
        {
            return;
        }

        if (!_isLoadingHeroResourceSettings
            && !_isApplyingHeroResourceBulkChange
            && sender is HeroResourceOverviewRow row)
        {
            PersistHeroResourceSettings([row]);
        }
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
        _isApplyingHeroResourceBulkChange = true;
        try
        {
            foreach (var row in HeroResourceRows)
            {
                set(row, target);
            }
        }
        finally
        {
            _isApplyingHeroResourceBulkChange = false;
        }

        PersistHeroResourceSettings();
    }

    private void PersistHeroResourceSettings(IReadOnlyCollection<HeroResourceOverviewRow>? changedRows = null)
    {
        _heroResourceMaxSaveTimer.Stop();
        if (!int.TryParse(HeroResourceMaxLimitTextBox.Text, out var maxUsePerResource) || maxUsePerResource < 0)
        {
            HeroResourceSettingsStatusText.Foreground = (Brush)FindResource("DangerTextBrush");
            HeroResourceSettingsStatusText.Text = "Enter a valid max limit.";
            return;
        }

        var rowsToSave = changedRows ?? HeroResourceRows;
        var results = rowsToSave
            .Select(row => new HeroResourceOverviewResult(
                row.VillageKey,
                row.VillageName,
                row.Settings with { MaxUsePerResource = maxUsePerResource }))
            .ToList();
        if (Host?.SaveHeroResourceSettingsFromPanel(results) != true)
        {
            HeroResourceSettingsStatusText.Foreground = (Brush)FindResource("DangerTextBrush");
            HeroResourceSettingsStatusText.Text = "Could not save.";
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
        if (FindVisualParent<TextBox>(e.OriginalSource as DependencyObject) is not null)
        {
            _dragSource = null;
            return;
        }

        _dragSource = FindHeroAttributePriorityItem(e.OriginalSource as DependencyObject);
    }

    private void HeroAttributeMaximum_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            PersistHeroAttributeMaximum(textBox);
        }
    }

    private void HeroAttributeMaximum_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
        {
            return;
        }

        PersistHeroAttributeMaximum(textBox);
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void PersistHeroAttributeMaximum(TextBox textBox)
    {
        if (textBox.DataContext is not HeroAttributePriorityItem item)
        {
            return;
        }

        var maximum = int.TryParse(textBox.Text, out var parsed) && parsed is >= 0 and <= 100
            ? parsed
            : 100;
        var changed = item.MaxPoints != maximum;
        item.MaxPoints = maximum;
        textBox.Text = maximum.ToString();
        if (changed)
        {
            Host?.PersistHeroPriorityToConfig();
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
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
