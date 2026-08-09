using System.Text.Json.Nodes;
using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Services;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class SettingsPersistenceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tbot-settings-persistence-{Guid.NewGuid():N}");

    [Fact]
    public void ResetToDefaults_WhenValidationRejectsDefaults_RestoresPreviousConfig()
    {
        Directory.CreateDirectory(_root);
        var configPath = Path.Combine(_root, "bot.json");
        var store = new BotConfigStore(configPath, _root, () => string.Empty);
        var previous = new JsonObject
        {
            [BotOptionPayloadKeys.AllowGoldSpending] = true,
        };
        store.Save(previous);
        var service = new SettingsPersistenceService(store, _ => "Proxy setup conflict");

        var result = service.ResetToDefaults(previous);

        Assert.Equal("Proxy setup conflict", result.ValidationError);
        Assert.Null(result.Exception);
        Assert.True(store.Load()[BotOptionPayloadKeys.AllowGoldSpending]!.GetValue<bool>());
    }

    [Fact]
    public void Save_WhenValidationRejectsConfig_DoesNotWriteIt()
    {
        Directory.CreateDirectory(_root);
        var configPath = Path.Combine(_root, "bot.json");
        var store = new BotConfigStore(configPath, _root, () => string.Empty);
        store.Save(new JsonObject { [BotOptionPayloadKeys.AllowGoldSpending] = false });
        var service = new SettingsPersistenceService(store, _ => "Proxy setup conflict");

        var result = service.Save(new JsonObject { [BotOptionPayloadKeys.AllowGoldSpending] = true });

        Assert.Equal("Proxy setup conflict", result.ValidationError);
        Assert.False(store.Load()[BotOptionPayloadKeys.AllowGoldSpending]!.GetValue<bool>());
    }

    [Fact]
    public void Save_WhenValidationAcceptsConfig_WritesIt()
    {
        Directory.CreateDirectory(_root);
        var configPath = Path.Combine(_root, "bot.json");
        var store = new BotConfigStore(configPath, _root, () => string.Empty);
        var service = new SettingsPersistenceService(store, _ => null);

        var result = service.Save(new JsonObject { [BotOptionPayloadKeys.AllowGoldSpending] = true });

        Assert.Null(result.ValidationError);
        Assert.Null(result.Exception);
        Assert.True(store.Load()[BotOptionPayloadKeys.AllowGoldSpending]!.GetValue<bool>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
