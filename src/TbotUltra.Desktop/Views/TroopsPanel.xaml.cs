using System.Windows;
using System.Windows.Controls;

namespace TbotUltra.Desktop.Views;

/// <summary>
/// Troops tab panel. Inherits its DataContext (a
/// <see cref="ViewModels.TroopTrainingViewModel"/>) from the host
/// TabItem. Click handlers route through a Host accessor back to
/// MainWindow's internal Core methods that drive _botService and the
/// queue, so the panel itself stays free of service references.
/// </summary>
public partial class TroopsPanel : UserControl
{
    public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
        nameof(Section),
        typeof(string),
        typeof(TroopsPanel),
        new PropertyMetadata("All"));

    public string Section
    {
        get => (string)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    public TroopsPanel()
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

        TroopsTitle.Visibility = Visibility.Collapsed;
        BuildTroopsSection.Visibility = Visibility.Collapsed;
        UpgradeTroopsSection.Visibility = Visibility.Collapsed;
        BreweryCelebrationSection.Visibility = Visibility.Collapsed;
        SectionSpacerColumn.Width = new GridLength(0);

        if (string.Equals(Section, "Build", StringComparison.OrdinalIgnoreCase))
        {
            TroopUtilityColumn.Visibility = Visibility.Collapsed;
            UtilityColumn.Width = new GridLength(0);
            BuildColumn.Width = new GridLength(1, GridUnitType.Star);
            BuildTroopsSection.Visibility = Visibility.Visible;
            return;
        }

        BuildColumn.Width = new GridLength(0);
        UtilityColumn.Width = new GridLength(1, GridUnitType.Star);
        TroopUtilityColumn.Width = double.NaN;
        TroopUtilityColumn.Visibility = Visibility.Visible;
        if (string.Equals(Section, "Upgrade", StringComparison.OrdinalIgnoreCase))
        {
            TroopUtilityColumn.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            TroopUtilityColumn.RowDefinitions[1].Height = new GridLength(0);
            TroopUtilityColumn.RowDefinitions[2].Height = new GridLength(0);
            UpgradeTroopsSection.Visibility = Visibility.Visible;
        }
        else
        {
            TroopUtilityColumn.RowDefinitions[0].Height = new GridLength(0);
            TroopUtilityColumn.RowDefinitions[1].Height = new GridLength(0);
            TroopUtilityColumn.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
            BreweryCelebrationSection.Visibility = Visibility.Visible;
        }
    }

}
