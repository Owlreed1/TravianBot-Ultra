using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>Owns the farming profile's verified capital read and session-state update.</summary>
internal sealed class CapitalProfileOperation(ICapitalProfileClient client)
{
    public Task<CapitalProfileCheckResult> CheckAsync(CancellationToken cancellationToken)
        => client.CheckCapitalFromProfileAsync(cancellationToken);

    public Task SetVerifiedStateAsync(CapitalProfileCheckResult capital, CancellationToken cancellationToken)
        => client.SetVerifiedCapitalStateAsync(capital, cancellationToken);
}
