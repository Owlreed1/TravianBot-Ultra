using System;
using System.IO;
using System.Text.Json;
using TbotUltra.Core.Accounts;

namespace TbotUltra.Desktop.Services;

/// <summary>Per-account persisted deadline for the next Village Status Sweep round.</summary>
public static class VillageStatusSweepStateStore
{
    private static readonly object FileIoLock = new();

    private sealed class StateFile
    {
        public DateTimeOffset NextScanUtc { get; set; }
    }

    public static DateTimeOffset? LoadNextScanUtc(string projectRoot, string? accountName, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return null;
        }

        lock (FileIoLock)
        {
            var path = AccountStoragePaths.VillageStatusSweepStatePath(projectRoot, accountName);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var state = JsonSerializer.Deserialize<StateFile>(File.ReadAllText(path));
                if (state?.NextScanUtc > nowUtc)
                {
                    return state.NextScanUtc.ToUniversalTime();
                }
            }
            catch (JsonException)
            {
                QuarantineCorruptFile(path);
            }
            catch (IOException)
            {
                // A transient file lock is safely treated as ready for this process; the next read retries.
            }
            catch (UnauthorizedAccessException)
            {
                // Access can be temporarily denied by sync/anti-virus software; retry on the next read.
            }

            return null;
        }
    }

    public static bool SaveNextScanUtc(string projectRoot, string? accountName, DateTimeOffset nextScanUtc)
    {
        if (string.IsNullOrWhiteSpace(accountName) || nextScanUtc == DateTimeOffset.MinValue)
        {
            return false;
        }

        lock (FileIoLock)
        {
            try
            {
                var path = AccountStoragePaths.VillageStatusSweepStatePath(projectRoot, accountName);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                AtomicFile.WriteAllText(path, JsonSerializer.Serialize(new StateFile
                {
                    NextScanUtc = nextScanUtc.ToUniversalTime(),
                }));
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    public static bool Clear(string projectRoot, string? accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return false;
        }

        lock (FileIoLock)
        {
            try
            {
                var path = AccountStoragePaths.VillageStatusSweepStatePath(projectRoot, accountName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    private static void QuarantineCorruptFile(string path)
    {
        try
        {
            File.Move(path, $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");
        }
        catch (IOException)
        {
            // A concurrent retry may already have moved the file.
        }
        catch (UnauthorizedAccessException)
        {
            // Leave the file in place; a future read can retry once access is available.
        }
    }
}
