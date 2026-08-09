using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using TbotUltra.Worker.Services.Automation;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class CapitalProfileOperationTests
{
    [Fact]
    public async Task CheckAndSetVerifiedState_ForwardTheExactResultAndCancellationToken()
    {
        var client = new RecordingCapitalProfileClient();
        var operation = new CapitalProfileOperation(client);
        using var cancellation = new CancellationTokenSource();

        var result = await operation.CheckAsync(cancellation.Token);
        await operation.SetVerifiedStateAsync(result, cancellation.Token);

        Assert.Same(client.Result, result);
        Assert.Same(result, client.SavedResult);
        Assert.Equal([cancellation.Token, cancellation.Token], client.CancellationTokens);
    }

    private sealed class RecordingCapitalProfileClient : ICapitalProfileClient
    {
        public CapitalProfileCheckResult Result { get; } = new("Capital", 1, -2);
        public CapitalProfileCheckResult? SavedResult { get; private set; }
        public List<CancellationToken> CancellationTokens { get; } = [];

        public Task<CapitalProfileCheckResult> CheckCapitalFromProfileAsync(CancellationToken cancellationToken = default)
        {
            CancellationTokens.Add(cancellationToken);
            return Task.FromResult(Result);
        }

        public Task SetVerifiedCapitalStateAsync(CapitalProfileCheckResult capital, CancellationToken cancellationToken = default)
        {
            SavedResult = capital;
            CancellationTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }
    }
}
