using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

/// <summary>Verified capital-profile operations exposed by the browser client.</summary>
internal interface ICapitalProfileClient
{
    Task<CapitalProfileCheckResult> CheckCapitalFromProfileAsync(CancellationToken cancellationToken = default);
    Task SetVerifiedCapitalStateAsync(CapitalProfileCheckResult capital, CancellationToken cancellationToken = default);
}

public sealed partial class TravianClient : ICapitalProfileClient
{
}
