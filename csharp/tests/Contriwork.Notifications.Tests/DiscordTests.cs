using System.Text.Json;
using Contriwork.Notifications.Adapters;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Contriwork.Notifications.Tests;

public sealed class DiscordTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();
    public void Dispose() => _server.Stop();

    private string Webhook => _server.Url + "/api/webhooks/123/secret";
    private static readonly Payload SamplePayload = new("hi", "world");

    private DiscordWebhookAdapter Adapter() => new(Webhook);

    [Fact]
    public async Task Delivers_On_204()
    {
        _server.Given(Request.Create().WithPath("/api/webhooks/123/secret").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(204));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.Delivered, result.Status);
    }

    [Fact]
    public async Task Invalid_Payload_On_400()
    {
        _server.Given(Request.Create().WithPath("/api/webhooks/123/secret").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(400));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.PermanentFailure, result.Status);
        Assert.Equal(ErrorCode.AdapterInvalidPayload, result.ErrorCode);
    }

    [Fact]
    public async Task Rate_Limited_On_429()
    {
        _server.Given(Request.Create().WithPath("/api/webhooks/123/secret").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(429));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.RetriableFailure, result.Status);
        Assert.Equal(ErrorCode.RateLimited, result.ErrorCode);
    }

    [Fact]
    public async Task Upstream_Error_On_500()
    {
        _server.Given(Request.Create().WithPath("/api/webhooks/123/secret").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.RetriableFailure, result.Status);
        Assert.Equal(ErrorCode.UpstreamError, result.ErrorCode);
    }

    [Fact]
    public async Task Silent_Sets_Suppress_Notifications_Flag()
    {
        _server.Given(Request.Create().WithPath("/api/webhooks/123/secret").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(204));
        await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: true);

        var body = _server.LogEntries.Single().RequestMessage!.Body!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(4096, doc.RootElement.GetProperty("flags").GetInt32());
    }

    [Fact]
    public async Task Non_Silent_Omits_Flags()
    {
        _server.Given(Request.Create().WithPath("/api/webhooks/123/secret").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(204));
        await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);

        var body = _server.LogEntries.Single().RequestMessage!.Body!;
        Assert.DoesNotContain("\"flags\"", body, StringComparison.Ordinal);
    }
}
