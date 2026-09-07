using System.Net;
using System.Net.Sockets;
using System.Text;
using TbotUltra.Worker.Infrastructure;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ProxyAuthenticationTests
{
    [Theory]
    [InlineData("correct", true)]
    [InlineData("incorrect", false)]
    [InlineData("", false)]
    public async Task HttpProbe_AnswersProxyChallengeWithConfiguredCredentials(string password, bool expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requests = 0;
        var authorized = false;
        var expectedHeader = "Proxy-Authorization: Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("user:correct"));
        var serve = Task.Run(async () =>
        {
            try
            {
                while (!timeout.IsCancellationRequested)
                {
                    using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
                    await using var stream = connection.GetStream();
                    using var reader = new StreamReader(stream, leaveOpen: true);
                    var headers = new List<string>();
                    while (await reader.ReadLineAsync(timeout.Token) is { Length: > 0 } line)
                        headers.Add(line);
                    Interlocked.Increment(ref requests);
                    var valid = headers.Contains(expectedHeader, StringComparer.OrdinalIgnoreCase);
                    authorized |= valid;
                    var response = valid
                        ? "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                        : "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"test\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), timeout.Token);
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        }, timeout.Token);
        ProxyProbeResult result;
        try
        {
            var server = ProxyParser.BuildServer("http", "127.0.0.1", port,
                password.Length > 0 ? "user" : null, password);
            result = await ProxyListTester.DefaultProbeAsync(server, "http://proxy-test.invalid/", timeout.Token);
        }
        finally
        {
            await timeout.CancelAsync();
            await serve;
        }
        Assert.True(requests > 0);
        Assert.Equal(expected, authorized);
        Assert.Equal(expected, result.Success);
    }
}
