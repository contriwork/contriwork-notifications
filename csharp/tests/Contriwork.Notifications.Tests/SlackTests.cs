using Contriwork.Notifications.Adapters;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Contriwork.Notifications.Tests;

public sealed class SlackTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();
    public void Dispose() => _server.Stop();

    private string Webhook => _server.Url + "/services/T0/B0/secret";
    private static readonly Payload SamplePayload = new("hi", "world");

    private SlackWebhookAdapter Adapter() => new(Webhook);

    [Fact]
    public async Task Delivers_On_2xx()
    {
        _server.Given(Request.Create().WithPath("/services/T0/B0/secret").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("ok"));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.Delivered, result.Status);
    }

    [Fact]
    public async Task Invalid_Payload_On_400()
    {
        _server.Given(Request.Create().WithPath("/services/T0/B0/secret").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(400));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.PermanentFailure, result.Status);
        Assert.Equal(ErrorCode.AdapterInvalidPayload, result.ErrorCode);
    }

    [Fact]
    public async Task Rate_Limited_On_429()
    {
        _server.Given(Request.Create().WithPath("/services/T0/B0/secret").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(429));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.RetriableFailure, result.Status);
        Assert.Equal(ErrorCode.RateLimited, result.ErrorCode);
    }

    [Fact]
    public async Task Upstream_Error_On_500()
    {
        _server.Given(Request.Create().WithPath("/services/T0/B0/secret").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));
        var result = await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: false);
        Assert.Equal(AdapterStatus.RetriableFailure, result.Status);
        Assert.Equal(ErrorCode.UpstreamError, result.ErrorCode);
    }

    [Fact]
    public async Task Silent_Is_NoOp_In_Payload()
    {
        _server.Given(Request.Create().WithPath("/services/T0/B0/secret").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("ok"));
        await Adapter().DeliverAsync(Severity.Info, SamplePayload, silent: true);
        var body = _server.LogEntries.Single().RequestMessage!.Body!;
        // No native silent flag; payload is identical regardless.
        Assert.DoesNotContain("flags", body, StringComparison.Ordinal);
        Assert.DoesNotContain("disable_notification", body, StringComparison.Ordinal);
    }
}
