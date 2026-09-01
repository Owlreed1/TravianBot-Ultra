using TbotUltra.Worker.Infrastructure;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class AutomationLogContextTests
{
    [Fact]
    public void NestedScopes_MergeKnownFieldsAndRestoreParent()
    {
        using var queueScope = AutomationLogContext.BeginScope(
            task: "build_troops",
            village: "SMET",
            villageKey: "xy:12|-34");
        using (AutomationLogContext.BeginScope(account: "Main account"))
        {
            Assert.Equal(
                new AutomationLogMetadata("Main account", "build_troops", "SMET", 12, -34),
                AutomationLogContext.Capture());
        }

        Assert.Equal(
            new AutomationLogMetadata(null, "build_troops", "SMET", 12, -34),
            AutomationLogContext.Capture());
    }

    [Fact]
    public void VerifiedVillage_ReplacesVillageIdentityForFollowingMessages()
    {
        using var scope = AutomationLogContext.BeginScope(
            account: "Main account",
            task: "scan_all_villages",
            village: "T1",
            villageKey: "xy:1|2");

        AutomationLogContext.UpdateVillage("T2", 3, -4);

        Assert.Equal(
            new AutomationLogMetadata("Main account", "scan_all_villages", "T2", 3, -4),
            AutomationLogContext.Capture());
    }

    [Fact]
    public void NewVillageName_DoesNotRetainPreviousVillageCoordinates()
    {
        using var scope = AutomationLogContext.BeginScope(
            village: "T1",
            villageKey: "xy:1|2");

        AutomationLogContext.UpdateVillageName("T2");

        Assert.Equal(
            new AutomationLogMetadata(null, null, "T2", null, null),
            AutomationLogContext.Capture());
    }

    [Fact]
    public void FormatForHuman_AlwaysMakesMissingVillageExplicit()
    {
        var formatted = AutomationLogContext.FormatForHuman(
            "ALARM: training failed",
            new AutomationLogMetadata("Main account", "build_troops", null, null, null));

        Assert.Equal(
            "ALARM: training failed [context account='Main account' task='build_troops' village='-' xy='-']",
            formatted);
    }

    [Fact]
    public void QueueAndRunnerSources_EstablishTaskVillageAndAccountScopes()
    {
        var root = FindProjectRoot();
        var queueSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Queue",
            "QueueExecutor.cs"));
        var runnerSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "BotTaskRunner.cs"));
        var villageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Core",
            "TravianClient.Villages.Switch.cs"));

        Assert.Contains("task: item.TaskName", queueSource, StringComparison.Ordinal);
        Assert.Contains("villageKey: villageKey", queueSource, StringComparison.Ordinal);
        Assert.Contains("account: account.Name", runnerSource, StringComparison.Ordinal);
        Assert.Contains("BeginScope(task: taskName)", runnerSource, StringComparison.Ordinal);
        Assert.Contains("AutomationLogContext.UpdateVillage(villageName, coordinates.X, coordinates.Y)", villageSource, StringComparison.Ordinal);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TbotUltra.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
