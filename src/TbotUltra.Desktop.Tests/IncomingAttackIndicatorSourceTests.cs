using TbotUltra.Desktop.Models;
using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class IncomingAttackIndicatorSourceTests
{
    [Fact]
    public void VillageIndicator_IsAlwaysVisibleAndPulsesOnlyForIncomingAttacks()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "Views", "DashboardPanel.xaml"));
        var start = xaml.IndexOf("ToolTip=\"{Binding IncomingAttackTooltip}\"", StringComparison.Ordinal);
        var end = xaml.IndexOf("<!-- Enabled indicator:", start, StringComparison.Ordinal);
        var indicator = xaml[start..end];

        Assert.DoesNotContain("Visibility", indicator, StringComparison.Ordinal);
        Assert.Contains("Fill\" Value=\"{DynamicResource BorderMutedBrush}", indicator, StringComparison.Ordinal);
        Assert.Contains("Binding=\"{Binding HasIncomingAttack}\" Value=\"True\"", indicator, StringComparison.Ordinal);
        Assert.Contains("Fill\" Value=\"{DynamicResource DangerBrush}", indicator, StringComparison.Ordinal);
        Assert.Contains("DataTrigger.EnterActions", indicator, StringComparison.Ordinal);
        Assert.Contains("BasedOn=\"{StaticResource {x:Type Button}}\"", indicator, StringComparison.Ordinal);
        Assert.Contains("Background\" Value=\"{DynamicResource SurfaceBrush}", indicator, StringComparison.Ordinal);
        Assert.Contains("Property=\"IsMouseOver\" Value=\"True\"", indicator, StringComparison.Ordinal);
        Assert.Contains("Background\" Value=\"{DynamicResource SurfaceAltBrush}", indicator, StringComparison.Ordinal);
        Assert.Contains("BorderBrush\" Value=\"{DynamicResource BorderHoverBrush}", indicator, StringComparison.Ordinal);
    }

    [Fact]
    public void VillageIndicator_DefaultTooltipReportsNoIncomingAttacks()
    {
        Assert.Equal("No incoming attacks", new VillageSelectionItem().IncomingAttackTooltip);
    }

    [Fact]
    public void VillageIndicator_UsesCompactCrossedSwordsColumnHeader()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "Views", "DashboardPanel.xaml"));

        Assert.Contains("x:Name=\"IncomingAttackColumnHeaderIcon\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"&#x2694;\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FontSize=\"13\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"Incoming attacks\"", xaml, StringComparison.Ordinal);
    }
}
