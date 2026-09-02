using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ManualLoginBrowserSessionReplacementSourceTests
{
    [Fact]
    public void SharedBrowser_IsReplacedWhenManualLoginLaunchPolicyChanges()
    {
        var source = ReadSource();
        var replacementStart = source.IndexOf("var mustReplaceSession =", StringComparison.Ordinal);
        var replacementEnd = source.IndexOf("if (mustReplaceSession)", replacementStart, StringComparison.Ordinal);
        var replacementPolicy = source[replacementStart..replacementEnd];

        Assert.Contains("_sharedVisibleManualLogin", replacementPolicy, StringComparison.Ordinal);
        Assert.Contains("account.ManualLogin", replacementPolicy, StringComparison.Ordinal);
        Assert.Contains("_sharedVisibleManualLogin = account.ManualLogin", source, StringComparison.Ordinal);
    }

    private static string ReadSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "TbotUltra.Worker", "Services", "BotTaskRunner.cs");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new DirectoryNotFoundException("Could not locate BotTaskRunner.cs.");
    }
}
