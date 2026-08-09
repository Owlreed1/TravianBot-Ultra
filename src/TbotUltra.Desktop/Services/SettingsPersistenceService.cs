using System.Text.Json.Nodes;
using TbotUltra.Core.Configuration;

namespace TbotUltra.Desktop.Services;

/// <summary>Executes the Settings reset transaction and restores the previous config when validation rejects defaults.</summary>
public sealed class SettingsPersistenceService(
    BotConfigStore store,
    Func<JsonObject, string?>? validateBeforeSave)
{
    public JsonObject Load() => store.Load();

    public SettingsSaveResult Save(JsonObject config)
    {
        try
        {
            var validationError = validateBeforeSave?.Invoke(config);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return SettingsSaveResult.ValidationRejected(validationError);
            }

            store.Save(config);
            return SettingsSaveResult.Succeeded;
        }
        catch (Exception exception)
        {
            return SettingsSaveResult.Failed(exception);
        }
    }

    public SettingsResetResult ResetToDefaults(JsonObject currentConfig)
    {
        try
        {
            var previous = (JsonObject)currentConfig.DeepClone();
            store.ResetSettingsToDefaults();
            var reset = Load();
            var validationError = validateBeforeSave?.Invoke(reset);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                store.Save(previous);
                return SettingsResetResult.ValidationRejected(validationError);
            }

            return SettingsResetResult.Succeeded;
        }
        catch (Exception exception)
        {
            return SettingsResetResult.Failed(exception);
        }
    }
}

public sealed record SettingsResetResult(string? ValidationError, Exception? Exception)
{
    public static SettingsResetResult Succeeded { get; } = new(null, null);

    public static SettingsResetResult ValidationRejected(string validationError)
        => new(validationError, null);

    public static SettingsResetResult Failed(Exception exception)
        => new(null, exception);
}

public sealed record SettingsSaveResult(string? ValidationError, Exception? Exception)
{
    public static SettingsSaveResult Succeeded { get; } = new(null, null);

    public static SettingsSaveResult ValidationRejected(string validationError)
        => new(validationError, null);

    public static SettingsSaveResult Failed(Exception exception)
        => new(null, exception);
}
