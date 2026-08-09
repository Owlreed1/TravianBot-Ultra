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
    public TroopsPanel()
    {
        InitializeComponent();
    }

}
