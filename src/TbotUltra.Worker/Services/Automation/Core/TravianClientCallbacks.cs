using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

/// <summary>
/// Optional host callbacks a <see cref="TravianClient"/> calls back into. Bundled into one object so
/// the client's constructor stays a short list of core dependencies instead of a long tail of nullable
/// delegates. Every member is optional; an omitted callback simply disables the flow that needs it
/// (e.g. no <see cref="RunInIsolatedBonusVideoBrowserAsync"/> means the bonus-video flow is skipped).
/// </summary>
public sealed record TravianClientCallbacks
{
    /// <summary>Receives human-readable status/log lines from the client.</summary>
    public Action<string>? StatusCallback { get; init; }

    /// <summary>
    /// Flips the browser session's consentmanager route block on/off. Used only by the bonus-video
    /// flow, which needs GDPR/TCF consent while the rest of the session keeps it blocked.
    /// </summary>
    public Action<bool>? SetConsentDomainsAllowed { get; init; }

    /// <summary>Cleans up after a bonus-video run on the given page.</summary>
    public Func<IPage, CancellationToken, Task>? CleanupAfterBonusVideoAsync { get; init; }

    /// <summary>Runs a bonus-video action inside an isolated browser and returns its result.</summary>
    public Func<Func<IPage, CancellationToken, Task<string>>, CancellationToken, Task<string>>? RunInIsolatedBonusVideoBrowserAsync { get; init; }

    /// <summary>Rotates to a fresh page/browser after a lobby login for the given account.</summary>
    public Func<string, CancellationToken, Task<IPage>>? RotateAfterLobbyLoginAsync { get; init; }

    /// <summary>Asks the host to resolve which world to select at the lobby.</summary>
    public Func<LobbyWorldSelectionRequest, CancellationToken, Task<string?>>? LobbyWorldSelectionRequested { get; init; }

    /// <summary>Notifies the host once a lobby world's server identity is verified.</summary>
    public Func<LobbyWorldServerResolution, CancellationToken, Task>? LobbyWorldServerResolved { get; init; }
}
