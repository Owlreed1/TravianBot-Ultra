using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>Owns the farming profile's verified capital read and session-state update.</summary>
internal sealed class CapitalProfileOperation(TravianClient client)
{
    public Task<CapitalProfileCheckResult> CheckAsync(CancellationToken cancellationToken)
        => client.CheckCapitalFromProfileAsync(cancellationToken);

    public Task SetVerifiedStateAsync(CapitalProfileCheckResult capital, CancellationToken cancellationToken)
        => client.SetVerifiedCapitalStateAsync(capital, cancellationToken);
}
