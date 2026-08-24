using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ProxyWorkflowSourceTests
{
    [Fact]
    public void ProxyFinderListDownload_UsesCancelableBusyOverlayForWholeOperation()
    {
        var source = ReadDesktopSource("ProxyFinderWindow.xaml.cs");
        var method = MethodBody(source, "private async Task LoadProxyListAsync");

        Assert.Contains("BusyOverlay.ShowCancel = true;", method, StringComparison.Ordinal);
        Assert.Contains("BusyOverlay.Show(\"Loading proxy list\"", method, StringComparison.Ordinal);
        Assert.Contains("finally", method, StringComparison.Ordinal);
        Assert.Contains("BusyOverlay.Hide();", method, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosingFinderAfterAddingProxy_ReloadsAccountProxyDropdown()
    {
        var source = ReadDesktopSource("AccountsWindow.xaml.cs");
        var method = MethodBody(source, "private void ProxyFinderButton_Click");
        var showDialog = method.IndexOf("finder.ShowDialog()", StringComparison.Ordinal);
        var reload = method.IndexOf("ReloadProxyLibraryEntries();", StringComparison.Ordinal);

        Assert.True(showDialog >= 0 && reload > showDialog, "The account proxy library must reload whenever Finder closes.");
    }

    [Fact]
    public void ProxyLibraryDelete_PersistsTheSingleEntryImmediately()
    {
        var source = ReadDesktopSource("ProxyLibraryWindow.xaml.cs");
        var method = MethodBody(source, "private void DeleteRowButton_Click");

        Assert.Contains("_store.Remove(entry.Id)", method, StringComparison.Ordinal);
    }

    [Fact]
    public void ProxyLibraryCheck_RequiresTravianReachability()
    {
        var source = ReadDesktopSource("ProxyLibraryWindow.xaml.cs");
        var method = MethodBody(source, "private async Task CheckProxiesAsync");

        Assert.Contains("FilterReachableAsync", method, StringComparison.Ordinal);
        Assert.Contains("TravianTargetUrl", method, StringComparison.Ordinal);
    }

    private static string ReadDesktopSource(string fileName)
    {
        var root = ProjectRootLocator.FindProjectRoot();
        return File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", fileName));
    }

    private static string MethodBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find {signature}.");
        var nextMethod = source.IndexOf("\n    private ", start + signature.Length, StringComparison.Ordinal);
        return nextMethod < 0 ? source[start..] : source[start..nextMethod];
    }
}
